using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;

namespace ApiPlayground.Tests;

internal sealed class Story9DisposableDatabase : IAsyncDisposable
{
    private static readonly Regex DatabaseNamePattern = new(@"\Aapg_mig_[0-9a-f]{32}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RoleNamePattern = new(@"\Aapg_role_[0-9a-f]{32}\z", RegexOptions.CultureInvariant);
    private readonly string _adminConnectionString;
    private bool _databaseCreated;
    private bool _roleCreated;

    private Story9DisposableDatabase(string adminConnectionString, string databaseName, string roleName, string appConnectionString)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        RoleName = roleName;
        AppConnectionString = appConnectionString;
    }

    public string DatabaseName { get; }
    public string RoleName { get; }
    public string AppConnectionString { get; }

    public static string? GetAdminConnectionString()
    {
        var environmentValue = Environment.GetEnvironmentVariable("APIPLAYGROUND_TEST_ADMIN_CONNECTION");
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                var value = DotNetEnv.Env.NoEnvVars().Load(envPath)
                    .LastOrDefault(pair => string.Equals(pair.Key, "APIPLAYGROUND_TEST_ADMIN_CONNECTION", StringComparison.Ordinal))
                    .Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static async Task<Story9DisposableDatabase> CreateAsync(string adminConnectionString)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (string.IsNullOrWhiteSpace(adminBuilder.Host) ||
            !string.Equals(adminBuilder.Database, "postgres", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(adminBuilder.Username))
        {
            throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION must target the PostgreSQL maintenance database 'postgres'; application databases are never accepted.");
        }

        var fixture = new Story9DisposableDatabase(
            adminBuilder.ConnectionString,
            "apg_mig_" + Guid.NewGuid().ToString("N"),
            "apg_role_" + Guid.NewGuid().ToString("N"),
            string.Empty);
        ValidateGeneratedIdentifiers(fixture.DatabaseName, fixture.RoleName);
        var rolePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var appBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = fixture.DatabaseName,
            Username = fixture.RoleName,
            Password = rolePassword,
        };
        var initialized = new Story9DisposableDatabase(adminBuilder.ConnectionString, fixture.DatabaseName, fixture.RoleName, appBuilder.ConnectionString);

        try
        {
            await initialized.ValidateAdminAsync();
            var quotedDatabase = QuoteIdentifier(initialized.DatabaseName);
            var quotedRole = QuoteIdentifier(initialized.RoleName);
            await initialized.ExecuteAdminAsync($"CREATE DATABASE {quotedDatabase}");
            initialized._databaseCreated = true;

            // rolePassword is cryptographically random hexadecimal and stays only in process memory.
            await initialized.ExecuteAdminAsync($"CREATE ROLE {quotedRole} LOGIN PASSWORD '{rolePassword}' NOCREATEDB NOCREATEROLE NOSUPERUSER NOREPLICATION NOBYPASSRLS");
            initialized._roleCreated = true;
            await initialized.ExecuteAdminAsync($"REVOKE ALL PRIVILEGES ON DATABASE {quotedDatabase} FROM PUBLIC");
            await initialized.ExecuteAdminAsync($"GRANT CONNECT ON DATABASE {quotedDatabase} TO {quotedRole}");

            var databaseAdminBuilder = new NpgsqlConnectionStringBuilder(initialized._adminConnectionString) { Database = initialized.DatabaseName };
            await ExecuteAsync(databaseAdminBuilder.ConnectionString,
                "REVOKE ALL PRIVILEGES ON SCHEMA public FROM PUBLIC");
            await ExecuteAsync(databaseAdminBuilder.ConnectionString,
                $"GRANT USAGE, CREATE ON SCHEMA public TO {quotedRole}");
            await initialized.AssertAppRoleAsync(appBuilder.ConnectionString);

            return initialized;
        }
        catch (Exception originalException)
        {
            try
            {
                await initialized.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Disposable PostgreSQL initialization failed and exact-resource cleanup also failed; inspect the generated database and role IDs privately.", originalException, cleanupException);
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Exception? cleanupError = null;
        try
        {
            if (_databaseCreated)
            {
                var name = QuoteIdentifier(DatabaseName);
                var databaseAdminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
                {
                    Database = DatabaseName,
                };
                ClearPool(databaseAdminBuilder.ConnectionString);
                ClearPool(AppConnectionString);
                await ExecuteAdminAsync($"DROP DATABASE {name}");
                _databaseCreated = false;
            }
        }
        catch (Exception exception)
        {
            cleanupError = exception;
        }

        try
        {
            if (_roleCreated)
            {
                var name = QuoteIdentifier(RoleName);
                await ExecuteAdminAsync($"DROP ROLE IF EXISTS {name}");
                _roleCreated = false;
            }
        }
        catch (Exception exception)
        {
            cleanupError ??= exception;
        }

        if (cleanupError is not null)
            throw new InvalidOperationException("Disposable PostgreSQL cleanup failed. Inspect the harness-generated database and role IDs privately and recover only those exact resources after verifying ownership.", cleanupError);
    }

    private async Task ValidateAdminAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT rolcanlogin, rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls FROM pg_roles WHERE rolname = current_user";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || !reader.GetBoolean(0) || reader.GetBoolean(1) || !reader.GetBoolean(2) ||
            !reader.GetBoolean(3) || reader.GetBoolean(4) || reader.GetBoolean(5))
        {
            throw new InvalidOperationException("The test admin must be LOGIN NOSUPERUSER CREATEDB CREATEROLE NOREPLICATION NOBYPASSRLS on postgres. Provision this limited role through a trusted local PostgreSQL administrator.");
        }
    }

    private async Task AssertAppRoleAsync(string appConnectionString)
    {
        await ExecuteAdminAssertionAsync($"SELECT rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls FROM pg_roles WHERE rolname = '{RoleName}'");
        await ExecuteAdminAssertionAsync($"SELECT NOT EXISTS (SELECT 1 FROM pg_auth_members WHERE member = '{RoleName}'::regrole)");
        await ExecuteAdminAssertionAsync($"SELECT has_database_privilege('{RoleName}', '{DatabaseName}', 'CONNECT') AND NOT has_database_privilege('{RoleName}', '{DatabaseName}', 'CREATE') AND NOT has_database_privilege('{RoleName}', '{DatabaseName}', 'TEMP')");

        var databaseAdminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = DatabaseName };
        await using (var adminConnection = new NpgsqlConnection(databaseAdminBuilder.ConnectionString))
        {
            await adminConnection.OpenAsync();
            await using var schemaCommand = adminConnection.CreateCommand();
            schemaCommand.CommandText = $"SELECT has_schema_privilege('{RoleName}', 'public', 'USAGE') AND has_schema_privilege('{RoleName}', 'public', 'CREATE')";
            if (!Equals(await schemaCommand.ExecuteScalarAsync(), true))
                throw new InvalidOperationException("The app role must have the explicit public-schema USAGE and CREATE grants needed by migrations.");
        }

        await using var appConnection = new NpgsqlConnection(appConnectionString);
        await appConnection.OpenAsync();
        await using var appCommand = appConnection.CreateCommand();
        appCommand.CommandText = "SELECT current_database(), current_user";
        await using var appReader = await appCommand.ExecuteReaderAsync();
        if (!await appReader.ReadAsync() || appReader.GetString(0) != DatabaseName || appReader.GetString(1) != RoleName)
            throw new InvalidOperationException("The app connection must resolve only to its generated disposable database and login.");
    }

    private async Task ExecuteAdminAssertionAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (!Equals(await command.ExecuteScalarAsync(), true))
            throw new InvalidOperationException("A disposable database/app-role privilege assertion failed.");
    }

    private Task ExecuteAdminAsync(string sql) => ExecuteAsync(_adminConnectionString, sql);

    private static void ClearPool(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static void ValidateGeneratedIdentifiers(string databaseName, string roleName)
    {
        if (!DatabaseNamePattern.IsMatch(databaseName) || !RoleNamePattern.IsMatch(roleName))
            throw new InvalidOperationException("Refusing PostgreSQL DDL for identifiers outside the exact generated disposable-name patterns.");
    }
}
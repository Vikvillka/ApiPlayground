using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class MigrationContinuityTests
{
    private const string InitialMigrationId = "20261018000000_InitialPlatformBaseline";
    private const string HistoryMigrationId = "20261023000000_RequestHistory";
    private const string CollectionsMigrationId = "20261025165909_RequestCollections";
    private readonly string _adminConnectionString;

    public MigrationContinuityTests()
    {
        _adminConnectionString = GetTestAdminConnection()
            ?? throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION is required for PostgreSQL migration integration tests; tests never skip.");
        var admin = new NpgsqlConnectionStringBuilder(_adminConnectionString);
        if (string.IsNullOrWhiteSpace(admin.Host)
            || !string.Equals(admin.Database, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(admin.Username))
        {
            throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION must target the PostgreSQL maintenance database 'postgres'; application databases are never accepted.");
        }
    }

    private static string? GetTestAdminConnection()
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

    [Fact]
    public async Task Fresh_disposable_database_applies_original_migration_once()
    {
        await WithDisposableDatabase(async connectionString =>
        {
            await MigrateAsync(connectionString);
            var migrationIds = await ReadMigrationIdsAsync(connectionString);

            Assert.Equal([InitialMigrationId, HistoryMigrationId, CollectionsMigrationId], migrationIds);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'request_history'";
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        });
    }

    [Fact]
    public void Infrastructure_exposes_the_original_migration_in_its_single_stream()
    {
        var options = new DbContextOptionsBuilder<ApiPlayground.Api.Data.PlatformDbContext>()
            .UseNpgsql(_adminConnectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
            .Options;
        using var context = new ApiPlayground.Api.Data.PlatformDbContext(options);

        Assert.Equal([InitialMigrationId, HistoryMigrationId, CollectionsMigrationId], context.Database.GetMigrations());
    }

    [Fact]
    public async Task Story_one_migrated_database_recognizes_original_migration_without_duplicate()
    {
        await WithDisposableDatabase(async connectionString =>
        {
            await MigrateAsync(connectionString, typeof(LegacyMigrationFixture.LegacyStoryOneInitialPlatformBaseline).Assembly.GetName().Name);
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE \"__EFMigrationsHistory\" SET \"ProductVersion\" = '10.0.0' WHERE \"MigrationId\" = @migrationId";
                command.Parameters.AddWithValue("migrationId", InitialMigrationId);
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            await MigrateAsync(connectionString);
            var migrationIds = await ReadMigrationIdsAsync(connectionString);

            Assert.Equal([InitialMigrationId, HistoryMigrationId, CollectionsMigrationId], migrationIds);
        });
    }

    [Fact]
    public async Task Temporary_following_migration_is_discovered_and_applied_in_isolated_workspace()
    {
        await WithDisposableDatabase(async connectionString =>
        {
            var backendRoot = FindBackendRoot();
            var workspace = Path.Combine(Path.GetTempPath(), "apg_migration_workspace_" + Guid.NewGuid().ToString("N"));
            var copiedBackend = Path.Combine(workspace, "backend");
            Directory.CreateDirectory(copiedBackend);

            try
            {
                File.Copy(Path.Combine(backendRoot, "ApiPlayground.sln"), Path.Combine(copiedBackend, "ApiPlayground.sln"));
                CopyDirectory(Path.Combine(backendRoot, ".config"), Path.Combine(copiedBackend, ".config"));
                CopyDirectory(Path.Combine(backendRoot, "src"), Path.Combine(copiedBackend, "src"));
                Directory.CreateDirectory(Path.Combine(copiedBackend, "tests", "ApiPlayground.Tests"));
                File.Copy(
                    Path.Combine(backendRoot, "tests", "ApiPlayground.Tests", "ApiPlayground.Tests.csproj"),
                    Path.Combine(copiedBackend, "tests", "ApiPlayground.Tests", "ApiPlayground.Tests.csproj"));
                await RunDotnetAsync(copiedBackend, "tool restore");
                var environment = new Dictionary<string, string?>
                {
                    ["ConnectionStrings__Postgres"] = connectionString,
                };
                await RunDotnetAsync(copiedBackend, "restore ApiPlayground.sln");
                await RunDotnetAsync(
                    copiedBackend,
                    "ef database update --project src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj --startup-project src/ApiPlayground.Api/ApiPlayground.Api.csproj",
                    environment);
                var initialMigrationIds = await ReadMigrationIdsAsync(connectionString);
                Assert.Equal([InitialMigrationId, HistoryMigrationId, CollectionsMigrationId], initialMigrationIds);
                AddTemporaryMigrationModel(copiedBackend);
                await RunDotnetAsync(
                    copiedBackend,
                    "ef migrations add Ticket10TemporaryContinuity --project src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj --startup-project src/ApiPlayground.Api/ApiPlayground.Api.csproj --output-dir Data/Migrations",
                    environment);
                var generatedMigration = Directory.EnumerateFiles(
                    Path.Combine(copiedBackend, "src", "ApiPlayground.Infrastructure", "Data", "Migrations"),
                    "*_Ticket10TemporaryContinuity.cs").Single();
                Assert.Contains("Ticket10TemporaryContinuity", File.ReadAllText(generatedMigration), StringComparison.Ordinal);
                var generatedId = Path.GetFileNameWithoutExtension(generatedMigration);
                var generatedDesigner = Path.Combine(
                    Path.GetDirectoryName(generatedMigration)!,
                    Path.GetFileNameWithoutExtension(generatedMigration) + ".Designer.cs");
                Assert.True(File.Exists(generatedDesigner), "EF must generate migration target-model metadata for the temporary continuation.");
                var designerContent = File.ReadAllText(generatedDesigner);
                Assert.Contains("BuildTargetModel", designerContent, StringComparison.Ordinal);
                Assert.Contains("ticket_10_temporary_marker", designerContent, StringComparison.Ordinal);
                var snapshotPath = Path.Combine(
                    copiedBackend,
                    "src",
                    "ApiPlayground.Infrastructure",
                    "Data",
                    "Migrations",
                    "PlatformDbContextModelSnapshot.cs");
                Assert.Contains("ticket_10_temporary_marker", File.ReadAllText(snapshotPath), StringComparison.Ordinal);
                await RunDotnetAsync(
                    copiedBackend,
                    "ef database update --project src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj --startup-project src/ApiPlayground.Api/ApiPlayground.Api.csproj",
                    environment);

                var migrationIds = await ReadMigrationIdsAsync(connectionString);
                Assert.Equal(4, migrationIds.Length);
                Assert.Equal(InitialMigrationId, migrationIds[0]);
                Assert.Equal(HistoryMigrationId, migrationIds[1]);
                Assert.Equal(CollectionsMigrationId, migrationIds[2]);
                Assert.Equal(generatedId, migrationIds[3]);
            }
            finally
            {
                if (Directory.Exists(workspace))
                {
                    Directory.Delete(workspace, recursive: true);
                }
            }
        });
    }

    internal async Task WithDisposableDatabase(Func<string, Task> test)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString);
        var databaseName = "apg_mig_" + Guid.NewGuid().ToString("N");
        var testBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = databaseName };
        var quotedName = "\"" + databaseName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var created = false;

        try
        {
            await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE {quotedName}";
                await command.ExecuteNonQueryAsync();
                created = true;
            }

            try
            {
                await using (var connection = new NpgsqlConnection(testBuilder.ConnectionString))
                {
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT current_database()";
                    Assert.Equal(databaseName, await command.ExecuteScalarAsync());
                }

                await test(testBuilder.ConnectionString);
            }
            finally
            {
                await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS {quotedName} WITH (FORCE)";
                await command.ExecuteNonQueryAsync();
                created = false;
            }
        }
        finally
        {
            if (created)
            {
                await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS {quotedName} WITH (FORCE)";
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task MigrateAsync(string connectionString, string? migrationsAssembly = "ApiPlayground.Infrastructure")
    {
        var options = new DbContextOptionsBuilder<ApiPlayground.Api.Data.PlatformDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly(migrationsAssembly))
            .Options;
        await using var context = new ApiPlayground.Api.Data.PlatformDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static async Task<string[]> ReadMigrationIdsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"";
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids.ToArray();
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApiPlayground.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate backend/ApiPlayground.sln from the test output directory.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetFileName(file).StartsWith(".env", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            if (Path.GetFileName(directory) is "bin" or "obj" or ".git" or ".vs")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void AddTemporaryMigrationModel(string copiedBackend)
    {
        var contextPath = Path.Combine(copiedBackend, "src", "ApiPlayground.Infrastructure", "Data", "PlatformDbContext.cs");
        var context = File.ReadAllText(contextPath);
        var normalizedContext = context.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string original = "\t\tvar history = modelBuilder.Entity<RequestHistoryEntity>();";
        const string replacement = original + "\n\t\tmodelBuilder.Entity<Ticket10TemporaryMarker>(entity =>\n\t\t{\n\t\t\tentity.ToTable(\"ticket_10_temporary_marker\");\n\t\t\tentity.HasKey(marker => marker.Id);\n\t\t});";

        Assert.Contains(original, normalizedContext, StringComparison.Ordinal);
        var markerPath = Path.Combine(copiedBackend, "src", "ApiPlayground.Infrastructure", "Data", "Ticket10TemporaryMarker.cs");
        File.WriteAllText(markerPath, "namespace ApiPlayground.Api.Data;\n\npublic sealed class Ticket10TemporaryMarker\n{\n    public int Id { get; set; }\n}\n");
        File.WriteAllText(contextPath, normalizedContext.Replace(original, replacement, StringComparison.Ordinal));
    }

    private static async Task RunDotnetAsync(
        string workingDirectory,
        string arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start dotnet {arguments}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;

        Assert.True(process.ExitCode == 0, $"dotnet {arguments} failed with exit code {process.ExitCode}.\n{output}\n{error}");
    }
}

using Npgsql;
using Xunit;

namespace ApiPlayground.Tests;

// Owner rows must exist in AspNetUsers because owner_id references it. Tests that hit PostgreSQL seed them here.
internal static class TestUsers
{
    internal static async Task InsertAsync(string connectionString, Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        var name = $"user-{id:N}";
        insert.CommandText = "INSERT INTO \"AspNetUsers\" (\"Id\", \"UserName\", \"NormalizedUserName\", \"Email\", \"NormalizedEmail\", "
            + "\"EmailConfirmed\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\") "
            + "VALUES (@id, @name, @normalizedName, @email, @normalizedEmail, false, false, false, false, 0)";
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("name", name);
        insert.Parameters.AddWithValue("normalizedName", name.ToUpperInvariant());
        insert.Parameters.AddWithValue("email", $"{name}@example.test");
        insert.Parameters.AddWithValue("normalizedEmail", $"{name}@example.test".ToUpperInvariant());
        Assert.Equal(1, await insert.ExecuteNonQueryAsync());
    }
}

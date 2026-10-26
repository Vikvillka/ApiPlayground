using ApiPlayground.Infrastructure.Outbound;
using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class TracerPersistenceSmokeCheckTests
{
    [Fact]
    public async Task Actual_smoke_check_round_trips_and_rolls_back_generated_table()
    {
        var migrationTests = new MigrationContinuityTests();
        await migrationTests.WithDisposableDatabase(async connectionString =>
        {
            await using var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
            var options = new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(dataSource).Options;
            await using var dbContext = new PlatformDbContext(options);
            await dbContext.Database.OpenConnectionAsync();
            var smokeCheck = new PostgresTracerPersistenceSmokeCheck(dbContext);
            Assert.True(await smokeCheck.VerifyRoundTripAsync());

            var connection = dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pg_class WHERE relname LIKE 'apg_tracer_smoke_%'";
            var tableName = await command.ExecuteScalarAsync();
            Assert.Equal(0L, Convert.ToInt64(tableName));
        });
    }
}
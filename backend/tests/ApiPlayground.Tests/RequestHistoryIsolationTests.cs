using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApiPlayground.Tests;

// Two-user isolation for request history against PostgreSQL. These tests never skip: they fail when
// APIPLAYGROUND_TEST_ADMIN_CONNECTION is missing, through the disposable-database harness.
public sealed class RequestHistoryIsolationTests
{
    [Fact]
    public async Task User_b_cannot_list_find_or_read_user_a_history_entries()
    {
        var continuity = new MigrationContinuityTests();
        await continuity.WithDisposableDatabase(async connectionString =>
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
                .Options;
            await using (var setup = new PlatformDbContext(options))
                await setup.Database.MigrateAsync();

            var ownerA = Guid.NewGuid();
            var ownerB = Guid.NewGuid();
            await TestUsers.InsertAsync(connectionString, ownerA);
            await TestUsers.InsertAsync(connectionString, ownerB);

            var entry = NewHistorySnapshot();
            await new PostgresRequestHistoryRepository(new PlatformDbContext(options)).SaveAsync(ownerA, entry);

            await using (var contextA = new PlatformDbContext(options))
            {
                Assert.Equal(entry.Id, Assert.Single(await new PostgresRequestHistoryRepository(contextA).ListAsync(ownerA)).Id);
            }

            var repositoryB = new PostgresRequestHistoryRepository(new PlatformDbContext(options));
            Assert.Empty(await repositoryB.ListAsync(ownerB));
            Assert.Null(await repositoryB.FindAsync(ownerB, entry.Id));

            // A DbSet read with user B's scope must not see user A's row, even when addressed by id.
            await using var directB = new PlatformDbContext(options) { OwnerScope = ownerB };
            Assert.Null(await directB.RequestHistory.SingleOrDefaultAsync(item => item.Id == entry.Id));
        });
    }

    [Fact]
    public async Task Queries_without_an_owner_scope_return_no_history_rows()
    {
        var continuity = new MigrationContinuityTests();
        await continuity.WithDisposableDatabase(async connectionString =>
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
                .Options;
            await using (var setup = new PlatformDbContext(options))
                await setup.Database.MigrateAsync();

            var owner = Guid.NewGuid();
            await TestUsers.InsertAsync(connectionString, owner);
            await new PostgresRequestHistoryRepository(new PlatformDbContext(options)).SaveAsync(owner, NewHistorySnapshot());

            await using var unscoped = new PlatformDbContext(options);
            Assert.Empty(await unscoped.RequestHistory.ToListAsync());
        });
    }

    private static RequestHistorySnapshot NewHistorySnapshot() => RequestHistoryRedaction.Create(
        new OutboundRequest(new Uri("http://127.0.0.1:49152/history"), "GET"),
        new OutboundResult(OutboundFailure.None, 200, TimeSpan.FromMilliseconds(1),
            new Dictionary<string, IReadOnlyList<string>>(), "ok"u8.ToArray(), DispatchStarted: true));
}

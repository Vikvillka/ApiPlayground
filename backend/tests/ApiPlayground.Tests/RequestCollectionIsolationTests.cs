using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApiPlayground.Tests;

// Two-user isolation for collections and saved requests against PostgreSQL. These tests never skip: they fail when
// APIPLAYGROUND_TEST_ADMIN_CONNECTION is missing, through the disposable-database harness.
public sealed class RequestCollectionIsolationTests
{
    [Fact]
    public async Task User_b_cannot_list_or_read_user_a_collections_or_saved_requests()
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

            var saved = await new SaveRequestUseCase(new PostgresRequestCollectionRepository(new PlatformDbContext(options)))
                .SaveAsync(ownerA, NewCollectionCommand("Alpha"));

            await using (var contextB = new PlatformDbContext(options))
            {
                Assert.Empty(await new PostgresRequestCollectionRepository(contextB).ListAsync(ownerB));
            }

            // Reads through the DbSet with user B's scope must not see user A's rows, even when addressed by id.
            await using var directB = new PlatformDbContext(options) { OwnerScope = ownerB };
            Assert.Null(await directB.SavedRequests.SingleOrDefaultAsync(item => item.Id == saved.Id));
            Assert.Empty(await directB.RequestCollections.ToListAsync());
        });
    }

    [Fact]
    public async Task User_b_saving_into_user_a_collection_gets_not_found_and_leaves_it_unchanged()
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

            await new SaveRequestUseCase(new PostgresRequestCollectionRepository(new PlatformDbContext(options)))
                .SaveAsync(ownerA, NewCollectionCommand("Alpha"));
            var collectionId = Assert.Single(await new PostgresRequestCollectionRepository(new PlatformDbContext(options))
                .ListAsync(ownerA)).Id;

            var useCaseB = new SaveRequestUseCase(new PostgresRequestCollectionRepository(new PlatformDbContext(options)));
            var foreignSave = new SaveRequestCommand(collectionId, null, "GET /intruder", "GET", "https://example.test/intruder",
                new Dictionary<string, string>(), null);
            var error = await Assert.ThrowsAsync<CollectionNotFoundException>(() => useCaseB.SaveAsync(ownerB, foreignSave));
            Assert.Equal("The selected collection was not found.", error.Message);

            await using var verificationContext = new PlatformDbContext(options);
            var alpha = Assert.Single(await new PostgresRequestCollectionRepository(verificationContext).ListAsync(ownerA));
            Assert.Single(alpha.Requests);
            Assert.DoesNotContain(alpha.Requests, request => request.Title == "GET /intruder");
        });
    }

    [Fact]
    public async Task Queries_without_an_owner_scope_return_no_collection_or_saved_request_rows()
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
            await new SaveRequestUseCase(new PostgresRequestCollectionRepository(new PlatformDbContext(options)))
                .SaveAsync(owner, NewCollectionCommand("Alpha"));

            await using var unscoped = new PlatformDbContext(options);
            Assert.Empty(await unscoped.RequestCollections.ToListAsync());
            Assert.Empty(await unscoped.SavedRequests.ToListAsync());
        });
    }

    private static SaveRequestCommand NewCollectionCommand(string collectionName) => new(null, collectionName, "GET /items", "GET",
        "https://example.test/items", new Dictionary<string, string>(), null);
}

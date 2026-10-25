using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class RequestCollectionTests
{
    [Fact]
    public async Task Save_redacts_sensitive_headers_and_preserves_other_request_fields()
    {
        var store = new InMemoryCollectionStore();
        var useCase = new SaveRequestUseCase(store);
        var saved = await useCase.SaveAsync(new SaveRequestCommand(null, "Reusable", "PATCH /resource", "PATCH",
            "https://example.test/resource?tag=one&access_token=private&mode=full", new Dictionary<string, string>
            {
                ["Authorization"] = "private",
                ["X-Proxy-Token-Name"] = "secret",
                ["X-Ordinary"] = "visible",
            }, "{\"enabled\":true}"));

        Assert.Equal("PATCH /resource", saved.Title);
        Assert.Equal("https://example.test/resource?tag=one&access_token=&mode=full", saved.Url);
        Assert.Equal("{\"enabled\":true}", saved.Body);
        Assert.Equal(string.Empty, saved.Headers["Authorization"]);
        Assert.Equal(string.Empty, saved.Headers["X-Proxy-Token-Name"]);
        Assert.Equal("visible", saved.Headers["X-Ordinary"]);
        Assert.Equal(new[] { "Authorization", "X-Proxy-Token-Name" }, saved.RedactedHeaders);
    }

    [Fact]
    public async Task Save_use_case_rejects_ambiguous_or_invalid_requests_before_persistence()
    {
        var store = new InMemoryCollectionStore();
        var useCase = new SaveRequestUseCase(store);
        var valid = new SaveRequestCommand(null, "Requests", "GET /", "GET", "https://example.test/",
            new Dictionary<string, string>(), null);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { CollectionId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Url = "file:///private" }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Method = "TRACE" }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Body = "{invalid" }));
        Assert.Empty(store.Collections);
    }

    [Fact]
    public async Task Postgres_save_survives_new_context_and_rolls_back_inline_collection_on_failure()
    {
        var continuity = new MigrationContinuityTests();
        await continuity.WithDisposableDatabase(async connectionString =>
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
                .Options;
            await using (var setup = new PlatformDbContext(options))
                await setup.Database.MigrateAsync();

            var useCase = new SaveRequestUseCase(new PostgresRequestCollectionStore(new PlatformDbContext(options)));
            var saved = await useCase.SaveAsync(new SaveRequestCommand(null, "Persisted", "GET /items", "GET",
                "https://example.test/items?mode=full&password=private", new Dictionary<string, string>
                {
                    ["Cookie"] = "private-cookie",
                    ["X-Visible"] = "retained",
                }, null));

            await using (var freshContext = new PlatformDbContext(options))
            {
                var reloaded = Assert.Single(await new PostgresRequestCollectionStore(freshContext).ListAsync());
                var request = Assert.Single(reloaded.Requests);
                Assert.Equal("Persisted", reloaded.Name);
                Assert.Equal(saved.Id, request.Id);
                Assert.Equal("GET /items", request.Title);
                Assert.Equal("https://example.test/items?mode=full&password=", request.Url);
                Assert.Equal(string.Empty, request.Headers["Cookie"]);
                Assert.Equal("retained", request.Headers["X-Visible"]);
                Assert.Contains("Cookie", request.RedactedHeaders);
            }

            await using (var freshContext = new PlatformDbContext(options))
            {
                var store = new PostgresRequestCollectionStore(freshContext);
                var invalidRow = new SavedRequestSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty, string.Empty,
                    new string('x', 121), "GET", "https://example.test/", new Dictionary<string, string>(), [], null);
                await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(invalidRow, null, "Must roll back"));
            }

            await using var verificationContext = new PlatformDbContext(options);
            var collections = await new PostgresRequestCollectionStore(verificationContext).ListAsync();
            Assert.Single(collections);
            Assert.DoesNotContain(collections, item => item.Name == "Must roll back");
        });
    }

    private sealed class InMemoryCollectionStore : IRequestCollectionStore
    {
        public List<RequestCollectionSnapshot> Collections { get; } = [];

        public Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestCollectionSnapshot>>(Collections.ToArray());

        public Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionId is null
                ? new RequestCollectionSnapshot(Guid.NewGuid(), newCollectionName!, [])
                : Collections.Single(item => item.Id == collectionId.Value);
            var saved = request with { CollectionId = collection.Id, CollectionName = collection.Name };
            var existingIndex = Collections.FindIndex(item => item.Id == collection.Id);
            var updated = collection with { Requests = collection.Requests.Append(saved).ToArray() };
            if (existingIndex >= 0) Collections[existingIndex] = updated;
            else Collections.Add(updated);
            return Task.FromResult(saved);
        }
    }
}
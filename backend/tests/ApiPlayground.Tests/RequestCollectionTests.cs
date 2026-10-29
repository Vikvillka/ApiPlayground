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
        var store = new InMemoryCollectionRepository();
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
    public async Task Save_redacts_all_sensitive_header_categories_case_insensitively_like_history()
    {
        var headers = new Dictionary<string, string>
        {
            ["AUTHORIZATION"] = "private",
            ["pRoXy-AuThOrIzAtIoN"] = "private",
            ["cOoKiE"] = "private",
            ["sEt-CoOkIe"] = "private",
            ["X-API-KEY"] = "private",
            ["X-Access-TOKEN"] = "private",
            ["X-Ordinary"] = "visible",
        };
        var useCase = new SaveRequestUseCase(new InMemoryCollectionRepository());
        var command = new SaveRequestCommand(null, "Reusable", "GET /", "GET", "https://example.test/", headers, null);

        var saved = await useCase.SaveAsync(command);
        var history = RequestHistoryRedaction.Create(
            new OutboundRequest(new Uri(command.Url), command.Method, headers),
            new OutboundResult(OutboundFailure.None, 200, TimeSpan.Zero,
                new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty, DispatchStarted: true));

        Assert.Equal(history.Headers, saved.Headers);
        Assert.Equal(history.RedactedHeaders, saved.RedactedHeaders);
        Assert.All(headers.Keys.Where(name => name != "X-Ordinary"), name => Assert.Equal(string.Empty, saved.Headers[name]));
        Assert.Equal("visible", saved.Headers["X-Ordinary"]);
        Assert.DoesNotContain("X-Ordinary", saved.RedactedHeaders);
    }

    [Fact]
    public async Task Save_use_case_rejects_ambiguous_or_invalid_requests_before_persistence()
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var valid = new SaveRequestCommand(null, "Requests", "GET /", "GET", "https://example.test/",
            new Dictionary<string, string>(), null);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { CollectionId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Url = "file:///private" }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Method = "TRACE" }));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(valid with { Body = "{invalid" }));
        Assert.Empty(store.Collections);
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("get")]
    public async Task Save_rejects_unsupported_or_nonuppercase_methods_with_the_existing_message(string method)
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var command = new SaveRequestCommand(null, "Requests", "request", method, "https://example.test/",
            new Dictionary<string, string>(), null);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(command));

        Assert.Equal("Choose a supported HTTP method.", error.Message);
        Assert.Empty(store.Collections);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://example.test/")]
    [InlineData("https://user:pass@example.test/")]
    [InlineData("https://example.test/#fragment")]
    public async Task Save_rejects_urls_outside_common_http_uri_shape_with_existing_message(string url)
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var command = new SaveRequestCommand(null, "Requests", "request", "GET", url,
            new Dictionary<string, string>(), null);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(command));

        Assert.Equal("Enter a valid absolute HTTP or HTTPS URL within the 2048 character limit.", error.Message);
        Assert.Empty(store.Collections);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Save_accepts_each_exact_supported_method(string method)
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var command = new SaveRequestCommand(null, "Requests", "request", method, "https://example.test/",
            new Dictionary<string, string>(), null);

        var saved = await useCase.SaveAsync(command);

        Assert.Equal(method, saved.Method);
        Assert.Single(store.Collections);
    }

    [Fact]
    public async Task Save_preserves_whitespace_body_as_absent_and_rejects_blank_or_duplicate_headers_before_persistence()
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var valid = new SaveRequestCommand(null, "Requests", "POST /", "POST", "https://example.test/",
            new Dictionary<string, string>(), "   ");

        var saved = await useCase.SaveAsync(valid);

        Assert.Null(saved.Body);
        var blankHeader = valid with { Headers = new Dictionary<string, string> { [" "] = "value" } };
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(blankHeader));
        var duplicateHeaders = new Dictionary<string, string> { ["X-Example"] = "one", ["x-example"] = "two" };
        var duplicateError = await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.SaveAsync(valid with { Headers = duplicateHeaders }));
        Assert.Equal("Enabled request header names must be unique and non-empty.", duplicateError.Message);
        var getWhitespaceBodyError = await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.SaveAsync(valid with { Method = "GET" }));
        Assert.Equal("JSON bodies are only supported for POST, PUT, and PATCH.", getWhitespaceBodyError.Message);
        Assert.Single(store.Collections);
        Assert.Single(Assert.Single(store.Collections).Requests);
    }

    [Fact]
    public async Task Save_keeps_method_validation_ahead_of_url_validation()
    {
        var store = new InMemoryCollectionRepository();
        var useCase = new SaveRequestUseCase(store);
        var command = new SaveRequestCommand(null, "Requests", "GET /", "trace", "not-a-url",
            new Dictionary<string, string>(), null);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveAsync(command));

        Assert.Equal("Choose a supported HTTP method.", error.Message);
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

            var useCase = new SaveRequestUseCase(new PostgresRequestCollectionRepository(new PlatformDbContext(options)));
            var saved = await useCase.SaveAsync(new SaveRequestCommand(null, "Persisted", "GET /items", "GET",
                "https://example.test/items?mode=full&password=private", new Dictionary<string, string>
                {
                    ["Cookie"] = "private-cookie",
                    ["X-Visible"] = "retained",
                }, null));

            await using (var freshContext = new PlatformDbContext(options))
            {
                var reloaded = Assert.Single(await new PostgresRequestCollectionRepository(freshContext).ListAsync());
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
                var store = new PostgresRequestCollectionRepository(freshContext);
                var invalidRow = new SavedRequestSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty, string.Empty,
                    new string('x', 121), "GET", "https://example.test/", new Dictionary<string, string>(), [], null);
                await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(invalidRow, null, "Must roll back"));
            }

            await using var verificationContext = new PlatformDbContext(options);
            var collections = await new PostgresRequestCollectionRepository(verificationContext).ListAsync();
            Assert.Single(collections);
            Assert.DoesNotContain(collections, item => item.Name == "Must roll back");
        });
    }

    private sealed class InMemoryCollectionRepository : IRequestCollectionRepository
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
using ApiPlayground.Application.Outbound;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class RequestHistoryTests
{
    [Fact]
    public void Snapshot_redacts_sensitive_header_values_and_query_values()
    {
        var request = new OutboundRequest(
            new Uri("https://example.test/path?tag=ok&access_token=private&Api-Key=hidden&x=1"), "POST",
            new Dictionary<string, string>
            {
                ["Authorization"] = "bearer-secret",
                ["X-Api-Key-Id"] = "private-key",
                ["X-Ordinary"] = "visible",
            }, System.Text.Encoding.UTF8.GetBytes("{\"safe\":true}"), "application/json");

        var snapshot = RequestHistoryRedaction.Create(request, SuccessResult(), DateTimeOffset.UnixEpoch);

        Assert.Equal("https://example.test/path?tag=ok&access_token=&Api-Key=&x=1", snapshot.Url);
        Assert.Equal(new[] { "access_token", "Api-Key" }, snapshot.RedactedQueryParameters);
        Assert.Equal(string.Empty, snapshot.Headers["Authorization"]);
        Assert.Equal(string.Empty, snapshot.Headers["X-Api-Key-Id"]);
        Assert.Equal("visible", snapshot.Headers["X-Ordinary"]);
        Assert.Equal(new[] { "Authorization", "X-Api-Key-Id" }, snapshot.RedactedHeaders);
        Assert.Equal("{\"safe\":true}", snapshot.Body);
    }

    [Fact]
    public void Snapshot_redacts_all_sensitive_header_categories_case_insensitively()
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
        var request = new OutboundRequest(new Uri("https://example.test/"), "GET", headers);

        var snapshot = RequestHistoryRedaction.Create(request, SuccessResult());

        Assert.All(headers.Keys.Where(name => name != "X-Ordinary"), name => Assert.Equal(string.Empty, snapshot.Headers[name]));
        Assert.Equal(headers.Keys.Where(name => name != "X-Ordinary"), snapshot.RedactedHeaders);
        Assert.Equal("visible", snapshot.Headers["X-Ordinary"]);
        Assert.DoesNotContain("X-Ordinary", snapshot.RedactedHeaders);
    }

    [Theory]
    [InlineData(OutboundFailure.None, true)]
    [InlineData(OutboundFailure.Timeout, true)]
    [InlineData(OutboundFailure.TransportFailure, true)]
    [InlineData(OutboundFailure.PolicyRejected, false)]
    public async Task Use_case_records_only_dispatch_started_attempts(OutboundFailure failure, bool dispatchStarted)
    {
        var store = new InMemoryHistoryStore();
        var useCase = new GetTracerUseCase(new FixedExecutor(new OutboundResult(failure, null, TimeSpan.FromMilliseconds(5),
            new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty, "diagnostic", dispatchStarted)),
            new SuccessfulSmokeCheck(), store);

        var result = await useCase.ExecuteAsync(new OutboundRequest(new Uri("https://example.test/"), "GET"));

        Assert.Equal(dispatchStarted, result.Outbound!.DispatchStarted);
        Assert.Equal(dispatchStarted ? 1 : 0, store.Items.Count);
    }

    [Fact]
    public async Task History_storage_failure_does_not_replace_dispatch_result()
    {
        var expected = SuccessResult();
        var useCase = new GetTracerUseCase(new FixedExecutor(expected), new SuccessfulSmokeCheck(), new FailingHistoryStore());

        var result = await useCase.ExecuteAsync(new OutboundRequest(new Uri("https://example.test/"), "GET"));

        Assert.Same(expected, result.Outbound);
        Assert.False(result.HistorySaved);
        Assert.Contains("History entry was not saved", result.Error);
    }

    [Fact]
    public async Task Postgres_store_uses_stable_newest_first_order_and_does_not_archive_response_data()
    {
        var store = new InMemoryHistoryStore();
        var older = RequestHistoryRedaction.Create(new OutboundRequest(new Uri("https://example.test/older"), "GET"), SuccessResult(), DateTimeOffset.UnixEpoch);
        var newerA = RequestHistoryRedaction.Create(new OutboundRequest(new Uri("https://example.test/newer-a"), "GET"), SuccessResult(), DateTimeOffset.UnixEpoch.AddSeconds(1));
        var newerB = newerA with { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") };
        await store.SaveAsync(older);
        await store.SaveAsync(newerA);
        await store.SaveAsync(newerB);

        var listed = await store.ListAsync();

        Assert.Equal(new[] { newerB.Id, newerA.Id, older.Id }, listed.Select(item => item.Id));
        Assert.DoesNotContain("response", typeof(RequestHistorySnapshot).GetProperties().Select(property => property.Name), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Application_history_use_case_loads_the_requested_snapshot()
    {
        var store = new InMemoryHistoryStore();
        var snapshot = RequestHistoryRedaction.Create(new OutboundRequest(new Uri("https://example.test/load?token="), "GET"), SuccessResult());
        await store.SaveAsync(snapshot);

        var loaded = await new RequestHistoryUseCase(store).FindAsync(snapshot.Id);

        Assert.Equal(snapshot, loaded);
    }

    private static OutboundResult SuccessResult() => new(OutboundFailure.None, 200, TimeSpan.FromMilliseconds(3),
        new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty, DispatchStarted: true);

    private sealed class FixedExecutor(OutboundResult result) : IOutboundRequestExecutor
    {
        public Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class SuccessfulSmokeCheck : ITracerPersistenceSmokeCheck
    {
        public Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class InMemoryHistoryStore : IRequestHistoryStore
    {
        public List<RequestHistorySnapshot> Items { get; } = [];
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Items.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>(Items.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToArray());
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
    }

    private sealed class FailingHistoryStore : IRequestHistoryStore
    {
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default) => throw new InvalidOperationException("storage unavailable");
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>([]);
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<RequestHistorySnapshot?>(null);
    }
}
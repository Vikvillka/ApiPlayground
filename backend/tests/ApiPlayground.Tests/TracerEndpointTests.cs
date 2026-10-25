using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class TracerEndpointTests
{
    private static HttpRequestMessage CreateAllowedRequest(string url, HttpMethod? method = null, string? path = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, path ?? (method == HttpMethod.Get ? "/api/tracer/history" : "/api/tracer/get"))
        {
            Content = method == HttpMethod.Get ? null : JsonContent.Create(new { url }),
        };
        request.Headers.Host = "localhost:5000";
        request.Headers.Add("Origin", "http://127.0.0.1:5173");
        request.Headers.Add("X-ApiPlayground-Request", "1");
        return request;
    }

    [Theory]
    [InlineData("localhost:5001", "http://127.0.0.1:5173")]
    [InlineData("127.0.0.1:5000", "http://127.0.0.1:5173")]
    [InlineData("localhost:5000", "http://localhost:5173")]
    [InlineData("localhost:5000", "null")]
    [InlineData("localhost:5000", null)]
    public async Task Invalid_host_or_origin_is_rejected_before_dispatch(string host, string? origin)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Headers.Host = host;
        request.Headers.Remove("Origin");
        if (origin is not null)
            request.Headers.Add("Origin", origin);

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("text/plain")]
    public async Task Non_json_content_type_is_rejected(string contentType)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = new StringContent("{}", Encoding.UTF8, contentType);
        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Missing_custom_header_is_rejected()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Headers.Remove("X-ApiPlayground-Request");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Invalid_allowed_url_is_rejected_without_sending()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("not-a-url");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Oversized_body_is_rejected_before_dispatch()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = new StringContent("{\"url\":\"" + new string('a', 5000) + "\"}", Encoding.UTF8, "application/json");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Oversized_url_is_rejected_before_dispatch()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/" + new string('a', 2100));

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Content-Length")]
    [InlineData("Content-Type")]
    public async Task Managed_request_headers_are_rejected_before_dispatch(string headerName)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new { url = "https://example.test/", headers = new Dictionary<string, string> { [headerName] = "value" } });

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task URL_at_maximum_length_is_not_rejected_by_length_validation()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        var url = "https://example.test/" + new string('a', 2048 - "https://example.test/".Length);
        using var request = CreateAllowedRequest(url);

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task Allowed_proxy_host_and_origin_pass_guard_and_reach_dispatch()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, executor.Calls);
    }

    [Theory]
    [InlineData("/api/tracer/get/")]
    [InlineData("/API/TRACER/GET")]
    public async Task Tracer_route_variants_are_protected_before_dispatch(string path)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.RequestUri = new Uri(path, UriKind.Relative);
        request.Headers.Remove("Origin");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Valid_request_runs_exact_guarded_loopback_get_and_persistence_check()
    {
        await using var target = await Outbound.ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.ContentType = "text/plain";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("controlled tracer response"));
        });
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new GuardedTargetHttpClient(
                OutboundPolicy.ForTestOnlyLoopbackEndpoint(target.BaseAddress),
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback })));
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(new InMemoryHistoryStore());
        }));
        await using (factory)
        using (var request = CreateAllowedRequest(target.BaseAddress + "?tag=ok&access_token=secret"))
        using (var response = await factory.CreateClient().SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();
            Assert.NotNull(payload);
            Assert.Equal("None", payload.Failure);
            Assert.Equal(200, payload.StatusCode);
            Assert.Equal("controlled tracer response", payload.Body);
            Assert.True(payload.PersistenceVerified);
            Assert.True(payload.RequestSent);
            using var historyRequest = CreateAllowedRequest(target.BaseAddress.ToString(), HttpMethod.Get, "/api/tracer/history");
            using var historyResponse = await factory.CreateClient().SendAsync(historyRequest);
            var history = await historyResponse.Content.ReadFromJsonAsync<HistoryDto[]>();
            Assert.NotNull(history);
            var recorded = Assert.Single(history!);
            Assert.Contains("access_token=", recorded.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", recorded.Url, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Collection_routes_are_guarded_and_save_maps_only_application_data()
    {
        var store = new InMemoryCollectionStore();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestCollectionStore>();
            services.AddSingleton<IRequestCollectionStore>(store);
        }));
        var client = factory.CreateClient();

        using (var blockedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tracer/collections"))
        using (var blockedResponse = await client.SendAsync(blockedRequest))
        {
            Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
        }

        using (var saveRequest = CreateAllowedRequest("https://example.test/items?mode=full", path: "/api/tracer/collections/requests"))
        {
            saveRequest.Content = JsonContent.Create(new
            {
                collectionId = (Guid?)null,
                newCollectionName = "Reusable",
                title = "GET /items",
                method = "GET",
                url = "https://example.test/items?mode=full",
                headers = new Dictionary<string, string> { ["Cookie"] = "private", ["X-Visible"] = "yes" },
            });
            using var savedResponse = await client.SendAsync(saveRequest);
            Assert.Equal(HttpStatusCode.Created, savedResponse.StatusCode);
            var saved = await savedResponse.Content.ReadFromJsonAsync<SavedRequestDto>();
            Assert.NotNull(saved);
            Assert.Equal("GET /items", saved.Title);
            Assert.Equal(string.Empty, saved.Headers.Single(header => header.Name == "Cookie").Value);
            Assert.True(saved.Headers.Single(header => header.Name == "Cookie").Redacted);
            Assert.Equal("yes", saved.Headers.Single(header => header.Name == "X-Visible").Value);
        }

        using var listRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/collections");
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var collections = await listResponse.Content.ReadFromJsonAsync<CollectionDto[]>();
        Assert.NotNull(collections);
        Assert.Equal("Reusable", Assert.Single(collections!).Name);
        Assert.Single(collections[0].Requests);
    }

    [Fact]
    public async Task Collections_list_persistence_failure_returns_sanitized_service_unavailable()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestCollectionStore>();
            services.AddSingleton<IRequestCollectionStore>(new FailingCollectionStore());
        }));
        using var request = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/collections");

        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Collections could not be loaded", body);
        Assert.DoesNotContain("database password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Http_error_response_preserves_target_status_elapsed_and_repeated_headers()
    {
        await using var target = await Outbound.ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("X-Observed", "first");
            context.Response.Headers.Append("X-Observed", "second");
            context.Response.Headers.Append("X-Only", "value");
            await context.Response.WriteAsync("{\"error\":\"slow down\"}");
        });
        var factory = CreateGuardedFactory(target.BaseAddress);
        await using (factory)
        using (var request = CreateAllowedRequest(target.BaseAddress.ToString()))
        using (var response = await factory.CreateClient().SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();
            Assert.NotNull(payload);
            Assert.Equal("None", payload.Failure);
            Assert.Equal(429, payload.StatusCode);
            Assert.True(payload.ElapsedMilliseconds >= 0);
            Assert.Equal("{\"error\":\"slow down\"}", payload.Body);
            Assert.Equal(new[] { "first", "second" }, payload.Headers["X-Observed"]);
            Assert.Equal(new[] { "value" }, payload.Headers["X-Only"]);
            Assert.True(payload.RequestSent);
        }
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "{\"hello\":\"world\"}")]
    [InlineData("PUT", "{\"hello\":\"world\"}")]
    [InlineData("PATCH", "{\"hello\":\"world\"}")]
    [InlineData("DELETE", "")]
    public async Task Configured_method_query_headers_and_optional_json_reach_guarded_target(string method, string body)
    {
        string? observedMethod = null;
        string? observedQuery = null;
        string? observedHeader = null;
        string? observedBody = null;
        await using var target = await Outbound.ControlledLoopbackServer.StartAsync(async context =>
        {
            observedMethod = context.Request.Method;
            observedQuery = context.Request.QueryString.Value;
            observedHeader = context.Request.Headers["X-Test-Value"].ToString();
            using var reader = new StreamReader(context.Request.Body);
            observedBody = await reader.ReadToEndAsync();
            await context.Response.WriteAsync("ok");
        });
        var factory = CreateGuardedFactory(target.BaseAddress);
        await using (factory)
        using (var request = CreateAllowedRequest(target.BaseAddress + "?existing=one&tag=old&tag=again&tag=new%20value"))
        {
            request.Content = JsonContent.Create(new
            {
                url = target.BaseAddress + "?existing=one&tag=old&tag=again&tag=new%20value",
                method,
                body = string.IsNullOrEmpty(body) ? null : body,
                headers = new Dictionary<string, string> { ["X-Test-Value"] = "configured" },
            });
            using var response = await factory.CreateClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(method, observedMethod);
            Assert.Equal("?existing=one&tag=old&tag=again&tag=new%20value", observedQuery);
            Assert.Equal("configured", observedHeader);
            Assert.Equal(body, observedBody);
        }
    }

    [Theory]
    [InlineData("{not-json}", "POST")]
    [InlineData("{\"x\":1}", "GET")]
    [InlineData("{\"x\":1}", "DELETE")]
    public async Task Invalid_json_or_body_method_is_rejected_before_dispatch(string body, string method)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new { url = "https://example.test/", method, body });

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Duplicate_header_names_are_rejected_before_dispatch_case_insensitively()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new
        {
            url = "https://example.test/",
            headers = new Dictionary<string, string> { ["X-Example"] = "one", ["x-example"] = "two" },
        });

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Unsupported_method_is_rejected_before_dispatch()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new { url = "https://example.test/", method = "TRACE" });

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.Calls);
    }

    private static WebApplicationFactory<Program> CreateGuardedFactory(Uri targetAddress) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new GuardedTargetHttpClient(
                OutboundPolicy.ForTestOnlyLoopbackEndpoint(targetAddress),
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback })));
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(new InMemoryHistoryStore());
        }));

    [Fact]
    public async Task Policy_rejection_is_reported_as_not_sent()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new FixedResultExecutor(new OutboundResult(
                OutboundFailure.PolicyRejected, null, TimeSpan.Zero,
                new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty)));
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(new InMemoryHistoryStore());
        }));
        await using (factory)
        using (var request = CreateAllowedRequest("http://127.0.0.1/"))
        using (var response = await factory.CreateClient().SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();
            Assert.NotNull(payload);
            Assert.False(payload.RequestSent);
            Assert.Equal("Blocked by security policy. Request not sent.", payload.Error);
            Assert.Null(payload.StatusCode);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingExecutor executor) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(executor);
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(new InMemoryHistoryStore());
        }));

    private sealed class RecordingExecutor : IOutboundRequestExecutor
    {
        public int Calls { get; private set; }

        public Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new OutboundResult(OutboundFailure.PolicyRejected, null, TimeSpan.Zero,
                new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty));
        }
    }

    private sealed class FixedResultExecutor(OutboundResult result) : IOutboundRequestExecutor
    {
        public Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class SuccessfulPersistenceCheck : ITracerPersistenceSmokeCheck
    {
        public Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class InMemoryHistoryStore : IRequestHistoryStore
    {
        private readonly List<RequestHistorySnapshot> _items = [];
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _items.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>(_items.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToArray());
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
    }

    private sealed class InMemoryCollectionStore : IRequestCollectionStore
    {
        private readonly List<RequestCollectionSnapshot> _items = [];
        public Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestCollectionSnapshot>>(_items.ToArray());

        public Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionId is null
                ? new RequestCollectionSnapshot(Guid.NewGuid(), newCollectionName!, [])
                : _items.Single(item => item.Id == collectionId.Value);
            var saved = request with { CollectionId = collection.Id, CollectionName = collection.Name };
            var updated = collection with { Requests = collection.Requests.Append(saved).ToArray() };
            var index = _items.FindIndex(item => item.Id == collection.Id);
            if (index < 0) _items.Add(updated);
            else _items[index] = updated;
            return Task.FromResult(saved);
        }
    }

    private sealed class FailingCollectionStore : IRequestCollectionStore
    {
        public Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database password: secret; Npgsql failure details");

        public Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
            CancellationToken cancellationToken = default) => Task.FromResult(request);
    }

    private sealed record TracerResponse(string Failure, int? StatusCode, double ElapsedMilliseconds,
        Dictionary<string, string[]> Headers, string Body, bool PersistenceVerified, string? Error, bool RequestSent);

    private sealed record HistoryDto(Guid Id, string Url, string[] RedactedQueryParameters);
    private sealed record SavedRequestDto(Guid Id, string Title, HeaderDto[] Headers);
    private sealed record HeaderDto(string Name, string Value, bool Redacted);
    private sealed record CollectionDto(Guid Id, string Name, SavedRequestDto[] Requests);
}

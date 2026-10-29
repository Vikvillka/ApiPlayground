using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
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

    [Theory]
    [InlineData("", "GET", "Enter a URL to send a request.")]
    [InlineData("   ", "GET", "Enter a URL to send a request.")]
    [InlineData("not-a-url", "GET", "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments.")]
    [InlineData("ftp://example.test/", "GET", "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments.")]
    [InlineData("https://user:pass@example.test/", "GET", "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments.")]
    [InlineData("https://example.test/#fragment", "GET", "Enter a valid absolute HTTP or HTTPS URL without credentials or fragments.")]
    [InlineData("https://example.test/", "TRACE", "Choose a supported HTTP method.")]
    [InlineData("https://example.test/", "POST", "Enter a valid JSON body.")]
    public async Task Validation_preserves_existing_message_and_does_not_dispatch(string url, string method, string error)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest(url);
        request.Content = JsonContent.Create(new { url, method, body = "{invalid" });

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, payload?.Status);
        Assert.Equal(error, payload?.Detail);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Each_exact_supported_method_reaches_dispatch(string method)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new { url = "https://example.test/", method });

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task Validation_order_keeps_blank_url_before_method_and_method_before_url_shape()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        var client = factory.CreateClient();

        using var blankUrlRequest = CreateAllowedRequest(string.Empty);
        blankUrlRequest.Content = JsonContent.Create(new { url = string.Empty, method = "TRACE", body = "{" });
        using var blankUrlResponse = await client.SendAsync(blankUrlRequest);
        var blankUrlPayload = await blankUrlResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        using var invalidMethodRequest = CreateAllowedRequest("not-a-url");
        invalidMethodRequest.Content = JsonContent.Create(new { url = "not-a-url", method = "TRACE", body = "{" });
        using var invalidMethodResponse = await client.SendAsync(invalidMethodRequest);
        var invalidMethodPayload = await invalidMethodResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal("Enter a URL to send a request.", blankUrlPayload?.Detail);
        Assert.Equal("Choose a supported HTTP method.", invalidMethodPayload?.Detail);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Tracer_keeps_whitespace_body_and_blank_header_validation_caller_specific()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var whitespaceBodyRequest = CreateAllowedRequest("https://example.test/");
        whitespaceBodyRequest.Content = JsonContent.Create(new { url = "https://example.test/", method = "POST", body = "   " });

        using var whitespaceBodyResponse = await factory.CreateClient().SendAsync(whitespaceBodyRequest);
        var bodyError = await whitespaceBodyResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        using var blankHeaderRequest = CreateAllowedRequest("https://example.test/");
        blankHeaderRequest.Content = JsonContent.Create(new
        {
            url = "https://example.test/",
            headers = new Dictionary<string, string> { [" "] = "value" },
        });
        using var blankHeaderResponse = await factory.CreateClient().SendAsync(blankHeaderRequest);
        var headerError = await blankHeaderResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal("Enter a valid JSON body.", bodyError?.Detail);
        Assert.Equal("Host, Content-Length, and Content-Type headers are managed by the request sender.", headerError?.Detail);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"url\":3}")]
    [InlineData("{\"url\":\"https://example.test/\",\"method\":3}")]
    public async Task Invalid_json_binding_returns_bodyless_bad_request_without_dispatch(string json)
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(body);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Wrong_json_content_type_is_rejected_by_the_existing_request_guard()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = new StringContent("{\"url\":\"https://example.test/\"}", Encoding.UTF8, "application/problem+json");

        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEmpty(body);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData(OutboundFailure.Timeout, 504)]
    [InlineData(OutboundFailure.TransportFailure, 502)]
    [InlineData(OutboundFailure.ResponseTooLarge, 502)]
    [InlineData(OutboundFailure.Cancelled, 499)]
    public async Task Outbound_failures_keep_their_status_and_rich_result_fields(OutboundFailure failure, int statusCode)
    {
        var outbound = new OutboundResult(failure, null, TimeSpan.FromMilliseconds(12),
            new Dictionary<string, IReadOnlyList<string>> { ["X-Test"] = ["value"] },
            Encoding.UTF8.GetBytes("partial"), "outbound diagnostic", DispatchStarted: true);
        await using var factory = CreateFactory(new FixedResultExecutor(outbound));
        using var request = CreateAllowedRequest("https://example.test/");

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();

        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(failure.ToString(), payload.Failure);
        Assert.Equal("partial", payload.Body);
        Assert.Equal(new[] { "value" }, payload.Headers["X-Test"]);
        Assert.Equal("outbound diagnostic", payload.Error);
        Assert.True(payload.RequestSent);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "History entry was not saved")]
    public async Task History_outcome_does_not_replace_a_successful_dispatch(bool historySaveSucceeds, string? expectedError)
    {
        var executor = new FixedResultExecutor(new OutboundResult(OutboundFailure.None, 201, TimeSpan.FromMilliseconds(8),
            new Dictionary<string, IReadOnlyList<string>>(), Encoding.UTF8.GetBytes("created"), DispatchStarted: true));
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(executor);
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(historySaveSucceeds ? new InMemoryHistoryRepository() : new FailingHistoryRepository());
        }));
        await using var ownedFactory = factory;
        using var request = CreateAllowedRequest("https://example.test/");

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("None", payload.Failure);
        Assert.Equal(201, payload.StatusCode);
        Assert.Equal("created", payload.Body);
        Assert.Equal(historySaveSucceeds, payload.HistorySaved);
        if (expectedError is null)
            Assert.Null(payload.Error);
        else
            Assert.Contains(expectedError, payload.Error);
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
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(new InMemoryHistoryRepository());
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
            Assert.True(payload.RequestSent);
            Assert.True(payload.HistorySaved);
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
    public async Task History_routes_preserve_list_lookup_wire_shape_redaction_and_bodyless_not_found()
    {
        var id = Guid.NewGuid();
        var historyStore = new InMemoryHistoryRepository();
        await historyStore.SaveAsync(new RequestHistorySnapshot(
            id,
            DateTimeOffset.Parse("2026-10-29T12:34:56Z"),
            "POST",
            "https://example.test/items?access_token=&mode=full",
            ["access_token"],
            new Dictionary<string, string> { ["Authorization"] = string.Empty, ["X-Visible"] = "yes" },
            ["Authorization"],
            "{\"hello\":\"world\"}",
            "None",
            201,
            12.5));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(historyStore);
        }));
        var client = factory.CreateClient();

        using var listRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/history");
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listed = Assert.Single(list.RootElement.EnumerateArray().ToArray());
        AssertHistoryContract(listed, id);

        using var findRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, $"/api/tracer/history/{id:D}");
        using var findResponse = await client.SendAsync(findRequest);
        Assert.Equal(HttpStatusCode.OK, findResponse.StatusCode);
        using var found = JsonDocument.Parse(await findResponse.Content.ReadAsStringAsync());
        AssertHistoryContract(found.RootElement, id);

        using var missingRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, $"/api/tracer/history/{Guid.NewGuid():D}");
        using var missingResponse = await client.SendAsync(missingRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Empty(await missingResponse.Content.ReadAsByteArrayAsync());

        using var malformedRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/history/not-a-guid");
        using var malformedResponse = await client.SendAsync(malformedRequest);
        Assert.Equal(HttpStatusCode.NotFound, malformedResponse.StatusCode);
        Assert.Empty(await malformedResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Application_route_inventory_uses_controller_actions_without_duplicate_minimal_routes()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("api/tracer/", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.Equal(5, routes.Length);
        Assert.All(routes, endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()));

        var routeMethods = routes.Select(endpoint => (
            endpoint.RoutePattern.RawText,
            Methods: endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ToArray()))
            .ToHashSet();
        Assert.Contains(("api/tracer/get", new[] { "POST" }), routeMethods, RouteMethodComparer.Instance);
        Assert.Contains(("api/tracer/history", new[] { "GET" }), routeMethods, RouteMethodComparer.Instance);
        Assert.Contains(("api/tracer/history/{id:guid}", new[] { "GET" }), routeMethods, RouteMethodComparer.Instance);
        Assert.Contains(("api/tracer/collections", new[] { "GET" }), routeMethods, RouteMethodComparer.Instance);
        Assert.Contains(("api/tracer/collections/requests", new[] { "POST" }), routeMethods, RouteMethodComparer.Instance);
    }

    [Theory]
    [InlineData("GET", "/api/tracer/history")]
    [InlineData("GET", "/api/tracer/history/10000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/tracer/collections")]
    [InlineData("POST", "/api/tracer/collections/requests")]
    public async Task Every_migrated_route_is_rejected_by_guard_without_required_headers(string method, string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? new StringContent("{}", Encoding.UTF8, "application/json") : null,
        };

        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Request origin or headers are not allowed.", body.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/tracer/history")]
    [InlineData("GET", "/api/tracer/history/10000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/tracer/collections")]
    [InlineData("POST", "/api/tracer/collections/requests")]
    public async Task Every_migrated_route_rejects_disallowed_host_and_origin(string method, string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        foreach (var (host, origin) in new (string Host, string Origin)[]
        {
            ("localhost:5001", "http://127.0.0.1:5173"),
            ("localhost:5000", "http://localhost:5173"),
        })
        {
            using var request = CreateAllowedRequest(string.Empty, new HttpMethod(method), path);
            request.Headers.Host = host;
            request.Headers.Remove("Origin");
            request.Headers.Add("Origin", origin);

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Request origin or headers are not allowed.", body.RootElement.GetProperty("error").GetString());
        }
    }

    [Theory]
    [InlineData("GET", "/api/tracer/history")]
    [InlineData("GET", "/api/tracer/history/10000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/tracer/collections")]
    [InlineData("POST", "/api/tracer/collections/requests")]
    public async Task Every_migrated_route_remains_under_the_path_prefix_body_limit(string method, string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var request = CreateAllowedRequest(string.Empty, new HttpMethod(method), path);
        request.Content = new StringContent("{\"payload\":\"" + new string('x', 5000) + "\"}", Encoding.UTF8, "application/json");

        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"title\":3}")]
    public async Task Collection_save_preserves_empty_bad_request_for_invalid_body_binding(string json)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var request = CreateAllowedRequest(string.Empty, path: "/api/tracer/collections/requests");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task History_store_exceptions_return_sanitized_problem_details()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(new ThrowingHistoryRepository());
        }));
        using var request = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/history");

        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storage unavailable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Collections could not be loaded", body, StringComparison.Ordinal);
        var problem = JsonSerializer.Deserialize<ProblemResponse>(body, JsonSerializerOptions.Web);
        Assert.Equal(500, problem?.Status);
        Assert.Equal("The request could not be completed. Try again later.", problem?.Detail);
    }

    [Fact]
    public async Task Unknown_non_tracer_routes_remain_bodyless_not_found()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var request = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/unknown-route");

        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Collection_routes_are_guarded_and_save_maps_only_application_data()
    {
        var store = new InMemoryCollectionRepository();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestCollectionRepository>();
            services.AddSingleton<IRequestCollectionRepository>(store);
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
            Assert.NotEqual(Guid.Empty, saved.Id);
            Assert.NotEqual(default, saved.CreatedAt);
            Assert.NotEqual(Guid.Empty, saved.CollectionId);
            Assert.Equal("Reusable", saved.CollectionName);
            Assert.Equal("GET /items", saved.Title);
            Assert.Equal("GET", saved.Method);
            Assert.Equal("https://example.test/items?mode=full", saved.Url);
            Assert.Equal(string.Empty, saved.Headers.Single(header => header.Name == "Cookie").Value);
            Assert.True(saved.Headers.Single(header => header.Name == "Cookie").Redacted);
            Assert.Equal("yes", saved.Headers.Single(header => header.Name == "X-Visible").Value);
            Assert.False(saved.Headers.Single(header => header.Name == "X-Visible").Redacted);
            Assert.Equal("Cookie", Assert.Single(saved.RedactedHeaders));
            Assert.Null(saved.Body);
        }

        using var listRequest = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/collections");
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var collections = await listResponse.Content.ReadFromJsonAsync<CollectionDto[]>();
        Assert.NotNull(collections);
        var collection = Assert.Single(collections!);
        Assert.NotEqual(Guid.Empty, collection.Id);
        Assert.Equal("Reusable", collection.Name);
        var listedRequest = Assert.Single(collection.Requests);
        Assert.NotEqual(Guid.Empty, listedRequest.Id);
        Assert.Equal("GET /items", listedRequest.Title);
        Assert.Equal("Reusable", listedRequest.CollectionName);
    }

    [Fact]
    public async Task Collections_list_persistence_failure_returns_sanitized_service_unavailable()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestCollectionRepository>();
            services.AddSingleton<IRequestCollectionRepository>(new FailingCollectionRepository());
        }));
        using var request = CreateAllowedRequest(string.Empty, HttpMethod.Get, "/api/tracer/collections");

        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Collections could not be loaded. Try again.", JsonSerializer.Deserialize<ProblemResponse>(body, JsonSerializerOptions.Web)?.Detail);
        Assert.DoesNotContain("database password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Collection_save_validation_failure_returns_problem_details_without_storage_errors()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var request = CreateAllowedRequest(string.Empty, path: "/api/tracer/collections/requests");
        request.Content = JsonContent.Create(new
        {
            collectionId = (Guid?)null,
            newCollectionName = "Reusable",
            title = " ",
            method = "GET",
            url = "https://example.test/items",
        });

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Enter a request title of 1 to 120 characters.", payload?.Detail);
    }

    [Fact]
    public async Task Collection_save_storage_failure_returns_sanitized_service_unavailable()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestCollectionRepository>();
            services.AddSingleton<IRequestCollectionRepository>(new FailingCollectionRepository());
        }));
        using var request = CreateAllowedRequest(string.Empty, path: "/api/tracer/collections/requests");
        request.Content = JsonContent.Create(new
        {
            collectionId = (Guid?)null,
            newCollectionName = "Reusable",
            title = "GET /items",
            method = "GET",
            url = "https://example.test/items",
        });

        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("The request could not be saved. Your request is still available to retry.", JsonSerializer.Deserialize<ProblemResponse>(body, JsonSerializerOptions.Web)?.Detail);
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

        var payload = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Enabled request header names must be unique.", payload?.Detail);
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

    [Fact]
    public async Task Lowercase_supported_method_is_rejected_with_existing_message_before_dispatch()
    {
        var executor = new RecordingExecutor();
        await using var factory = CreateFactory(executor);
        using var request = CreateAllowedRequest("https://example.test/");
        request.Content = JsonContent.Create(new { url = "https://example.test/", method = "get" });

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Choose a supported HTTP method.", payload?.Detail);
        Assert.Equal(0, executor.Calls);
    }

    private static WebApplicationFactory<Program> CreateGuardedFactory(Uri targetAddress) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new GuardedTargetHttpClient(
                OutboundPolicy.ForTestOnlyLoopbackEndpoint(targetAddress),
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback })));
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(new InMemoryHistoryRepository());
        }));

    private static void AssertHistoryContract(JsonElement history, Guid id)
    {
        Assert.Equal(id, history.GetProperty("id").GetGuid());
        Assert.Equal("POST", history.GetProperty("method").GetString());
        Assert.Equal("https://example.test/items?access_token=&mode=full", history.GetProperty("url").GetString());
        Assert.Equal("{\"hello\":\"world\"}", history.GetProperty("body").GetString());
        Assert.Equal("None", history.GetProperty("failure").GetString());
        Assert.Equal(201, history.GetProperty("statusCode").GetInt32());
        Assert.Equal(12.5, history.GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal("access_token", Assert.Single(history.GetProperty("redactedQueryParameters").EnumerateArray()).GetString());
        Assert.Equal("Authorization", Assert.Single(history.GetProperty("redactedHeaders").EnumerateArray()).GetString());
        var headers = history.GetProperty("headers").EnumerateArray().ToArray();
        Assert.Equal(string.Empty, headers.Single(header => header.GetProperty("name").GetString() == "Authorization").GetProperty("value").GetString());
        Assert.True(headers.Single(header => header.GetProperty("name").GetString() == "Authorization").GetProperty("redacted").GetBoolean());
        Assert.Equal("yes", headers.Single(header => header.GetProperty("name").GetString() == "X-Visible").GetProperty("value").GetString());
        Assert.False(headers.Single(header => header.GetProperty("name").GetString() == "X-Visible").GetProperty("redacted").GetBoolean());
    }

    [Fact]
    public async Task Policy_rejection_is_reported_as_not_sent()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new FixedResultExecutor(new OutboundResult(
                OutboundFailure.PolicyRejected, null, TimeSpan.Zero,
                new Dictionary<string, IReadOnlyList<string>>(), ReadOnlyMemory<byte>.Empty)));
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(new InMemoryHistoryRepository());
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

    private static WebApplicationFactory<Program> CreateFactory(IOutboundRequestExecutor executor) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(executor);
            services.RemoveAll<IRequestHistoryRepository>();
            services.AddSingleton<IRequestHistoryRepository>(new InMemoryHistoryRepository());
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

    private sealed class FailingHistoryRepository : IRequestHistoryRepository
    {
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage unavailable");
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>([]);
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestHistorySnapshot?>(null);
    }

    private sealed class ThrowingHistoryRepository : IRequestHistoryRepository
    {
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage unavailable");
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage unavailable");
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage unavailable");
    }

    private sealed class RouteMethodComparer : IEqualityComparer<(string? RoutePattern, string[]? Methods)>
    {
        public static RouteMethodComparer Instance { get; } = new();

        public bool Equals((string? RoutePattern, string[]? Methods) left, (string? RoutePattern, string[]? Methods) right) =>
            string.Equals(left.RoutePattern, right.RoutePattern, StringComparison.Ordinal) &&
            (left.Methods ?? []).SequenceEqual(right.Methods ?? [], StringComparer.Ordinal);

        public int GetHashCode((string? RoutePattern, string[]? Methods) value) =>
            HashCode.Combine(value.RoutePattern, value.Methods is null ? 0 : StringComparer.Ordinal.GetHashCode(string.Join(',', value.Methods)));
    }

    private sealed class InMemoryHistoryRepository : IRequestHistoryRepository
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

    private sealed class InMemoryCollectionRepository : IRequestCollectionRepository
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

    private sealed class FailingCollectionRepository : IRequestCollectionRepository
    {
        public Task<IReadOnlyList<RequestCollectionSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database password: secret; Npgsql failure details");

        public Task<SavedRequestSnapshot> SaveAsync(SavedRequestSnapshot request, Guid? collectionId, string? newCollectionName,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("database password: secret; Npgsql failure details");
    }

    private sealed record TracerResponse(string Failure, int? StatusCode, double ElapsedMilliseconds,
        Dictionary<string, string[]> Headers, string Body, string? Error, bool RequestSent,
        bool HistorySaved);

    private sealed record ProblemResponse(string? Type, string? Title, int? Status, string? Detail);

    private sealed record HistoryDto(Guid Id, string Url, string[] RedactedQueryParameters);
    private sealed record SavedRequestDto(Guid Id, DateTimeOffset CreatedAt, Guid CollectionId, string CollectionName,
        string Title, string Method, string Url, HeaderDto[] Headers, string[] RedactedHeaders, string? Body);
    private sealed record HeaderDto(string Name, string Value, bool Redacted);
    private sealed record CollectionDto(Guid Id, string Name, SavedRequestDto[] Requests);
}

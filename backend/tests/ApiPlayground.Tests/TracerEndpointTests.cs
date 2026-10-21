using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class TracerEndpointTests
{
    private static HttpRequestMessage CreateAllowedRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tracer/get")
        {
            Content = JsonContent.Create(new { url }),
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
        }));
        await using (factory)
        using (var request = CreateAllowedRequest(target.BaseAddress.ToString()))
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
        }
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
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
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

    private sealed record TracerResponse(string Failure, int? StatusCode, string Body, bool PersistenceVerified, string? Error, bool RequestSent);
}

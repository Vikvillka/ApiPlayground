using System.Net;
using System.Net.Sockets;
using System.Text;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiPlayground.Tests.Outbound;

public sealed class OutboundSafetyTests
{
    [Theory]
    [InlineData("ftp://example.com/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://10.1.2.3/", false)]
    [InlineData("http://169.254.1.2/", false)]
    [InlineData("http://192.168.0.1/", false)]
    [InlineData("http://8.8.8.8/", true)]
    public void Uri_policy_rejects_disallowed_schemes_and_ip_literals(string value, bool expectedAllowed)
    {
        var target = new Uri(value);
        var policy = OutboundPolicy.PublicInternetOnly();

        var isAllowed = target.IsAbsoluteUri && target.Scheme is "http" or "https" &&
            IPAddress.TryParse(target.Host, out var address) && policy.AllowsAddress(target, address);

        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Theory]
    [InlineData("CONNECT")]
    [InlineData("TRACE")]
    [InlineData("OPTIONS")]
    [InlineData("get")]
    public async Task Unsupported_or_noncanonical_methods_are_rejected(string method)
    {
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.PublicInternetOnly(),
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            connector: (_, _) => Task.FromException<Stream>(new InvalidOperationException("Connection must not be attempted.")));
        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri("http://8.8.8.8/"), method));

        Assert.Equal(OutboundFailure.PolicyRejected, result.Failure);
    }

    [Fact]
    public async Task Malformed_content_type_and_headers_return_policy_rejection()
    {
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.PublicInternetOnly(),
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            connector: (_, _) => Task.FromException<Stream>(new InvalidOperationException("Connection must not be attempted.")));
        var uri = new Uri("http://8.8.8.8/");

        var badContentType = await executor.ExecuteAsync(new OutboundRequest(uri, "POST", ContentType: "not a valid content type"));
        var badHeader = await executor.ExecuteAsync(new OutboundRequest(uri, "GET", new Dictionary<string, string> { ["Bad Header"] = "x" }));

        Assert.Equal(OutboundFailure.PolicyRejected, badContentType.Failure);
        Assert.Equal(OutboundFailure.PolicyRejected, badHeader.Failure);
    }

    [Theory]
    [InlineData("0.0.0.0", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("172.31.0.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("198.19.0.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("192.0.0.9", false)]
    [InlineData("192.31.196.1", false)]
    [InlineData("192.52.193.1", false)]
    [InlineData("192.88.99.1", false)]
    [InlineData("192.175.48.1", false)]
    [InlineData("8.8.4.4", true)]
    [InlineData("2001::1", false)]
    [InlineData("2001:2::1", false)]
    [InlineData("2001:20::1", false)]
    [InlineData("2001:4:112::1", false)]
    [InlineData("2001:30::1", false)]
    [InlineData("2001:100::1", false)]
    [InlineData("2001:db8:1::1", false)]
    [InlineData("2001:4860::1", true)]
    [InlineData("2002::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("3fff::1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    public void Address_classifier_handles_special_use_ranges(string address, bool expected) =>
        Assert.Equal(expected, OutboundPolicy.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public async Task Validated_ipv6_dns_answer_is_selected_for_dns_hostname()
    {
        IPEndPoint? connectedEndpoint = null;
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.PublicInternetOnly(),
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("2606:4700:4700::1111") }),
            connector: (endpoint, _) =>
            {
                connectedEndpoint = endpoint;
                throw new HttpRequestException("Connection intentionally stopped by test.");
            });

        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri("http://ipv6-only.example.test/"), "GET"));

        Assert.Equal(OutboundFailure.TransportFailure, result.Failure);
        Assert.True(result.DispatchStarted);
        Assert.Equal(AddressFamily.InterNetworkV6, connectedEndpoint?.AddressFamily);
        Assert.Equal(IPAddress.Parse("2606:4700:4700::1111"), connectedEndpoint?.Address);
    }

    [Fact]
    public async Task Null_uri_is_rejected_without_throwing()
    {
        var executor = new GuardedTargetHttpClient(OutboundPolicy.PublicInternetOnly());
        var result = await executor.ExecuteAsync(new OutboundRequest(null!, "GET"));

        Assert.Equal(OutboundFailure.PolicyRejected, result.Failure);
    }

    [Fact]
    public async Task Connect_deadline_returns_timeout_when_connection_does_not_complete()
    {
        await using var server = await ControlledLoopbackServer.StartAsync();
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress),
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback }),
            connector: async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unreachable after cancellation.");
            });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri(server.BaseAddress, "/connect-timeout"), "GET"));

        Assert.Equal(OutboundFailure.Timeout, result.Failure);
        Assert.True(result.DispatchStarted);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task Explicit_test_policy_allows_only_the_exact_loopback_endpoint_through_guard()
    {
        await using var server = await ControlledLoopbackServer.StartAsync();
        var executor = CreateTestExecutor(OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress, "echo.example.test"), server.BaseAddress);
        var targetUri = new Uri($"http://echo.example.test:{server.BaseAddress.Port}/echo");
        var result = await executor.ExecuteAsync(new OutboundRequest(targetUri, "GET"));

        Assert.True(result.Succeeded, result.Diagnostic);
        Assert.True(result.DispatchStarted);
        Assert.Equal("controlled", Encoding.UTF8.GetString(result.Body.Span));
        var wrongPort = await executor.ExecuteAsync(new OutboundRequest(new Uri($"http://echo.example.test:{server.BaseAddress.Port + 1}/"), "GET"));
        Assert.Equal(OutboundFailure.PolicyRejected, wrongPort.Failure);
    }

    [Fact]
    public async Task All_dns_answers_are_validated_and_first_approved_address_is_pinned()
    {
        await using var server = await ControlledLoopbackServer.StartAsync();
        var target = new Uri($"http://target.example.test:{server.BaseAddress.Port}/");
        var policy = OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress, "target.example.test");
        var dns = new GuardedTargetHttpClient(policy, (_, _) => Task.FromResult(new[] { IPAddress.Loopback, IPAddress.Parse("8.8.8.8") }));
        var mixed = await dns.ExecuteAsync(new OutboundRequest(target, "GET"));
        Assert.Equal(OutboundFailure.PolicyRejected, mixed.Failure);

        var pinned = new GuardedTargetHttpClient(policy, (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));
        var response = await pinned.ExecuteAsync(new OutboundRequest(target, "GET"));
        Assert.True(response.Succeeded, response.Diagnostic);
    }

    [Fact]
    public async Task Redirect_is_returned_without_following_second_location()
    {
        var secondRequests = 0;
        await using var second = await ControlledLoopbackServer.StartAsync(_ =>
        {
            Interlocked.Increment(ref secondRequests);
            return Task.CompletedTask;
        });
        await using var first = await ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Redirect;
            context.Response.Headers.Location = new Uri(second.BaseAddress, "next").ToString();
            await context.Response.CompleteAsync();
        });
        var executor = new GuardedTargetHttpClient(OutboundPolicy.ForTestOnlyLoopbackEndpoint(first.BaseAddress));
        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri(first.BaseAddress, "redirect"), "GET"));

        Assert.Equal(302, result.StatusCode);
        Assert.Equal(0, secondRequests);
    }

    [Fact]
    public async Task Streamed_response_is_capped_at_one_mibibyte()
    {
        await using var server = await ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.ContentLength = null;
            var chunk = new byte[32 * 1024];
            for (var index = 0; index < 40 && !context.RequestAborted.IsCancellationRequested; index++)
                await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
        });
        var executor = CreateTestExecutor(OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress, "large.example.test"), server.BaseAddress);
        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri($"http://large.example.test:{server.BaseAddress.Port}/large"), "GET"));

        Assert.Equal(OutboundFailure.ResponseTooLarge, result.Failure);
        Assert.True(result.DispatchStarted);
    }

    [Fact]
    public async Task Total_deadline_covers_streaming_response_reads()
    {
        await using var server = await ControlledLoopbackServer.StartAsync(async context =>
        {
            await context.Response.Body.WriteAsync("partial"u8.ToArray(), context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(TimeSpan.FromSeconds(35), context.RequestAborted);
        });
        var executor = CreateTestExecutor(OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress, "slow.example.test"), server.BaseAddress);
        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri($"http://slow.example.test:{server.BaseAddress.Port}/slow"), "GET"));

        Assert.Equal(OutboundFailure.Timeout, result.Failure);
        Assert.True(result.DispatchStarted);
    }

    [Fact]
    public void Missing_policy_fails_closed_and_no_default_executor_is_registered()
    {
        Assert.Throws<ArgumentNullException>(() => new GuardedTargetHttpClient(null!));
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddGuardedOutbound(null!));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IOutboundRequestExecutor));
    }

    [Fact]
    public async Task Caller_cancellation_during_body_read_is_reported_as_cancelled()
    {
        await using var server = await ControlledLoopbackServer.StartAsync(async context =>
        {
            await context.Response.Body.WriteAsync("partial"u8.ToArray(), context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var executor = CreateTestExecutor(OutboundPolicy.ForTestOnlyLoopbackEndpoint(server.BaseAddress, "cancel.example.test"), server.BaseAddress);
        var result = await executor.ExecuteAsync(new OutboundRequest(new Uri($"http://cancel.example.test:{server.BaseAddress.Port}/cancel"), "GET"), cancellation.Token);

        Assert.Equal(OutboundFailure.Cancelled, result.Failure);
        Assert.True(result.DispatchStarted);
    }

    [Fact]
    public void Test_policy_rejects_wildcard_or_non_ephemeral_shapes()
    {
        Assert.Throws<ArgumentException>(() => OutboundPolicy.ForTestOnlyLoopbackEndpoint(new Uri("http://localhost:80/")));
        Assert.Throws<ArgumentException>(() => OutboundPolicy.ForTestOnlyLoopbackEndpoint(new Uri("http://0.0.0.0:12345/")));
        Assert.Throws<ArgumentException>(() => OutboundPolicy.ForTestOnlyLoopbackEndpoint(new Uri("http://127.0.0.1:8080/")));
        Assert.Throws<ArgumentException>(() => OutboundPolicy.ForTestOnlyLoopbackEndpoint(new Uri("http://127.0.0.1:50000/other")));
    }

    private static GuardedTargetHttpClient CreateTestExecutor(OutboundPolicy policy, Uri endpoint)
    {
        var address = IPAddress.Parse(endpoint.Host);
        return new GuardedTargetHttpClient(policy, (_, _) => Task.FromResult(new[] { address }));
    }

}

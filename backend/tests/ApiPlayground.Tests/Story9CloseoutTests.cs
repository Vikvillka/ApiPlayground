using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using ApiPlayground.Tests.Outbound;

namespace ApiPlayground.Tests;

public sealed class Story9CloseoutTests
{
    [Fact]
    public async Task Allowed_loopback_request_is_observed_and_saved_by_the_real_api_host()
    {
        await using var target = await Outbound.ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Story9"] = "owned-loopback";
            await context.Response.WriteAsync("{\"ok\":true,\"marker\":\"story9\"}");
        });
        var history = new RecordingHistoryStore();
        await using var factory = CreateApiFactory(target.BaseAddress, history);
        using var request = CreateTracerRequest(target.BaseAddress.ToString());
        request.Content = JsonContent.Create(new { url = target.BaseAddress.ToString(), method = "GET" });

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<TracerPayload>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(200, payload.StatusCode);
        Assert.Equal("application/json", payload.Headers["Content-Type"].Single().Split(';')[0]);
        Assert.Equal("{\"ok\":true,\"marker\":\"story9\"}", payload.Body);
        Assert.Equal("GET", Assert.Single(history.Items).Method);
        Assert.Equal(target.BaseAddress.ToString(), history.Items[0].Url);
        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public async Task Policy_rejection_does_not_connect_or_change_history()
    {
        await using var target = await Outbound.ControlledLoopbackServer.StartAsync();
        var history = new RecordingHistoryStore();
        await using var factory = CreateProductionApiFactory(history);
        using var request = CreateTracerRequest(target.BaseAddress.ToString());
        request.Content = JsonContent.Create(new { url = target.BaseAddress.ToString(), method = "GET" });

        using var response = await factory.CreateClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<TracerPayload>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload.RequestSent);
        Assert.Equal("PolicyRejected", payload.Failure);
        Assert.Empty(history.Items);
        Assert.Equal(0, target.HitCount);
    }

    [Fact]
    public async Task Mixed_dns_answers_are_rejected_before_loopback_dispatch()
    {
        await using var target = await ControlledLoopbackServer.StartAsync();
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.ForTestOnlyLoopbackEndpoint(target.BaseAddress, "story9.example.test"),
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback, IPAddress.Parse("8.8.8.8") }));

        var result = await executor.ExecuteAsync(new OutboundRequest(
            new Uri($"http://story9.example.test:{target.BaseAddress.Port}/"), "GET"));

        Assert.Equal(OutboundFailure.PolicyRejected, result.Failure);
        Assert.False(result.DispatchStarted);
        Assert.Equal(0, target.HitCount);
    }

    [Fact]
    public async Task Dns_answer_change_after_validation_does_not_change_the_pinned_connection_target()
    {
        var validatedAddress = IPAddress.Parse("8.8.8.8");
        var changedAddress = IPAddress.Loopback;
        var currentAnswers = new[] { validatedAddress };
        var lookupCount = 0;
        IPEndPoint? connectedEndpoint = null;
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.PublicInternetOnly(),
            (_, _) =>
            {
                lookupCount++;
                return Task.FromResult(currentAnswers);
            },
            connector: (endpoint, _) =>
            {
                currentAnswers = [changedAddress];
                connectedEndpoint = endpoint;
                return Task.FromException<Stream>(new HttpRequestException("Connection intentionally stopped by test."));
            });

        var result = await executor.ExecuteAsync(new OutboundRequest(
            new Uri("http://rebind.example.test/"), "GET"));

        Assert.Equal(OutboundFailure.TransportFailure, result.Failure);
        Assert.Equal(1, lookupCount);
        Assert.Equal(validatedAddress, connectedEndpoint?.Address);
        Assert.NotEqual(changedAddress, connectedEndpoint?.Address);
    }

    [Fact]
    public async Task Redirect_is_not_followed_to_a_second_target()
    {
        await using var second = await ControlledLoopbackServer.StartAsync();
        await using var first = await ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Redirect;
            context.Response.Headers.Location = second.BaseAddress.ToString();
            await context.Response.CompleteAsync();
        });
        var executor = new GuardedTargetHttpClient(OutboundPolicy.ForTestOnlyLoopbackEndpoint(first.BaseAddress));

        var result = await executor.ExecuteAsync(new OutboundRequest(first.BaseAddress, "GET"));

        Assert.Equal((int)HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal(1, first.HitCount);
        Assert.Equal(0, second.HitCount);
    }

    [Fact]
    public async Task Connect_timeout_enforces_the_five_second_limit()
    {
        await using var target = await ControlledLoopbackServer.StartAsync();
        var executor = new GuardedTargetHttpClient(
            OutboundPolicy.ForTestOnlyLoopbackEndpoint(target.BaseAddress),
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback }),
            connector: async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unreachable after timeout.");
            });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = await executor.ExecuteAsync(new OutboundRequest(target.BaseAddress, "GET"));

        Assert.Equal(OutboundFailure.Timeout, result.Failure);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task Total_deadline_includes_response_body_reads()
    {
        await using var target = await ControlledLoopbackServer.StartAsync(async context =>
        {
            await context.Response.Body.WriteAsync("partial"u8.ToArray(), context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(TimeSpan.FromSeconds(35), context.RequestAborted);
        });
        var executor = new GuardedTargetHttpClient(OutboundPolicy.ForTestOnlyLoopbackEndpoint(target.BaseAddress));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = await executor.ExecuteAsync(new OutboundRequest(target.BaseAddress, "GET"));

        Assert.Equal(OutboundFailure.Timeout, result.Failure);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(28), TimeSpan.FromSeconds(34));
    }

    [Fact]
    public async Task Streamed_response_is_rejected_at_the_one_mibibyte_cap()
    {
        await using var target = await ControlledLoopbackServer.StartAsync(async context =>
        {
            context.Response.ContentLength = null;
            var chunk = new byte[32 * 1024];
            for (var index = 0; index < 40 && !context.RequestAborted.IsCancellationRequested; index++)
                await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
        });
        var executor = new GuardedTargetHttpClient(OutboundPolicy.ForTestOnlyLoopbackEndpoint(target.BaseAddress));

        var result = await executor.ExecuteAsync(new OutboundRequest(target.BaseAddress, "GET"));

        Assert.Equal(OutboundFailure.ResponseTooLarge, result.Failure);
        Assert.True(result.DispatchStarted);
        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public async Task Disposable_database_role_is_restricted_and_migrations_use_app_credentials()
    {
        var adminConnection = Story9DisposableDatabase.GetAdminConnectionString()
            ?? throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION is required for Story 9 closeout; tests never skip.");
        await using var database = await Story9DisposableDatabase.CreateAsync(adminConnection);
        var options = new DbContextOptionsBuilder<ApiPlayground.Api.Data.PlatformDbContext>()
            .UseNpgsql(database.AppConnectionString, postgres => postgres.MigrationsAssembly("ApiPlayground.Infrastructure"))
            .Options;
        await using (var context = new ApiPlayground.Api.Data.PlatformDbContext(options))
            await context.Database.MigrateAsync();

        await using var historyContext = new ApiPlayground.Api.Data.PlatformDbContext(options);
        var historyStore = new ApiPlayground.Infrastructure.Outbound.PostgresRequestHistoryStore(historyContext);
        var saved = RequestHistoryRedaction.Create(
            new OutboundRequest(new Uri("http://127.0.0.1:49152/persisted"), "GET"),
            new OutboundResult(OutboundFailure.None, 200, TimeSpan.FromMilliseconds(1),
                new Dictionary<string, IReadOnlyList<string>>(), "ok"u8.ToArray(), DispatchStarted: true));
        await historyStore.SaveAsync(saved);

        await using var verificationContext = new ApiPlayground.Api.Data.PlatformDbContext(options);
        var listed = await new ApiPlayground.Infrastructure.Outbound.PostgresRequestHistoryStore(verificationContext).ListAsync();
        Assert.Equal(saved.Id, Assert.Single(listed).Id);
        var appConnection = new NpgsqlConnectionStringBuilder(database.AppConnectionString);
        Assert.Equal(database.DatabaseName, appConnection.Database);
        Assert.Equal(database.RoleName, appConnection.Username);
    }

    [Fact]
    public async Task Registered_production_policy_is_fixed_public_only_and_has_one_guarded_executor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());

        using var provider = services.BuildServiceProvider();
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOutboundRequestExecutor));
        Assert.IsType<GuardedTargetHttpClient>(provider.GetRequiredService<IOutboundRequestExecutor>());
        var policy = provider.GetRequiredService<OutboundPolicy>();
        Assert.All(new[] { "http://127.0.0.1/", "http://10.0.0.1/", "ftp://8.8.8.8/" }, value =>
        {
            var uri = new Uri(value);
            Assert.False(uri.Scheme is "http" or "https" && IPAddress.TryParse(uri.Host, out var address) && policy.AllowsAddress(uri, address));
        });

        var program = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "backend", "src", "ApiPlayground.Api", "Program.cs"));
        Assert.Contains("AddGuardedOutbound(OutboundPolicy.PublicInternetOnly())", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ForTestOnlyLoopbackEndpoint", program, StringComparison.Ordinal);
        Assert.DoesNotContain("OutboundPolicy__", program, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateApiFactory(Uri target, RecordingHistoryStore history) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOutboundRequestExecutor>();
            services.AddSingleton<IOutboundRequestExecutor>(new GuardedTargetHttpClient(
                OutboundPolicy.ForTestOnlyLoopbackEndpoint(target),
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback })));
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(history);
        }));

    private static WebApplicationFactory<Program> CreateProductionApiFactory(RecordingHistoryStore history) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITracerPersistenceSmokeCheck>();
            services.AddSingleton<ITracerPersistenceSmokeCheck>(new SuccessfulPersistenceCheck());
            services.RemoveAll<IRequestHistoryStore>();
            services.AddSingleton<IRequestHistoryStore>(history);
        }));

    private static HttpRequestMessage CreateTracerRequest(string url)
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class RecordingHistoryStore : IRequestHistoryStore
    {
        public List<RequestHistorySnapshot> Items { get; } = [];
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Items.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>(Items.ToArray());
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
    }

    private sealed class SuccessfulPersistenceCheck : ITracerPersistenceSmokeCheck
    {
        public Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed record TracerPayload(string Failure, int? StatusCode, string Body,
        Dictionary<string, string[]> Headers, bool RequestSent);
}
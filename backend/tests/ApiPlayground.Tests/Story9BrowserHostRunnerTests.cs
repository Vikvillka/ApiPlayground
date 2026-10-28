using System.Net;
using System.Text.Json;
using ApiPlayground.Api;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class Story9BrowserHostRunnerTests
{
    [Fact]
    public async Task Runs_real_loopback_api_and_owned_fixture_until_parent_requests_cleanup()
    {
        var runnerDirectory = Environment.GetEnvironmentVariable("APG_STORY9_RUNNER_DIR");
        if (string.IsNullOrWhiteSpace(runnerDirectory))
            return;
        Directory.CreateDirectory(runnerDirectory!);
        var adminConnection = Story9DisposableDatabase.GetAdminConnectionString()
            ?? throw new InvalidOperationException("APIPLAYGROUND_TEST_ADMIN_CONNECTION is required for the Story 9 browser runner; tests never skip.");
        try
        {
            await using var database = await Story9DisposableDatabase.CreateAsync(adminConnection);
            Environment.SetEnvironmentVariable("APIPLAYGROUND_TEST_ADMIN_CONNECTION", null);

            var targetHitsPath = Path.Combine(runnerDirectory!, "target-hits.txt");
            var target = await Story9BrowserTarget.StartAsync(targetHitsPath);
            await using (target)
            await using (var apiHost = new Story9BrowserApiHost(database.AppConnectionString, target.BaseAddress))
            {
                await apiHost.StartAsync();
                await File.WriteAllTextAsync(Path.Combine(runnerDirectory!, "ready.json"),
                    JsonSerializer.Serialize(new { targetUrl = target.BaseAddress.ToString() }));
                await File.WriteAllTextAsync(targetHitsPath, "0");

                while (!File.Exists(Path.Combine(runnerDirectory!, "shutdown")))
                {
                    var restartRequest = Path.Combine(runnerDirectory!, "restart-request");
                    if (File.Exists(restartRequest))
                    {
                        File.Delete(restartRequest);
                        await apiHost.RestartAsync();
                        await File.WriteAllTextAsync(Path.Combine(runnerDirectory!, "restart-complete"), Guid.NewGuid().ToString("N"));
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }
        }
        catch
        {
            await File.WriteAllTextAsync(Path.Combine(runnerDirectory!, "shutdown"), "failed");
            throw;
        }
    }
}

internal sealed class Story9BrowserApiHost(string appConnectionString, Uri targetAddress) : IAsyncDisposable
{
    private WebApplication? _app;

    public async Task StartAsync() => await StartCoreAsync();

    public async Task RestartAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
        await StartCoreAsync();
    }

    private async Task StartCoreAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ApplicationName = typeof(Program).Assembly.FullName,
        });
        builder.WebHost.UseKestrel(options => options.ListenLocalhost(5000));
        builder.Services.AddInfrastructure(appConnectionString);
        builder.Services.AddGuardedOutbound(OutboundPolicy.ForTestOnlyLoopbackEndpoint(targetAddress));
        builder.Services.AddScoped<ITracerPersistenceSmokeCheck, PostgresTracerPersistenceSmokeCheck>();
        builder.Services.AddControllers();
        builder.Services.AddScoped<GetTracerUseCase>();
        builder.Services.AddScoped<RequestHistoryUseCase>();
        builder.Services.AddScoped<SaveRequestUseCase>();
        var app = builder.Build();
        app.UseRequestGuard();
        app.MapTracerEndpoints();
        app.MapControllers();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApiPlayground.Api.Data.PlatformDbContext>();
            await database.Database.MigrateAsync();
        }

        await app.StartAsync();
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}

internal sealed class Story9BrowserTarget : IAsyncDisposable
{
    private readonly Microsoft.AspNetCore.Builder.WebApplication _app;

    private Story9BrowserTarget(Microsoft.AspNetCore.Builder.WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public static async Task<Story9BrowserTarget> StartAsync(string hitsPath)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        app.Run(async context =>
        {
            var count = int.TryParse(await File.ReadAllTextAsync(hitsPath), out var current) ? current : 0;
            await File.WriteAllTextAsync(hitsPath, checked(count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(hitsPath)!, "last-request.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                method = context.Request.Method,
                url = context.Request.GetDisplayUrl(),
                header = context.Request.Headers["X-Story9-Request"].ToString(),
                body = await new StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted),
            }));
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Fixture"] = "story9-loopback";
            await context.Response.WriteAsync("{\"ok\":true,\"marker\":\"story9\"}", context.RequestAborted);
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features
            .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
            .Addresses.Single(value => value.StartsWith("http://", StringComparison.Ordinal));
        return new Story9BrowserTarget(app, new Uri(address));
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
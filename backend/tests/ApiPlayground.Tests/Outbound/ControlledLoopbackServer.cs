using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace ApiPlayground.Tests.Outbound;

internal sealed class ControlledLoopbackServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly HitCounter _hitCounter;
    private ControlledLoopbackServer(WebApplication application, Uri baseAddress, HitCounter hitCounter)
    {
        _application = application;
        BaseAddress = baseAddress;
        _hitCounter = hitCounter;
    }

    public Uri BaseAddress { get; }
    public int HitCount => Volatile.Read(ref _hitCounter.Value);

    public static async Task<ControlledLoopbackServer> StartAsync(Func<HttpContext, Task> handler)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        var hitCounter = new HitCounter();
        app.Run(context =>
        {
            Interlocked.Increment(ref hitCounter.Value);
            return handler(context);
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single(value => value.StartsWith("http://", StringComparison.Ordinal));
        return new ControlledLoopbackServer(app, new Uri(address), hitCounter);
    }

    public static async Task<ControlledLoopbackServer> StartAsync() =>
        await StartAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return context.Response.WriteAsync("controlled", context.RequestAborted);
        });

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    private sealed class HitCounter
    {
        public int Value;
    }
}

using ApiPlayground.Api;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddNLog(CreateNLogConfiguration());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());
builder.Services.AddTracerPersistenceSmokeCheck();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddScoped<GetTracerUseCase>();
builder.Services.AddScoped<RequestHistoryUseCase>();
builder.Services.AddScoped<SaveRequestUseCase>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseRequestGuard();
app.MapTracerEndpoints();
app.MapControllers();
app.Run();

// Code-only NLog configuration: one console target with an explicit JSON attribute allowlist.
// Request headers, query strings, and bodies are never rendered, and exceptions are rendered as type names only.
static LoggingConfiguration CreateNLogConfiguration()
{
    var layout = new JsonLayout { SuppressSpaces = true };
    layout.Attributes.Add(new JsonAttribute("time", "${longdate}"));
    layout.Attributes.Add(new JsonAttribute("level", "${level:uppercase=true}"));
    layout.Attributes.Add(new JsonAttribute("logger", "${logger}"));
    layout.Attributes.Add(new JsonAttribute("message", "${message}"));
    layout.Attributes.Add(new JsonAttribute("exception", "${exception:format=Type}"));

    var console = new ConsoleTarget("console") { Layout = layout };
    var configuration = new LoggingConfiguration();
    configuration.AddTarget(console);
    configuration.LoggingRules.Add(new LoggingRule("Microsoft.Hosting.Lifetime", NLog.LogLevel.Info, NLog.LogLevel.Fatal, console) { Final = true });
    configuration.LoggingRules.Add(new LoggingRule("Microsoft.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
    configuration.LoggingRules.Add(new LoggingRule("System.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
    configuration.LoggingRules.Add(new LoggingRule("Npgsql.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
    configuration.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, NLog.LogLevel.Fatal, console));
    return configuration;
}

using ApiPlayground.Api;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddNLogConsole();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());
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


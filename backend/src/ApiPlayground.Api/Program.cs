using ApiPlayground.Api;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());
builder.Services.AddTracerPersistenceSmokeCheck();
builder.Services.AddControllers();
builder.Services.AddScoped<GetTracerUseCase>();
builder.Services.AddScoped<RequestHistoryUseCase>();
builder.Services.AddScoped<SaveRequestUseCase>();

var app = builder.Build();
app.UseRequestGuard();
app.MapTracerEndpoints();
app.MapControllers();
app.Run();

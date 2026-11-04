using ApiPlayground.Api;
using ApiPlayground.Application.Auth;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddNLogConsole();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
// Default-deny: every endpoint needs an authenticated user unless it opts out with AllowAnonymous.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers();
builder.Services.AddScoped<GetTracerUseCase>();
builder.Services.AddScoped<RequestHistoryUseCase>();
builder.Services.AddScoped<SaveRequestUseCase>();
builder.Services.AddScoped<RegisterAccountUseCase>();
builder.Services.AddScoped<SignInUseCase>();
builder.Services.AddScoped<SignOutUseCase>();
builder.Services.AddScoped<CurrentUserUseCase>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseRequestGuard();
app.UseAuthentication();
app.UseAuthorization();
app.MapTracerEndpoints();
app.MapControllers();
app.Run();


using ApiPlayground.Api.Data;
using ApiPlayground.Application.Auth;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ApiPlayground.Infrastructure.Outbound;

namespace ApiPlayground.Infrastructure;

public static class DependencyInjection
{
    // Owns all persistence registration. The connection string is validated at host start, so a missing
    // value stops startup before any request is served.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PostgresOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("Postgres") ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), PostgresOptions.ValidationMessage)
            .ValidateOnStart();

        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            var postgres = provider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            options.UseNpgsql(postgres.ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName));
        });
        services.AddScoped<IRequestHistoryRepository, PostgresRequestHistoryRepository>();
        services.AddScoped<IRequestCollectionRepository, PostgresRequestCollectionRepository>();

        AddIdentitySession(services);

        return services;
    }

    // Identity stays in Infrastructure. Users are keyed by Guid and there are no roles. The session is an httpOnly,
    // SameSite=Strict cookie with about 8 hours of sliding lifetime. Anonymous requests get ProblemDetails, not redirects.
    private static void AddIdentitySession(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.AllowedForNewUsers = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<PlatformDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        // Validate the security stamp on every request so a sign-out rotates it and invalidates copies of the cookie.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "ApiPlayground.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            options.Events.OnRedirectToLogin = context =>
                WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Authentication is required.");
            options.Events.OnRedirectToAccessDenied = context =>
                WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Access is denied.");
        });

        services.AddScoped<IAccountPort, IdentityAccountPort>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title).ExecuteAsync(context);

    public static IServiceCollection AddGuardedOutbound(this IServiceCollection services, OutboundPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.IsValid)
            throw new InvalidOperationException("The outbound policy is invalid.");
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IOutboundRequestExecutor)))
            throw new InvalidOperationException("Exactly one guarded outbound executor may be registered.");

        services.AddSingleton(policy);
        services.AddSingleton<IOutboundRequestExecutor>(provider =>
        {
            var configuredPolicy = provider.GetRequiredService<OutboundPolicy>();
            if (!configuredPolicy.IsValid)
                throw new InvalidOperationException("The outbound policy is invalid.");
            return new GuardedTargetHttpClient(configuredPolicy);
        });
        return services;
    }
}

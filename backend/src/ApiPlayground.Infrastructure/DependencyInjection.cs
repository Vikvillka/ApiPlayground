using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
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

        return services;
    }

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

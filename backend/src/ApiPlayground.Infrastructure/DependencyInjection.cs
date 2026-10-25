using ApiPlayground.Api.Data;
using ApiPlayground.Application.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ApiPlayground.Infrastructure.Outbound;

namespace ApiPlayground.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)));
        services.AddScoped<IRequestHistoryStore, PostgresRequestHistoryStore>();
        services.AddScoped<IRequestCollectionStore, PostgresRequestCollectionStore>();

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

    public static IServiceCollection AddTracerPersistenceSmokeCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ITracerPersistenceSmokeCheck, PostgresTracerPersistenceSmokeCheck>();
        return services;
    }
}

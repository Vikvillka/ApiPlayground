using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlayground.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)));

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SerilogLevelApi.MySql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// adds both the persistent store of level overrides along with the monitor service that polls the overrides and applies them
    /// </summary>
    public static IServiceCollection AddMySqlLogLevelOverrides<TDbContext>(
        this IServiceCollection services,
        string connectionString)
        where TDbContext : DbContext, ILogOverridesTable
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextFactory<TDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)));

        services.TryAddSingleton<ILogLevelOverrides, MySqlLogLevelOverrides<TDbContext>>();
        services.TryAddSingleton<SerilogLevelMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<SerilogLevelMonitor>());

        return services;
    }
}

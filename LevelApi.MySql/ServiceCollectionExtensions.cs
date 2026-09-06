using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;

namespace SerilogLevelApi.MySql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// adds both the persistent store of level overrides along with the monitor service that polls the overrides and applies them
    /// </summary>
    public static IServiceCollection AddMySqlLogLevelOverrides<TDbContext>(
        this IServiceCollection services,
        string connectionString,
        LoggingLevelSwitch levelSwitch)
        where TDbContext : DbContext, ILogOverridesTable
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(levelSwitch);        

        services.AddDbContextFactory<TDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)), ServiceLifetime.Singleton);

        services.TryAddSingleton(levelSwitch);
        services.TryAddSingleton<ILogLevelOverrides, MySqlLogLevelOverrides<TDbContext>>();
        services.TryAddSingleton<SerilogLevelMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<SerilogLevelMonitor>());

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace SerilogLevelApi.MySql;

public static class ServiceCollectionExtensions
{
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

        return services;
    }
}

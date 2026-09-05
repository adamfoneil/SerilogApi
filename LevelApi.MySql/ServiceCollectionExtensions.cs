using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SerilogLevelApi.MySql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMySqlLogLevelOverrides(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<ILogLevelOverrides>(
            _ => new MySqlLogLevelOverrides(connectionString));

        return services;
    }
}

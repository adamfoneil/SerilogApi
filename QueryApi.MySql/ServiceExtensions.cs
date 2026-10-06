using Microsoft.Extensions.DependencyInjection;
using SerilogQueryApi;

namespace QueryApi.MySql;

public static class ServiceExtensions
{
    public static void AddSerilogQuery(this IServiceCollection services, string connectionString, TableConfiguration tableConfiguration)
    {
        services.AddSingleton(tableConfiguration);
        services.AddSingleton<ILogQuery, MySqlLogQuery>(sp => new MySqlLogQuery(connectionString, tableConfiguration));
    }
}

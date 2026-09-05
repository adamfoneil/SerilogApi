using Testcontainers.MySql;

namespace DemoApi;

public sealed class DemoDatabaseConnection(string connectionString, MySqlContainer? container) : IAsyncDisposable
{
    private bool _disposed;

    public string ConnectionString { get; } = connectionString;
    public bool IsDisposable => container is not null;

    public static async Task<DemoDatabaseConnection> CreateAsync(IConfiguration configuration)
    {
        var configuredConnection = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return new DemoDatabaseConnection(configuredConnection, null);
        }

        var image = configuration["DemoApi:MySqlImage"] ?? "mysql:8.4";

        var container = new MySqlBuilder(image)
            .WithDatabase("serilogdemo")
            .WithUsername("mysql")
            .WithPassword("mysql")
            .Build();

        await container.StartAsync();

        return new DemoDatabaseConnection(container.GetConnectionString(), container);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}

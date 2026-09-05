using MySqlConnector;
using Serilog.Events;

namespace SerilogLevelApi.MySql;

public sealed class MySqlLogLevelOverrides : ILogLevelOverrides
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public MySqlLogLevelOverrides(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS serilog_level_overrides (
                    category VARCHAR(255) NOT NULL,
                    level VARCHAR(32) NOT NULL,
                    expires_utc DATETIME(6) NULL,
                    PRIMARY KEY (category)
                );
                """;

            await command.ExecuteNonQueryAsync();
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<Dictionary<string, LogLevelOverride>> GetAsync()
    {
        await InitializeAsync();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT category, level, expires_utc
            FROM serilog_level_overrides;
            """;

        var overrides = new Dictionary<string, LogLevelOverride>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var category = reader.GetString(0);
            var levelName = reader.GetString(1);
            if (!Enum.TryParse<LogEventLevel>(levelName, ignoreCase: true, out var level) ||
                !Enum.IsDefined(level))
            {
                throw new InvalidOperationException(
                    $"The stored log level '{levelName}' for category '{category}' is invalid.");
            }

            DateTime? expiresUtc = reader.IsDBNull(2)
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);

            overrides.Add(category, new LogLevelOverride(level, expiresUtc));
        }

        return overrides;
    }

    public async Task SetAsync(
        string category,
        LogEventLevel level,
        TimeSpan? expiresAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        await InitializeAsync();

        var expiresUtc = expiresAfter.HasValue
            ? DateTime.UtcNow.Add(expiresAfter.Value)
            : (DateTime?)null;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO serilog_level_overrides (category, level, expires_utc)
            VALUES (@category, @level, @expiresUtc)
            ON DUPLICATE KEY UPDATE
                level = @level,
                expires_utc = @expiresUtc;
            """;
        command.Parameters.AddWithValue("@category", category);
        command.Parameters.AddWithValue("@level", level.ToString());
        command.Parameters.AddWithValue("@expiresUtc", expiresUtc);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveAsync(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        await InitializeAsync();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM serilog_level_overrides
            WHERE category = @category;
            """;
        command.Parameters.AddWithValue("@category", category);

        await command.ExecuteNonQueryAsync();
    }
}

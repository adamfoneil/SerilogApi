using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace SerilogLevelApi.MySql;

public sealed class MySqlLogLevelOverrides<TDbContext>(
    ILogger<MySqlLogLevelOverrides<TDbContext>> logger,
    IDbContextFactory<TDbContext> dbFactory) : ILogLevelOverrides
    where TDbContext : DbContext, ILogOverridesTable
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly ILogger<MySqlLogLevelOverrides<TDbContext>> _logger = logger;
    private readonly IDbContextFactory<TDbContext> _dbFactory = dbFactory;
    private bool _initialized;

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

            await using var db = await _dbFactory.CreateDbContextAsync();
            await db.EnsureLogOverridesTableExistsAsync();
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

        await using var db = await _dbFactory.CreateDbContextAsync();
        var overrides = new Dictionary<string, LogLevelOverride>(StringComparer.Ordinal);

        await foreach (var logOverride in db.LogOverrides.AsNoTracking().AsAsyncEnumerable())
        {
            if (!Enum.TryParse<LogEventLevel>(logOverride.Level, ignoreCase: true, out var level) ||
                !Enum.IsDefined(level))
            {
                throw new InvalidOperationException(
                    $"The stored log level '{logOverride.Level}' for category '{logOverride.Category}' is invalid.");
            }

            overrides.Add(logOverride.Category, new LogLevelOverride(level, logOverride.ExpiresUtc));
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

        using var db = _dbFactory.CreateDbContext();
        var logOverride = await db.LogOverrides.SingleOrDefaultAsync(x => x.Category == category);

        if (logOverride is null)
        {
            db.LogOverrides.Add(new LogOverride
            {
                Category = category,
                Level = level.ToString(),
                ExpiresUtc = expiresUtc
            });
        }
        else
        {
            logOverride.Level = level.ToString();
            logOverride.ExpiresUtc = expiresUtc;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Set level override {category} to {level} in database", category, level);
    }

    public async Task RemoveAsync(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        await InitializeAsync();

        using var db = _dbFactory.CreateDbContext();
        var logOverride = await db.LogOverrides.SingleOrDefaultAsync(x => x.Category == category);

        if (logOverride is not null)
        {
            db.LogOverrides.Remove(logOverride);
            await db.SaveChangesAsync();
            _logger.LogInformation("Removed log level override {category} from database", category);
        }
    }

    public async Task ClearAllAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        await db.LogOverrides.ExecuteDeleteAsync();
        _logger.LogInformation("Removed all log level overrides from database");        
    }

    public async Task RemoveTemporaryAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        await db.LogOverrides.Where(row => row.ExpiresUtc.HasValue).ExecuteDeleteAsync();
        _logger.LogInformation("Removed temporary level overrides from database");
    }
}

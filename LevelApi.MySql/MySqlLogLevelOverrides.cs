using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace SerilogLevelApi.MySql;

public sealed class MySqlLogLevelOverrides<TDbContext>(IDbContextFactory<TDbContext> dbFactory) : ILogLevelOverrides
    where TDbContext : DbContext, ILogOverridesTable
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
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
            await db.Database.EnsureCreatedAsync();
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

        await using var db = await _dbFactory.CreateDbContextAsync();
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
    }

    public async Task RemoveAsync(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        await InitializeAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var logOverride = await db.LogOverrides.SingleOrDefaultAsync(x => x.Category == category);

        if (logOverride is not null)
        {
            db.LogOverrides.Remove(logOverride);
            await db.SaveChangesAsync();
        }
    }
}

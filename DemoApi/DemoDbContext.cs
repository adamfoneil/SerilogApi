using Microsoft.EntityFrameworkCore;
using QueryApi.MySql;
using SerilogLevelApi.MySql;
using SerilogQueryApi;

namespace DemoApi;

public sealed class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options), ILogOverridesTable
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<LogOverride> LogOverrides { get; set; }

    public Task EnsureLogOverridesTableExistsAsync() =>
        Database.ExecuteSqlRawAsync(LogOverrideConfiguration.CreateIfNotExistsSql);

    public Task EnsureSerilogTableExistsAsync(TableConfiguration tableConfiguration) =>
        Database.ExecuteSqlRawAsync(SerilogEventConfiguration.CreateIfNotExistsSql(tableConfiguration));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Description).HasMaxLength(2000);
            entity.Property(item => item.Price).HasPrecision(10, 2);
        });

        modelBuilder.ApplyConfiguration(new LogOverrideConfiguration());
    }
}

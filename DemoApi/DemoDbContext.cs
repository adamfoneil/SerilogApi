using Microsoft.EntityFrameworkCore;
using SerilogLevelApi.MySql;

namespace DemoApi;

public sealed class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options), ILogOverridesTable
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<SerilogEvent> SerilogEvents => Set<SerilogEvent>();

    public DbSet<LogOverride> LogOverrides { get; set; }

    public Task EnsureLogOverridesTableExistsAsync() =>
        Database.ExecuteSqlRawAsync(LogOverrideConfiguration.CreateIfNotExistsSql);

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

        modelBuilder.Entity<SerilogEvent>(entity =>
        {
            entity.ToTable("serilog_events");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Level).HasMaxLength(32);
            entity.Property(log => log.Message).HasMaxLength(4000);
            entity.Property(log => log.MessageTemplate).HasMaxLength(4000);
            entity.Property(log => log.Exception).HasColumnType("longtext");
            entity.Property(log => log.PropertiesJson).HasColumnType("longtext");
        });

        modelBuilder.ApplyConfiguration(new LogOverrideConfiguration());
    }
}

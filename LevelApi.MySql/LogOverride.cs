using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SerilogLevelApi.MySql;

public class LogOverride
{
    public string Category { get; set; } = default!;
    public string Level { get; set; } = default!;
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class LogOverrideConfiguration : IEntityTypeConfiguration<LogOverride>
{
    public void Configure(EntityTypeBuilder<LogOverride> builder)
    {
        builder.ToTable("serilog_level_overrides");
        builder.HasKey(x => x.Category);
        builder.Property(x => x.Category).HasMaxLength(255);
        builder.Property(x => x.Level).HasMaxLength(32);
        builder.Property(x => x.ExpiresUtc).HasColumnType("datetime(6)");
    }
}

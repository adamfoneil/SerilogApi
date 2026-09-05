using Microsoft.EntityFrameworkCore;

namespace SerilogLevelApi.MySql;

public interface ILogOverridesTable
{
    DbSet<LogOverride> LogOverrides { get; set; }
}

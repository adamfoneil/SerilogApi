using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySqlConnector;

namespace DemoApi;

public sealed class DemoDbContextFactory : IDesignTimeDbContextFactory<DemoDbContext>
{
    public DemoDbContext CreateDbContext(string[] args)
    {
        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = "localhost",
            Port = 3306,
            Database = "serilogdemo",
            UserID = "mysql",
            Password = "mysql"
        }.ConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<DemoDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 4, 0)));

        return new DemoDbContext(optionsBuilder.Options);
    }
}

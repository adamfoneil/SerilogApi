using DemoApi;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var database = await DemoDatabaseConnection.CreateAsync(builder.Configuration);
var mySqlLogSink = new MySqlLogSink(database.ConnectionString);

var dbContextOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString))
    .Options;

await using (var db = new DemoDbContext(dbContextOptions))
{
    await db.Database.MigrateAsync();
}

builder.Services.AddSingleton(database);
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString)));

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestQuery |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
});

builder.Services.AddOpenApi();

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Sink(mySqlLogSink);
});

var app = builder.Build();

app.UseHttpLogging();

app.MapOpenApi();
app.MapScalarApiReference("/scalar/v1");

app.MapDemoEndpoints();

try
{
    await app.RunAsync();
}
finally
{
    await mySqlLogSink.DisposeAsync();
    await database.DisposeAsync();
}

public partial class Program;

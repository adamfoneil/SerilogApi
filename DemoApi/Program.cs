using DemoApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SerilogLevelApi;
using SerilogLevelApi.MySql;

var builder = WebApplication.CreateBuilder(args);

// MySQL test container
var database = await DemoDatabaseConnection.CreateAsync(builder.Configuration);

// custom Serilog sink
var mySqlLogSink = new MySqlLogSink(database.ConnectionString);

var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
//var logLevelOverrides = new MySqlLogLevelOverrides(database.ConnectionString);

// main db context used by demo app
var dbContextOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString))
    .Options;

await using (var db = new DemoDbContext(dbContextOptions))
{
    await db.Database.MigrateAsync();
}


builder.Services.AddSingleton(database);
builder.Services.AddSingleton(levelSwitch);

builder.Services.AddMySqlLogLevelOverrides<DemoDbContext>(database.ConnectionString);

builder.Services.AddHostedService<SerilogLevelMonitor>();
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString)));
builder.Services.AddAuthorization();

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
        .MinimumLevel.ControlledBy(levelSwitch)
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
app.MapLogLevelEndpoints(
    new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build());

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

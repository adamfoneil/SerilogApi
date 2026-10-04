using DemoApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using QueryApi.MySql;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SerilogLevelApi;
using SerilogLevelApi.MySql;
using SerilogQueryApi;
using SerilogUtil;

var builder = WebApplication.CreateBuilder(args);

// MySQL test container
var database = await DemoDatabaseConnection.CreateAsync(builder.Configuration);
builder.Services.AddSingleton(database);

var tableConfiguration = MySqlLogQuery.DefaultTableConfiguration;

builder.Services.AddSerilogQuery(database.ConnectionString, tableConfiguration);

// custom Serilog sink
var mySqlLogSink = new MySqlLogSink(database.ConnectionString, tableConfiguration);

// global level switch with default min level, managed by our monitor. Determines overall log level
var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);

// main db context used by demo app
var dbContextOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString))
    .Options;

await using (var db = new DemoDbContext(dbContextOptions))
{
    await db.Database.MigrateAsync();
    await db.EnsureSerilogTableExistsAsync(tableConfiguration);
}

builder.Services.AddMySqlLogLevelOverrides<DemoDbContext>(database.ConnectionString, levelSwitch);

builder.Services.AddDbContext<DemoDbContext>((services, options) =>
{
    options.UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString));
}, ServiceLifetime.Singleton);

builder.Services.AddSingleton(tableConfiguration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SerilogQueryPolicy", policy =>
        policy.RequireAssertion(_ => true)); // Allow all for demo

    options.AddPolicy("SerilogLevelPolicy", policy =>
        policy.RequireAssertion(_ => true)); // Allow all for demo
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Register Blazor UI services
builder.Services.AddScoped<Blazor.RCL.Services.SerilogQueryService>();
builder.Services.AddScoped<Blazor.RCL.Services.SerilogLevelService>();

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
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Connection", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("SerilogLevelApi", LogEventLevel.Information) // this is so actions from related internal component show regardless of levelSwitch
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(mySqlLogSink);
});

var app = builder.Build();

app.UseHttpLogging();

app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<DemoApi.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapOpenApi();
app.MapScalarApiReference("/scalar/v1");

app.MapDemoEndpoints();

var allowAll = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true) // for demo purposes, no authorization needed
        .Build();

// enables you to debug (and auto revert after 10 minutes)
app.MapLogLevelEndpoints(allowAll);

// enables you to query serilog data
app.MapLogQueryEndpoints(allowAll);

try
{
    await app.RunAsync();
}
finally
{
    await mySqlLogSink.DisposeAsync();
    await database.DisposeAsync();
}


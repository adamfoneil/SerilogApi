It should be easier to enable dynamic log level capability to Serilog. Typically, I'll run a production service at a high log level (warning or info) to minimize log ingestion. During an incident, I need to elevate the log detail temporarily. This is possible with Serilog's `LoggingLevelSwitch` but there are few moving pieces you need to make this work. This project brings together some infrastructure to make this easier in your applications.

A secondary purpose of this project is to enable Serilog querying. When you've temporarily elevated log detail, how do you query it? A wealth of off-the-shelf observability solutions for exactly this already exist. Why build another? The reason is that in enterprise settings, observability tools are expensive and hard to justify, or they are gatekept away for beaurocratic reasons. If you're already using a relational database sink with Serilog, querying it by SQL is natural to do, but still rather complex. The goal here therefore is to implement some practical query patterns you can plug into any application.

# Usage

## Level API

Step-by-step how to implement dynamic log leveling:

1. In your startup Program.cs, create a `LoggingLevelSwitch` with a desired default minimum level.

```csharp
var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);
```

2. Register log level management objects with the `AddMySqlLogLevelOverrides` method. This is in the LevelApi.MySql project. It requires a DbContext, connection string, and the `levelSwitch` you created above. The DbContext must implement [ILogOverridesTable](LevelApi.MySql/ILogOverridesTable.cs). See my example in [DemoApi/DemoDbContext](DemoApi/DemoDbContext.cs).

```csharp
builder.Services.AddMySqlLogLevelOverrides<YourDbContext>(<connection string>, levelSwitch);
```

This tracks dynamic log level changes in your database, and ensures that other instances of your app know what levels are overridden and when those overrides expire. Your app instances use a background service (also registered by this method) [SerilogLevelMonitor](LevelApi/SerilogLevelMonitor.cs) to poll the database every 5 seconds for updated log levels.

3. Configure Serilog, passing your `levelSwitch` and setting any default overrides appropriate for your application.

```csharp
builder.Host.UseSerilog((_, _, config) =>
{
    config
        .MinimumLevel.ControlledBy(levelSwitch)        
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)        
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("SerilogLevelApi", LogEventLevel.Information) 
        .Enrich.FromLogContext()
        .WriteTo(<your sinks>)
});
```

4. Map admin endpoints for managing overrides, supplying your own authorization policy. See example in [Program.cs](DemoApi/Program.cs). See also [LevelApi/EndpointExtensions.cs](LevelApi/EndpointExtensions.cs)

```csharp
app.MapLogLevelEndpoints(<auth policy>);
```


# Source Tour

[LevelApi](LevelApi/LevelApi.csproj) is for working with dynamic log levels. This is the core product.
- [ILogLevelStore](LevelApi/ILogLevelStore.cs) persists desired log level overrides along with optional expiration date/times.
- [SerilogLevelMonitor](LevelApi/SerilogLevelMonitor.cs) a background service that keeps log levels of load-balanced instances of an app in sync, and reverts temporarily elevated levels
- [EndpointExtensions](LevelApi/EndpointExtensions.cs) make it possible to manage log levels externally, without modifying or restarting your app.

[QueryApi](QueryApi/QueryApi.csproj) makes Serilog data securely queryable from your application when you don't have another way
- [ILogQuery](QueryApi/ILogQuery.cs) defines query operations against your Serilog data store
- [EndpointExtensions](QueryApi/EndpointExtensions.cs) enables secure querying of Serilog data

[DemoApi](DemoApi/DemoApi.csproj) is a runnable minimal API sample
- uses EF Core with MySQL for a disposable `items` table
- starts a disposable MySQL Testcontainer automatically when `ConnectionStrings__Default` is not configured
- allows overriding the default container image with `DemoApi__MySqlImage`
- writes application logs to both the console and a `serilog_events` table in MySQL
- exposes minimal `/items` endpoints for listing, creating, and updating catalog-style items

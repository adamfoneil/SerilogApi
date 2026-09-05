It should be easier to enable dynamic log level capability to Serilog. Typically, I'll run a production service at a high log level (warning or info) to minimize log ingestion. During an incident, I need to elevate the log detail temporarily. This is possible with Serilog's `LogLevelSwitch` but there are few moving pieces to this to make work. This project brings together some infrastructure to make this easier in your applications.


[LevelApi](LevelApi/LevelApi.csproj) is for working with dynamic log levels. This is the core product.
- [ILogLevelStore](LevelApi/ILogLevelStore.cs) persists desired log levels
- [SerilogLevelMonitor](LevelApi/SerilogLevelMonitor.cs) a background service that keeps log levels of load-balanced instances of an app in sync, and reverts temporarily elevated levels
- [EndpointExtensions](LevelApi/EndpointExtensions.cs) make it possible to manage log levels externally, without modifying or restarting your app.

[QueryApi](QueryApi/QueryApi.csproj) makes Serilog data securely queryable from your application when you don't have another way
- [ILogQuery](QueryApi/ILogQuery.cs) defines query operations against your Serilog data store
- [EndpointExtensions](QueryApi/EndpointExtensions.cs) enables secure querying of Serilog data

[DemoApi](DemoApi/DemoApi.csproj) is a runnable minimal API sample
- uses EF Core with MySQL for a disposable `items` table
- starts a disposable MySQL Testcontainer automatically when `ConnectionStrings__Default` is not configured
- writes application logs to both the console and a `serilog_events` table in MySQL
- exposes minimal `/items` endpoints for listing, creating, and updating catalog-style items
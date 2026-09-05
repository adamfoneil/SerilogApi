It should be easier to enable dynamic log level capability to Serilog. Typically, I'll run a production service at a high log level (warning or info) to minimize log ingestion. During an incident, I need to elevate the log detail temporarily. This is possible with Serilog's `LogLevelSwitch` but there are few moving pieces to this to make work. This project brings together some infrastructure to make this easier in your applications:

- some interfaces you implement:
  - [ILogLevelStore](SerilogApi/ILogLevelStore.cs) persists desired log levels
  - [ILogQuery](SerilogApi/ILogQuery.cs) defines query operations against your Serilog data store
- a background service that keeps log levels of load-balanced instances of an app in sync [SerilogLevelMonitor](SerilogApi/SerilogLevelMonitor.cs)
- endpoint extensions for making these capabilities possible to execute from outside your app [EndpointExtensions](SerilogApi/EndpointExtensions.cs)
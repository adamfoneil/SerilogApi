It should be easier to enable dynamic log level capability to Serilog. Typically, I'll run a production service at a high log level (warning or above) to minimize log ingestion, but need to temporarily elevate the log detail when responding to an incident. This is possible with Serilog's `LogLevelSwitch` but it needs to be wired in a certain way. This project is that "certain way." We'll do this with API endpoints you can add to any web project to 

- Inspect and elevate log levels. To prevent runaway log ingestion, escalations automatically revert after a few minutes.
- Query your serilog store via API endpoints. While there are many log view apps out there, these tend to require enterprise purchase and be unavailable to ICs.

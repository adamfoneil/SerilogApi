namespace SerilogQueryApi;

public record QueryRequest(
    LogCriteria Criteria,
    // additional columns to return (intended for json extracts of the Message)
    JsonColumn[]? Columns = null);

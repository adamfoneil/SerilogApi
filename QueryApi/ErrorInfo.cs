namespace SerilogQueryApi;

public record ErrorInfo(
    // age of most recent RequestId
    TimeSpan Age,
    string SourceContext,
    string MessageTemplate,
    // most recent RequestIds
    string[] RequestIds);

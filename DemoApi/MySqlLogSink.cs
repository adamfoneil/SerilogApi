using System.Text.Json;
using System.Threading.Channels;
using MySqlConnector;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace DemoApi;

public sealed class MySqlLogSink : ILogEventSink, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly Channel<PendingLogEvent> _channel = Channel.CreateUnbounded<PendingLogEvent>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    private readonly Task _processorTask;

    public MySqlLogSink(string connectionString)
    {
        _connectionString = connectionString;
        _processorTask = Task.Run(ProcessAsync);
    }

    public void Emit(LogEvent logEvent)
    {
        var pending = new PendingLogEvent(
            logEvent.Timestamp.UtcDateTime,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(),
            logEvent.MessageTemplate.Text,
            logEvent.Exception?.ToString(),
            JsonSerializer.Serialize(
                logEvent.Properties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString())));

        if (!_channel.Writer.TryWrite(pending))
        {
            SelfLog.WriteLine("Failed to queue Serilog event for MySQL persistence.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _processorTask;
    }

    private async Task ProcessAsync()
    {
        var batch = new List<PendingLogEvent>(20);

        await foreach (var logEvent in _channel.Reader.ReadAllAsync())
        {
            batch.Add(logEvent);

            if (batch.Count == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            while (batch.Count < 20 && _channel.Reader.TryRead(out var queuedLogEvent))
            {
                batch.Add(queuedLogEvent);
            }

            await FlushBatchAsync(batch);
            batch.Clear();
        }
    }

    private async Task FlushBatchAsync(List<PendingLogEvent> batch)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            foreach (var logEvent in batch)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO serilog_events
                        (TimestampUtc, Level, Message, MessageTemplate, Exception, PropertiesJson)
                    VALUES
                        (@timestampUtc, @level, @message, @messageTemplate, @exception, @propertiesJson);
                    """;

                command.Parameters.AddWithValue("@timestampUtc", logEvent.TimestampUtc);
                command.Parameters.AddWithValue("@level", logEvent.Level);
                command.Parameters.AddWithValue("@message", logEvent.Message);
                command.Parameters.AddWithValue("@messageTemplate", logEvent.MessageTemplate);
                command.Parameters.AddWithValue("@exception", logEvent.Exception);
                command.Parameters.AddWithValue("@propertiesJson", logEvent.PropertiesJson);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Failed to write Serilog batch to MySQL: {0}", ex);
        }
    }

    private sealed record PendingLogEvent(
        DateTime TimestampUtc,
        string Level,
        string Message,
        string MessageTemplate,
        string? Exception,
        string PropertiesJson);
}

using System.Text;
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
    private readonly Channel<PendingLogEvent> _channel = Channel.CreateBounded<PendingLogEvent>(new BoundedChannelOptions(1024)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropWrite
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
            SelfLog.WriteLine("Dropped Serilog event because the MySQL buffer is full.");
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
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            var sql = new StringBuilder(
                """
                INSERT INTO serilog_events
                    (TimestampUtc, Level, Message, MessageTemplate, Exception, PropertiesJson)
                VALUES
                """);

            for (var i = 0; i < batch.Count; i++)
            {
                if (i > 0)
                {
                    sql.AppendLine(",");
                }

                sql.Append($"(@timestampUtc{i}, @level{i}, @message{i}, @messageTemplate{i}, @exception{i}, @propertiesJson{i})");

                var logEvent = batch[i];
                command.Parameters.AddWithValue($"@timestampUtc{i}", logEvent.TimestampUtc);
                command.Parameters.AddWithValue($"@level{i}", logEvent.Level);
                command.Parameters.AddWithValue($"@message{i}", logEvent.Message);
                command.Parameters.AddWithValue($"@messageTemplate{i}", logEvent.MessageTemplate);
                command.Parameters.AddWithValue($"@exception{i}", logEvent.Exception);
                command.Parameters.AddWithValue($"@propertiesJson{i}", logEvent.PropertiesJson);
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync();
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

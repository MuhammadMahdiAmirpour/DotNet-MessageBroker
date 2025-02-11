using System.Net.Http.Json;
using MessageBroker.Core.Base;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

namespace MessageBroker.Producer.App;

public class DefaultProducer : ThreadedProducerBase
{
    private readonly HttpClient _client;
    private readonly string _brokerUrl;
    private readonly string _topic;
    private bool _disposed;

    public DefaultProducer(
        IMyCustomLogger logger,
        string topic,
        string brokerUrl = "http://localhost:5000"
    ) : base(logger)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _brokerUrl = brokerUrl.TrimEnd('/') ?? throw new ArgumentNullException(nameof(brokerUrl));
            
        _client = new HttpClient
        {
            BaseAddress = new Uri(_brokerUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        Logger.LogInfo($"Producer initialized with topic: {topic}, broker URL: {_brokerUrl}");
    }

    protected override async Task<bool> ProduceMessageAsync(IMessage message)
    {
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultProducer));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Logger.LogInfo($"Preparing to send message {message.Id} to topic {_topic}");

            var messageDto = new MessageDto
            {
                Id = message.Id.ToString(),
                Topic = _topic,
                Content = System.Text.Encoding.UTF8.GetString(message.Payload ?? Array.Empty<byte>()),
                ConsumerGroup = message.ConsumerGroup ?? string.Empty,
                Priority = message.Priority,
                Timestamp = message.Timestamp == default ? DateTime.UtcNow : message.Timestamp,
                Headers = new Dictionary<string, string>
                {
                    { "producer", Environment.UserName },
                    { "hostname", Environment.MachineName },
                    { "timestamp", DateTime.UtcNow.ToString("O") }
                }
            };

            Logger.LogInfo($"Sending message to {_brokerUrl}/api/messagebroker/messages");
                
            using var response = await _client.PostAsJsonAsync("/api/messagebroker/messages", messageDto);
                
            var responseContent = await response.Content.ReadAsStringAsync();
                
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError($"Server returned {response.StatusCode}: {responseContent}", 
                    new Exception($"HTTP {(int)response.StatusCode}: {responseContent}"));
                return false;
            }

            Logger.LogInfo($"Successfully produced message {message.Id} to topic {_topic}");
            Logger.LogInfo($"Server response: {responseContent}");
            return true;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError($"HTTP request failed: {ex.Message}", ex);
            RaiseOnError(ex);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to produce message: {ex.Message}", ex);
            RaiseOnError(ex);
            return false;
        }
    }

    public override async Task<bool> IsConnectedAsync()
    {
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultProducer));
            }

            Logger.LogInfo("Checking connection to broker...");
            var response = await _client.GetAsync("/api/messagebroker/health");
                
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Logger.LogError($"Health check failed with status {response.StatusCode}: {error}");
                return false;
            }

            Logger.LogInfo("Successfully connected to broker");
            return true;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError($"Connection check failed (network error): {ex.Message}", ex);
            RaiseOnError(ex);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Connection check failed: {ex.Message}", ex);
            RaiseOnError(ex);
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _client.Dispose();
                    Logger.LogInfo("Producer disposed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during producer disposal", ex);
                }
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
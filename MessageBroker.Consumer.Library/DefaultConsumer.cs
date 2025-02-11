using System.Net.Http.Json;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Base;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

namespace MessageBroker.Consumer.Library;

[RateLimit(3)]
public class DefaultConsumer : ThreadedConsumerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _brokerUrl;
    private readonly string _topic;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private readonly string _consumerId;
    private readonly string _currentUser;
    private bool _isRegistered;

    public DefaultConsumer(
        IMyCustomLogger logger,
        string topic,
        string consumerGroup,
        string brokerUrl = "http://localhost:5000") 
        : base(logger, consumerGroup)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _brokerUrl = brokerUrl.TrimEnd('/');
        _currentUser = "MuhammadMahdiAmirpour"; // Current user's login
        _maxRetries = 3;
        _retryDelayMs = 2000;
        _consumerId = $"consumer-{_currentUser.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        _isRegistered = false;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_brokerUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer initialized");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - User: {_currentUser}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer ID: {_consumerId}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Topic: {_topic}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Group: {ConsumerGroup}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Broker URL: {_brokerUrl}");
    }

    public override async Task<bool> IsConnectedAsync()
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync("api/messagebroker/health", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var healthData = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
                    MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Successfully connected to broker");
                    return true;
                }
            }
            catch (Exception ex)
            {
                MyCustomLogger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Connection attempt {i + 1}/{_maxRetries} failed: {ex.Message}");
                if (i < _maxRetries - 1)
                {
                    await Task.Delay(_retryDelayMs * (i + 1));
                }
            }
        }

        return false;
    }

    public async Task<bool> RegisterAsync()
    {
        try
        {
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Registering consumer {_consumerId}");

            var registration = new ConsumerRegistrationDto
            {
                Topic = _topic,
                ConsumerGroup = ConsumerGroup,
                ConsumerId = _consumerId
            };

            var response = await _httpClient.PostAsJsonAsync("api/messagebroker/consumers", registration);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to register consumer. Status: {response.StatusCode}. Error: {error}");
            }

            _isRegistered = true;
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {_consumerId} successfully registered");
            return true;
        }
        catch (Exception ex)
        {
            MyCustomLogger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to register consumer: {ex.Message}", ex);
            throw;
        }
    }

    protected override async Task<bool> ProcessMessageAsync(IMessage message)
    {
        if (!_isRegistered)
        {
            throw new InvalidOperationException("Consumer is not registered with the broker");
        }

        try
        {
            var content = System.Text.Encoding.UTF8.GetString(message.Payload ?? Array.Empty<byte>());
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Processing message {message.Id}");
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Content: {content}");
            Console.WriteLine($"\nReceived message: {content}");
            
            // Simulate some processing work
            await Task.Delay(100);
            
            return true;
        }
        catch (Exception ex)
        {
            MyCustomLogger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Error processing message {message.Id}: {ex.Message}", ex);
            RaiseOnError(ex);
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {_consumerId} disposed");
        }
        base.Dispose(disposing);
    }

    private class HealthCheckResponse
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = "";
    }
}
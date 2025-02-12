using System.Net.Http.Json;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Base;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

namespace MessageBroker.Consumer.Library;

[RateLimit(3)]
[Endpoint("/api/messagebroker/consumers")]
public class DefaultConsumer : ThreadedConsumerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _topic;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private readonly string _consumerId;
    private bool _isRegistered;

    public DefaultConsumer(
        IMyCustomLogger logger,
        string topic,
        string consumerGroup,
        string brokerUrl = "http://localhost:5000") 
        : base(logger, consumerGroup)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        var brokerUrl1 = brokerUrl.TrimEnd('/');
        const string currentUser = "MuhammadMahdiAmirpour"; // Current user's login
        _maxRetries = 3;
        _retryDelayMs = 2000;
        _consumerId = $"consumer-{currentUser.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        _isRegistered = false;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(brokerUrl1),
            Timeout = TimeSpan.FromSeconds(30)
        };

        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer initialized");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - User: {currentUser}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer ID: {_consumerId}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Topic: {_topic}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Group: {ConsumerGroup}");
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Broker URL: {brokerUrl1}");
    }

    public override async Task<bool> IsConnectedAsync()
    {
        for (var i = 0; i < _maxRetries; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync("api/messagebroker/health", cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var healthData = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(cancellationToken: cts.Token);
                    if (healthData?.Status == "Healthy")
                    {
                        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Successfully connected to broker. Version: {healthData.Version}, Last Check: {HealthCheckResponse.Timestamp:yyyy-MM-dd HH:mm:ss}");
                        return true;
                    }
                }
            
                await Task.Delay(_retryDelayMs * (i + 1));
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
        
            // Start polling for messages
            _ = Task.Run(() => PollMessagesAsync(CancellationToken.None));
        
            return true;
        }
        catch (Exception ex)
        {
            MyCustomLogger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to register consumer: {ex.Message}", ex);
            throw;
        }
    }

    public override async Task<bool> ProcessMessageAsync(IMessage message)
    {
        if (!_isRegistered)
        {
            throw new InvalidOperationException("Consumer is not registered with the broker");
        }

        try
        {
            var content = System.Text.Encoding.UTF8.GetString(message.Payload);
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Processing message {message.Id}");
            MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Content: {content}");
        
            // Enhanced console output
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n=== New Message Received ===");
            Console.WriteLine($"ID: {message.Id}");
            Console.WriteLine($"Content: {content}");
            Console.WriteLine($"Timestamp: {message.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("=========================\n");
            Console.ResetColor();
        
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
    
    private async Task PollMessagesAsync(CancellationToken cancellationToken)
    {
        MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Starting message polling for topic {_topic}");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var endpoint = $"api/messagebroker/topics/{_topic}/messages?consumerGroup={ConsumerGroup}";
                MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Polling endpoint: {endpoint}");
                
                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Poll response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>(cancellationToken: cancellationToken);
                    MyCustomLogger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Retrieved {messages?.Count ?? 0} messages");
                    
                    if (messages != null && messages.Any())
                    {
                        foreach (var messageDto in messages)
                        {
                            var message = new Message
                            {
                                Id = Guid.Parse(messageDto.Id),
                                Topic = messageDto.Topic,
                                ConsumerGroup = messageDto.ConsumerGroup ?? "",
                                Priority = messageDto.Priority,
                                Timestamp = messageDto.Timestamp,
                                Payload = System.Text.Encoding.UTF8.GetBytes(messageDto.Content)
                            };
                            
                            await ProcessMessageAsync(message);
                        }
                    }
                }
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                MyCustomLogger.LogError($"Error polling messages: {ex.Message}", ex);
                await Task.Delay(5000, cancellationToken);
            }
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
        public static DateTime Timestamp => default;

        public string Version { get; set; } = "";
    }
}
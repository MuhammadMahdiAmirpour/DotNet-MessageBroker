using System.Net.Http.Json;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

namespace MessageBroker.Consumer.App;

public class DefaultConsumer : IConsumer
{
    private readonly IMyCustomLogger _logger;
    private readonly string _consumerGroup;
    private readonly HttpClient _httpClient;
    private readonly string _brokerUrl;
    private readonly int _maxRetries = 3;
    private readonly int _retryDelayMs = 2000;
    private readonly int _pollIntervalMs = 5000; // Polling interval
    private bool _disposed;
    private readonly string _consumerId;
    private readonly string _topic;
    private bool _isRegistered;
    private CancellationTokenSource? _pollCancellation;

    public string ConsumerGroup => _consumerGroup;
    public event EventHandler<Exception>? OnError;
    public event EventHandler<IMessage>? OnMessageReceived;

    public DefaultConsumer(
        IMyCustomLogger logger,
        string topic,
        string consumerGroup,
        string brokerUrl = "http://localhost:5000")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _consumerGroup = consumerGroup ?? throw new ArgumentNullException(nameof(consumerGroup));
        _brokerUrl = brokerUrl.TrimEnd('/') ?? throw new ArgumentNullException(nameof(brokerUrl));
        _consumerId = $"{Environment.MachineName}-{Guid.NewGuid()}";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_brokerUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _logger.LogInfo($"Consumer initialized with broker URL: {_brokerUrl}, topic: {_topic}, group: {_consumerGroup}, id: {_consumerId}");
    }

    public async Task<bool> IsConnectedAsync()
    {
        ThrowIfDisposed();

        _logger.LogInfo($"Checking connection to broker at {_brokerUrl}");

        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync("api/messagebroker/health", cts.Token);
                    
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInfo($"Successfully connected to message broker at {_brokerUrl}");
                    _logger.LogInfo($"Health check response: {content}");
                    return true;
                }
                    
                _logger.LogWarning($"Failed to connect to broker (attempt {i + 1}/{_maxRetries}). Status: {response.StatusCode}. Content: {content}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Connection attempt {i + 1}/{_maxRetries} timed out");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Connection attempt {i + 1}/{_maxRetries} failed: {ex.Message}", ex);
            }

            if (i < _maxRetries - 1)
            {
                var delayMs = _retryDelayMs * (i + 1);
                _logger.LogInfo($"Waiting {delayMs}ms before next attempt...");
                try
                {
                    await Task.Delay(delayMs);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Connection retry was cancelled");
                    break;
                }
            }
        }

        var error = new Exception($"Failed to connect to message broker at {_brokerUrl} after {_maxRetries} attempts");
        _logger.LogError("Connection failed", error);
        OnError?.Invoke(this, error);
        return false;
    }
    
    public async Task StartPollingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isRegistered)
        {
            throw new InvalidOperationException("Consumer must be registered before starting to poll");
        }

        _pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _logger.LogInfo("Starting to poll for messages...");
            
            while (!_pollCancellation.Token.IsCancellationRequested)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"api/messagebroker/topics/{_topic}/messages?consumerGroup={_consumerGroup}",
                        _pollCancellation.Token
                    );

                    if (response.IsSuccessStatusCode)
                    {
                        var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>(_pollCancellation.Token);
                        if (messages != null && messages.Any())
                        {
                            foreach (var messageDto in messages)
                            {
                                var message = new Message
                                {
                                    Id = Guid.Parse(messageDto.Id),
                                    Topic = messageDto.Topic,
                                    ConsumerGroup = messageDto.ConsumerGroup,
                                    Priority = messageDto.Priority,
                                    Timestamp = messageDto.Timestamp,
                                    Payload = System.Text.Encoding.UTF8.GetBytes(messageDto.Content)
                                };

                                await ConsumeAsync(message);
                            }
                        }
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync(_pollCancellation.Token);
                        _logger.LogWarning($"Failed to fetch messages. Status: {response.StatusCode}. Error: {error}");
                    }

                    await Task.Delay(1000, _pollCancellation.Token); // 1 second delay between polls
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while polling messages: {ex.Message}", ex);
                    OnError?.Invoke(this, ex);
                    await Task.Delay(1000, _pollCancellation.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Message polling stopped");
        }
    }

    public async Task<bool> ConsumeAsync(IMessage message)
    {
        ThrowIfDisposed();

        if (!_isRegistered)
        {
            throw new InvalidOperationException("Consumer is not registered with the broker");
        }

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        try
        {
            var content = System.Text.Encoding.UTF8.GetString(message.Payload ?? Array.Empty<byte>());
            _logger.LogInfo($"Received message: {content}");
            Console.WriteLine($"\nReceived message: {content}");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing message {message.Id}: {ex.Message}", ex);
            OnError?.Invoke(this, ex);
            return false;
        }
    }

    public async Task<bool> RegisterAsync()
    {
        ThrowIfDisposed();

        try
        {
            _logger.LogInfo($"Registering consumer {_consumerId} for topic {_topic} in group {_consumerGroup}");

            var registration = new ConsumerRegistrationDto
            {
                Topic = _topic,
                ConsumerGroup = _consumerGroup,
                ConsumerId = _consumerId,
                Properties = new Dictionary<string, string>
                {
                    { "hostname", Environment.MachineName },
                    { "username", Environment.UserName },
                    { "registeredAt", DateTime.UtcNow.ToString("O") }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("api/messagebroker/consumers", registration);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to register consumer with broker. Status: {response.StatusCode}. Error: {error}");
            }

            _isRegistered = true;
            _logger.LogInfo($"Consumer {_consumerId} successfully registered");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to register consumer: {ex.Message}", ex);
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _pollCancellation?.Cancel();
                    _pollCancellation?.Dispose();
                    _httpClient.Dispose();
                    _logger.LogInfo($"Consumer {_consumerId} disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during consumer disposal", ex);
                }
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultConsumer));
        }
    }
    
    ~DefaultConsumer()
    {
        Dispose(false);
    }
}
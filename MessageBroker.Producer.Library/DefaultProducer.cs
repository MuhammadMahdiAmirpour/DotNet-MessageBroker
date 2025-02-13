using System.Net.Http.Json;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Base;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;
using MessageBroker.Storage;

namespace MessageBroker.Producer.Library;

[RateLimit(2)]
[ProducerImplementationData(3)]
[Endpoint("/api/messagebroker/messages")]
public class DefaultProducer : ThreadedProducerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _brokerUrl;
    private readonly int _maxRetries;
    private readonly string _producerId;
    private readonly string _currentUser;
    private readonly FileMessageStorage _storage;
    private bool _isInitialized;
    private const int RetryDelayMs = 2000;

    public DefaultProducer(IMyCustomLogger logger, string brokerUrl = "http://localhost:5000") : base(logger)
    {
        _brokerUrl = brokerUrl.TrimEnd('/');
        _currentUser = Environment.UserName;
        _producerId = $"producer-{_currentUser.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        _isInitialized = false;

        var producerData = (ProducerImplementationData)Attribute.GetCustomAttribute(
            GetType(), typeof(ProducerImplementationData))!;
        _maxRetries = producerData.RetryNumber;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_brokerUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _storage = new FileMessageStorage(logger);
    }

    public override async Task<bool> IsConnectedAsync()
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync("api/messagebroker/health", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Successfully connected to message broker");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Connection attempt {attempt}/{_maxRetries} failed: {ex.Message}");
                
                if (attempt < _maxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
        }

        Logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to connect to message broker after {_maxRetries} attempts");
        return false;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Starting producer application");
        Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Current User: {_currentUser}");
        Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Producer ID: {_producerId}");
        Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Broker URL: {_brokerUrl}");
        Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Max Retries: {_maxRetries}");

        if (await IsConnectedAsync())
        {
            _isInitialized = true;
            await ProcessStoredMessagesAsync();
            Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Producer initialized successfully");
        }
        else
        {
            Logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to initialize producer - broker unavailable");
            _isInitialized = false;
        }
    }

    private async Task ProcessStoredMessagesAsync()
    {
        var messages = await _storage.LoadAllMessagesAsync();
        foreach (var storedMessage in messages.OrderBy(m => m.Message.Timestamp))
        {
            await TrySendMessageAsync(storedMessage.Message);
        }
    }

    public override async Task<bool> ProduceMessageAsync(IMessage message)
    {
        // Always save the message first
        await _storage.SaveMessageAsync(new MessageStorageItem { Message = message, Topic = message.Topic });

        if (!_isInitialized)
        {
            Logger.LogInfo($"Producer not initialized. Message {message.Id} saved for later delivery");
            return false;
        }

        return await TrySendMessageAsync(message);
    }

    private async Task<bool> TrySendMessageAsync(IMessage message)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var messageDto = new MessageDto
                {
                    Id = message.Id.ToString(),
                    Topic = message.Topic,
                    ConsumerGroup = message.ConsumerGroup,
                    Priority = message.Priority,
                    Timestamp = DateTime.UtcNow,
                    Content = System.Text.Encoding.UTF8.GetString(message.Payload ?? Array.Empty<byte>()),
                    Headers = new Dictionary<string, string>
                    {
                        { "ProducerId", _producerId },
                        { "Producer-User", _currentUser },
                        { "Producer-Timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
                        { "Attempt", attempt.ToString() }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("api/messagebroker/messages", messageDto);
                
                if (response.IsSuccessStatusCode)
                {
                    await _storage.RemoveMessageAsync(message.Topic, message.Id);
                    Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Successfully published message {message.Id} to topic {message.Topic}");
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();
                Logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to publish message {message.Id} (Attempt {attempt}/{_maxRetries}): {error}");

                if (attempt < _maxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Error publishing message {message.Id} (Attempt {attempt}/{_maxRetries}): {ex.Message}");
                
                if (attempt < _maxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }
                else
                {
                    RaiseOnError(ex);
                    return false;
                }
            }
        }

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
            var storagePath = Path.Combine(AppContext.BaseDirectory, "data", "messages");
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, true);
                Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Storage cleaned up successfully");
            }
            Logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Producer {_producerId} disposed");
        }
        base.Dispose(disposing);
    }
}

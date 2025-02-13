using System.Collections.Concurrent;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;

namespace MessageBroker.API.Services;

public class MessageBrokerServer : IMessageBrokerServer
{
    private readonly IMyCustomLogger _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConsumerInfo>> _consumers;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IMessage>> _messageQueues;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<Guid>>> _processedMessages;
    private volatile bool _isRunning;

    public MessageBrokerServer(IMyCustomLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumers = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConsumerInfo>>();
        _messageQueues = new ConcurrentDictionary<string, ConcurrentQueue<IMessage>>();
        _processedMessages = new ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<Guid>>>();
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Message broker server is already running");
            return;
        }

        try
        {
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Starting message broker server...");
            _isRunning = true;
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Message broker server started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to start message broker server", ex);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Message broker server is not running");
            return;
        }

        try
        {
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Stopping message broker server...");
            _isRunning = false;
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Message broker server stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to stop message broker server", ex);
            throw;
        }
    }

    public async Task<bool> RegisterConsumerAsync(string topic, string consumerGroup, string consumerId)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Message broker server is not running");
        }

        try
        {
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Registering consumer {consumerId} for topic {topic} in group {consumerGroup}");

            var key = GetConsumerKey(topic, consumerGroup);
            var consumers = _consumers.GetOrAdd(key, _ => new ConcurrentDictionary<string, ConsumerInfo>());

            var consumerInfo = new ConsumerInfo
            {
                ConsumerId = consumerId,
                Topic = topic,
                ConsumerGroup = consumerGroup,
                RegisteredAt = DateTime.UtcNow
            };

            if (consumers.TryAdd(consumerId, consumerInfo))
            {
                _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {consumerId} successfully registered");
                return true;
            }

            _logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {consumerId} already registered");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to register consumer {consumerId}", ex);
            throw;
        }
    }

    public async Task<bool> UnregisterConsumerAsync(string consumerId)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Message broker server is not running");
        }

        try
        {
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Unregistering consumer {consumerId}");

            var removed = false;
            foreach (var consumers in _consumers.Values)
            {
                if (consumers.TryRemove(consumerId, out var removedConsumer))
                {
                    removed = true;
                    _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {consumerId} removed from topic {removedConsumer.Topic}");
                    break;
                }
            }

            if (removed)
            {
                _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {consumerId} successfully unregistered");
                return true;
            }

            _logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer {consumerId} not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to unregister consumer {consumerId}", ex);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetRegisteredConsumersAsync(string topic, string consumerGroup)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Message broker server is not running");
        }

        try
        {
            var key = GetConsumerKey(topic, consumerGroup);
            if (_consumers.TryGetValue(key, out var consumers))
            {
                var consumerList = consumers.Keys.ToList();
                _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Retrieved {consumerList.Count} consumers for topic {topic} and group {consumerGroup}");
                return consumerList;
            }

            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - No consumers found for topic {topic} and group {consumerGroup}");
            return Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to get consumers for topic {topic}", ex);
            throw;
        }
    }

    public async Task<bool> PublishMessageAsync(string topic, IMessage message)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Message broker server is not running");

        try
        {
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Publishing message {message.Id} to topic {topic}");
            var queue = _messageQueues.GetOrAdd(topic, _ => new ConcurrentQueue<IMessage>());
            queue.Enqueue(message);
            _logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Message {message.Id} published successfully to topic {topic}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Failed to publish message {message.Id}", ex);
            throw;
        }
    }

    public async Task<IEnumerable<IMessage>> GetMessagesAsync(string topic, string? consumerGroup = null)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Message broker server is not running");

        try
        {
            if (!_messageQueues.TryGetValue(topic, out var queue))
            {
                return Enumerable.Empty<IMessage>();
            }

            var messages = queue.ToList().OrderBy(m => m.Timestamp).ToList();
            var processedMessageIds = _processedMessages
                .GetOrAdd(topic, _ => new ConcurrentDictionary<string, HashSet<Guid>>())
                .GetOrAdd(consumerGroup ?? "", _ => new HashSet<Guid>());

            var unprocessedMessages = messages.Where(m => !processedMessageIds.Contains(m.Id)).ToList();
            foreach (var message in unprocessedMessages)
            {
                processedMessageIds.Add(message.Id);
            }

            _logger.LogInfo($"Returning {unprocessedMessages.Count} messages for topic {topic} and consumer group {consumerGroup}");
            return unprocessedMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get messages for topic {topic}", ex);
            throw;
        }
    }
    
    private static string GetConsumerKey(string topic, string consumerGroup)
    {
        return $"{topic}:{consumerGroup}";
    }
}

public class ConsumerInfo
{
    public string ConsumerId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}

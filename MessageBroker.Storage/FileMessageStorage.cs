using System.Collections.Concurrent;
using System.Text.Json;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

namespace MessageBroker.Storage;

public class FileMessageStorage : IMessageStorage
{
    private readonly string _storagePath;
    private readonly IMyCustomLogger _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, IMessage>> _messageCache;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<Guid>>> _consumedMessages;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _syncLock = new();

    public FileMessageStorage(IMyCustomLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storagePath = Path.Combine(AppContext.BaseDirectory, "data", "messages");
        _messageCache = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IMessage>>();
        _consumedMessages = new ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<Guid>>>();
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
            
        Directory.CreateDirectory(_storagePath);
        LoadMessagesFromDisk();
    }

    private void LoadMessagesFromDisk()
    {
        try
        {
            lock (_syncLock)
            {
                var topicDirs = Directory.GetDirectories(_storagePath);
                var totalMessages = 0;
                var loadedTopics = 0;

                foreach (var topicDir in topicDirs)
                {
                    try
                    {
                        var topic = Path.GetFileName(topicDir);
                        var topicCache = new ConcurrentDictionary<Guid, IMessage>();
                        _messageCache[topic] = topicCache;

                        var consumedFile = Path.Combine(topicDir, "consumed.json");
                        if (File.Exists(consumedFile))
                        {
                            var json = File.ReadAllText(consumedFile);
                            var consumed = JsonSerializer.Deserialize<Dictionary<string, HashSet<Guid>>>(json, _jsonOptions);
                            if (consumed != null)
                            {
                                var consumedDict = new ConcurrentDictionary<string, HashSet<Guid>>();
                                foreach (var kvp in consumed)
                                {
                                    consumedDict[kvp.Key] = kvp.Value;
                                }
                                _consumedMessages[topic] = consumedDict;
                            }
                        }

                        var files = Directory.GetFiles(topicDir, "*.msg");
                        foreach (var file in files)
                        {
                            try
                            {
                                var json = File.ReadAllText(file);
                                var message = JsonSerializer.Deserialize<Message>(json, _jsonOptions);
                                if (message != null)
                                {
                                    topicCache.TryAdd(message.Id, message);
                                    totalMessages++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error loading message file {file}: {ex.Message}", ex);
                            }
                        }
                        loadedTopics++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error loading topic directory {topicDir}: {ex.Message}", ex);
                    }
                }

                _logger.LogInfo($"Loaded {totalMessages} messages from {loadedTopics} topics in disk storage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading messages from disk: {ex.Message}", ex);
        }
    }

    public async Task<bool> SaveMessageAsync(MessageStorageItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (item.Message == null) throw new ArgumentException("Message cannot be null", nameof(item));
        if (string.IsNullOrEmpty(item.Topic)) throw new ArgumentException("Topic cannot be empty", nameof(item));

        try
        {
            var topicPath = Path.Combine(_storagePath, item.Topic);
            Directory.CreateDirectory(topicPath);

            var filePath = Path.Combine(topicPath, $"{item.Message.Id}.msg");
            var json = JsonSerializer.Serialize(item.Message, _jsonOptions);
            
            await File.WriteAllTextAsync(filePath, json);

            var topicCache = _messageCache.GetOrAdd(item.Topic, 
                _ => new ConcurrentDictionary<Guid, IMessage>());
            
            if (topicCache.TryAdd(item.Message.Id, item.Message))
            {
                _logger.LogInfo($"Message {item.Message.Id} saved to storage in topic {item.Topic}");
                return true;
            }

            _logger.LogWarning($"Message {item.Message.Id} already exists in topic {item.Topic}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving message {item.Message.Id}: {ex.Message}", ex);
            return false;
        }
    }

    public Task<IEnumerable<MessageStorageItem>> LoadAllMessagesAsync()
    {
        var result = new List<MessageStorageItem>();

        try
        {
            foreach (var topicKvp in _messageCache)
            {
                foreach (var messageKvp in topicKvp.Value)
                {
                    result.Add(new MessageStorageItem
                    {
                        Topic = topicKvp.Key,
                        Message = messageKvp.Value
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading all messages: {ex.Message}", ex);
        }

        return Task.FromResult<IEnumerable<MessageStorageItem>>(result);
    }

    public async Task<IEnumerable<IMessage>> LoadMessagesAsync(string topic, string? consumerGroup = null)
    {
        if (string.IsNullOrEmpty(topic))
            throw new ArgumentException("Topic cannot be empty", nameof(topic));

        try
        {
            if (_messageCache.TryGetValue(topic, out var topicCache))
            {
                if (string.IsNullOrEmpty(consumerGroup))
                {
                    return topicCache.Values;
                }

                var consumed = _consumedMessages
                    .GetOrAdd(topic, _ => new ConcurrentDictionary<string, HashSet<Guid>>())
                    .GetOrAdd(consumerGroup, _ => new HashSet<Guid>());

                var messages = topicCache.Values
                    .Where(m => !consumed.Contains(m.Id))
                    .ToList();

                foreach (var message in messages)
                {
                    consumed.Add(message.Id);
                }

                // Save consumed messages state
                await SaveConsumedMessagesAsync(topic);

                return messages;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading messages for topic {topic}: {ex.Message}", ex);
        }

        return Enumerable.Empty<IMessage>();
    }

    private async Task SaveConsumedMessagesAsync(string topic)
    {
        try
        {
            var topicPath = Path.Combine(_storagePath, topic);
            var consumedFile = Path.Combine(topicPath, "consumed.json");

            if (_consumedMessages.TryGetValue(topic, out var consumed))
            {
                var json = JsonSerializer.Serialize(consumed, _jsonOptions);
                await File.WriteAllTextAsync(consumedFile, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving consumed messages state for topic {topic}: {ex.Message}", ex);
        }
    }

    public Task ClearMessagesAsync(string topic)
    {
        if (string.IsNullOrEmpty(topic))
            throw new ArgumentException("Topic cannot be empty", nameof(topic));

        try
        {
            var topicPath = Path.Combine(_storagePath, topic);
            if (Directory.Exists(topicPath))
            {
                Directory.Delete(topicPath, true);
            }

            _messageCache.TryRemove(topic, out _);
            _consumedMessages.TryRemove(topic, out _);
            _logger.LogInfo($"Cleared all messages for topic {topic}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error clearing messages for topic {topic}: {ex.Message}", ex);
            throw;
        }

        return Task.CompletedTask;
    }
}
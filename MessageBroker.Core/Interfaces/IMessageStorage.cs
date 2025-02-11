using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;

namespace MessageBroker.Storage;

public interface IMessageStorage
{
    Task<bool> SaveMessageAsync(MessageStorageItem item);
    Task<IEnumerable<MessageStorageItem>> LoadAllMessagesAsync();
    Task<IEnumerable<IMessage>> LoadMessagesAsync(string topic, string? consumerGroup = null);
    Task ClearMessagesAsync(string topic);
}
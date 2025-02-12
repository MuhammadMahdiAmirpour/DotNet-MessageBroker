using MessageBroker.Core.Interfaces;

namespace MessageBroker.Core.Models;

public class MessageStorageItem
{
    public string Topic { get; set; } = string.Empty;
    public IMessage Message { get; set; } = null!;
}
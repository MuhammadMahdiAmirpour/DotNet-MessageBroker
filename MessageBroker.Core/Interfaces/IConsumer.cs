namespace MessageBroker.Core.Interfaces;

public interface IConsumer : IDisposable
{
    string ConsumerGroup { get; }
    Task<bool> ConsumeAsync(IMessage message);
    Task<bool> IsConnectedAsync();
    event EventHandler<Exception> OnError;
}
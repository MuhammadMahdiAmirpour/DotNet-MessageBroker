namespace MessageBroker.Core.Interfaces
{
    public interface IProducer : IDisposable
    {
        Task<bool> ProduceAsync(IMessage message);
        Task<bool> IsConnectedAsync();
        event EventHandler<Exception> OnError;
    }
}
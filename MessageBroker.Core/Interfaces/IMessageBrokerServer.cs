namespace MessageBroker.Core.Interfaces
{
    public interface IMessageBrokerServer
    {
        Task<bool> PublishMessageAsync(string topic, IMessage message);
        Task<IEnumerable<IMessage>> GetMessagesAsync(string topic, string? consumerGroup = null);
        Task StartAsync();
        Task StopAsync();
        Task<bool> RegisterConsumerAsync(string topic, string consumerGroup, string consumerId);
        Task<bool> UnregisterConsumerAsync(string consumerId);
        Task<IEnumerable<string>> GetRegisteredConsumersAsync(string topic, string consumerGroup);
    }
}
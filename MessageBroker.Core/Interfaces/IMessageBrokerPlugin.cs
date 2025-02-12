using MessageBroker.Core.Interfaces;

public interface IMessageBrokerPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Configure(string brokerUrl, string topic, string consumerGroup);
    Task ExecuteAsync(IMessage message);
    void Shutdown();
}
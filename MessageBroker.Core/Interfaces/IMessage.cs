namespace MessageBroker.Core.Interfaces;

public interface IMessage
{
    Guid Id { get; set; }
    string Topic { get; set; }
    string ConsumerGroup { get; set; }
    int Priority { get; set; }
    byte[] Payload { get; set; }
    DateTime Timestamp { get; set; }
    IMessage Clone();
}
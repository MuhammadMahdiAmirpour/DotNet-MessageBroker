namespace MessageBroker.Core.Models;

public class ConsumerInfo
{
    public string Id { get; set; }
    public string ConsumerGroup { get; set; }
    public string Topic { get; set; }
}
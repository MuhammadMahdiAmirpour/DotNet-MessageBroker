namespace MessageBroker.Core.Models;

public class ConsumerRegistrationDto
{
    public string Topic { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public string ConsumerId { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
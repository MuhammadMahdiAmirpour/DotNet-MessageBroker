namespace MessageBroker.Core.Models;

public class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}
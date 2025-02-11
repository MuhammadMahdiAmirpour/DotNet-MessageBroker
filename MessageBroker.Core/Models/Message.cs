using MessageBroker.Core.Interfaces;

namespace MessageBroker.Core.Models
{
    public class Message : IMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Topic { get; set; } = string.Empty;
        public string ConsumerGroup { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Helper property for string content
        public string Content
        {
            get => System.Text.Encoding.UTF8.GetString(Payload);
            set => Payload = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
        }

        public IMessage Clone()
        {
            return new Message
            {
                Id = this.Id,
                Topic = this.Topic,
                ConsumerGroup = this.ConsumerGroup,
                Priority = this.Priority,
                Payload = this.Payload.ToArray(),
                Timestamp = this.Timestamp
            };
        }
    }
}
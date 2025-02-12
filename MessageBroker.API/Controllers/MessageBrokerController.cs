using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;
using Microsoft.AspNetCore.Mvc;

namespace MessageBroker.API.Controllers;

[ApiController]
[Route("api/messagebroker")]
public class MessageBrokerController(IMessageBrokerServer server, IMyCustomLogger logger) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new 
        { 
            status = "Healthy", 
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    [HttpPost("consumers")]
    public async Task<IActionResult> RegisterConsumer([FromBody] ConsumerRegistrationDto registration)
    {
        logger.LogInfo($"Received registration request for consumer {registration.ConsumerId}");

        var success = await server.RegisterConsumerAsync(
            registration.Topic,
            registration.ConsumerGroup,
            registration.ConsumerId);

        if (success)
        {
            return Ok(new 
            { 
                status = "registered",
                consumerId = registration.ConsumerId,
                timestamp = DateTime.UtcNow
            });
        }

        return StatusCode(500, "Failed to register consumer");
    }

    [HttpGet("topics/{topic}/messages")]
    public async Task<IActionResult> GetMessages(string topic, [FromQuery] string? consumerGroup = null)
    {
        var messages = await server.GetMessagesAsync(topic, consumerGroup);
        var messageList = messages.Select(m => new MessageDto
        {
            Id = m.Id.ToString(),
            Topic = m.Topic,
            ConsumerGroup = m.ConsumerGroup,
            Priority = m.Priority,
            Timestamp = m.Timestamp,
            Content = System.Text.Encoding.UTF8.GetString(m.Payload)
        });

        return Ok(messageList);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> PublishMessage([FromBody] MessageDto messageDto)
    {
        logger.LogInfo($"Received message for topic {messageDto.Topic}");

        var message = new Message
        {
            Id = string.IsNullOrEmpty(messageDto.Id) ? Guid.NewGuid() : Guid.Parse(messageDto.Id),
            Topic = messageDto.Topic,
            ConsumerGroup = messageDto.ConsumerGroup ?? "",
            Priority = messageDto.Priority,
            Timestamp = DateTime.UtcNow,
            Payload = System.Text.Encoding.UTF8.GetBytes(messageDto.Content)
        };

        var success = await server.PublishMessageAsync(messageDto.Topic, message);

        if (success)
        {
            return Ok(new { id = message.Id, status = "published" });
        }

        return StatusCode(500, "Failed to publish message");
    }

    [HttpGet("debug/messages")]
    public async Task<IActionResult> GetAllMessages()
    {
        var messages = await server.GetMessagesAsync("test-topic");
        return Ok(new
        {
            count = messages.Count(),
            messages = messages.Select(m => new
            {
                id = m.Id,
                topic = m.Topic,
                content = System.Text.Encoding.UTF8.GetString(m.Payload),
                timestamp = m.Timestamp
            })
        });
    }
    
    [HttpGet("debug/storage")]
    public async Task<IActionResult> GetStorageStatus([FromServices] IMessageStorage storage)
    {
        var messages = await storage.LoadAllMessagesAsync();
        return Ok(new
        {
            count = messages.Count(),
            messages = messages.Select(m => new
            {
                id = m.Message.Id,
                topic = m.Topic,
                content = System.Text.Encoding.UTF8.GetString(m.Message.Payload),
                timestamp = m.Message.Timestamp
            })
        });
    }
}
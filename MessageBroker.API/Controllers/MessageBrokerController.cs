using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;
using Microsoft.AspNetCore.Mvc;

namespace MessageBroker.API.Controllers
{
    [ApiController]
    [Route("api/messagebroker")]
    public class MessageBrokerController : ControllerBase
    {
        private readonly IMessageBrokerServer _server;
        private readonly IMyCustomLogger _logger;

        public MessageBrokerController(IMessageBrokerServer server, IMyCustomLogger logger)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            try
            {
                return Ok(new 
                { 
                    status = "healthy", 
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Health check failed", ex);
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }

        [HttpPost("consumers")]
        public async Task<IActionResult> RegisterConsumer([FromBody] ConsumerRegistrationDto registration)
        {
            try
            {
                _logger.LogInfo($"Received registration request for consumer {registration.ConsumerId} " +
                              $"in group {registration.ConsumerGroup} for topic {registration.Topic}");

                if (string.IsNullOrEmpty(registration.Topic))
                    return BadRequest("Topic is required");

                if (string.IsNullOrEmpty(registration.ConsumerGroup))
                    return BadRequest("Consumer group is required");

                if (string.IsNullOrEmpty(registration.ConsumerId))
                    return BadRequest("Consumer ID is required");

                var success = await _server.RegisterConsumerAsync(
                    registration.Topic,
                    registration.ConsumerGroup,
                    registration.ConsumerId);

                if (success)
                {
                    _logger.LogInfo($"Successfully registered consumer {registration.ConsumerId}");
                    return Ok(new 
                    { 
                        status = "registered",
                        consumerId = registration.ConsumerId,
                        timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogError($"Failed to register consumer {registration.ConsumerId}");
                return StatusCode(500, "Failed to register consumer");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error registering consumer: {ex.Message}", ex);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("consumers/{consumerId}")]
        public async Task<IActionResult> UnregisterConsumer(string consumerId)
        {
            try
            {
                _logger.LogInfo($"Received unregistration request for consumer {consumerId}");

                var success = await _server.UnregisterConsumerAsync(consumerId);
                
                if (success)
                {
                    _logger.LogInfo($"Successfully unregistered consumer {consumerId}");
                    return Ok(new { status = "unregistered", timestamp = DateTime.UtcNow });
                }

                _logger.LogError($"Failed to unregister consumer {consumerId}");
                return StatusCode(500, "Failed to unregister consumer");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unregistering consumer: {ex.Message}", ex);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("topics/{topic}/consumers")]
        public async Task<IActionResult> GetConsumers(string topic, [FromQuery] string? consumerGroup = null)
        {
            try
            {
                _logger.LogInfo($"Fetching consumers for topic {topic}" + 
                              (consumerGroup != null ? $" in group {consumerGroup}" : ""));

                var consumers = await _server.GetRegisteredConsumersAsync(topic, consumerGroup ?? "");
                
                return Ok(new
                {
                    topic,
                    consumerGroup,
                    consumers = consumers.ToList(),
                    count = consumers.Count(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting consumers: {ex.Message}", ex);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("topics/{topic}/messages")]
        public async Task<IActionResult> GetMessages(string topic, [FromQuery] string? consumerGroup = null)
        {
            try
            {
                var messages = await _server.GetMessagesAsync(topic, consumerGroup);
                var messageList = messages.Select(m => new MessageDto
                {
                    Id = m.Id.ToString(),
                    Topic = m.Topic,
                    ConsumerGroup = m.ConsumerGroup,
                    Priority = m.Priority,
                    Timestamp = m.Timestamp,
                    Content = System.Text.Encoding.UTF8.GetString(m.Payload ?? Array.Empty<byte>()),
                    Headers = new Dictionary<string, string>()
                }).ToList();

                // Only log if we actually found messages
                if (messageList.Any())
                {
                    _logger.LogInfo($"Delivering {messageList.Count} message(s) to consumer group {consumerGroup} for topic {topic}");
                }

                return Ok(messageList);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving messages for topic {topic}: {ex.Message}", ex);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("messages")]
        public async Task<IActionResult> PublishMessage([FromBody] MessageDto messageDto)
        {
            try
            {
                _logger.LogInfo($"Received message for topic {messageDto.Topic}: {messageDto.Id}");

                if (string.IsNullOrEmpty(messageDto.Topic))
                    return BadRequest("Topic is required");

                if (string.IsNullOrEmpty(messageDto.Content))
                    return BadRequest("Message content is required");

                var message = new Message
                {
                    Id = string.IsNullOrEmpty(messageDto.Id) ? Guid.NewGuid() : Guid.Parse(messageDto.Id),
                    Topic = messageDto.Topic,
                    ConsumerGroup = messageDto.ConsumerGroup ?? "",
                    Priority = messageDto.Priority,
                    Timestamp = messageDto.Timestamp == default ? DateTime.UtcNow : messageDto.Timestamp,
                    Payload = System.Text.Encoding.UTF8.GetBytes(messageDto.Content)
                };

                var success = await _server.PublishMessageAsync(messageDto.Topic, message);

                if (success)
                {
                    _logger.LogInfo($"Successfully published message {message.Id}");
                    return Ok(new { id = message.Id, status = "published" });
                }

                _logger.LogError($"Failed to publish message {message.Id}");
                return StatusCode(500, "Failed to publish message");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing message: {ex.Message}", ex);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
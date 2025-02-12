using System.Net.Http.Json;
using System.Reflection;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;

[MessageBrokerPlugin("DefaultProducerPlugin", "Default message producer implementation")]
[ProducerImplementationData(retryNumber: 3)]
[RateLimit(maxConcurrentThreads: 5)]
public class DefaultProducerPlugin : IMessageBrokerPlugin
{
    private readonly IMyCustomLogger _logger;
    private HttpClient? _httpClient;
    private string _brokerUrl = string.Empty;
    private readonly SemaphoreSlim _rateLimiter;
    private const int RETRY_DELAY_MS = 5000;

    public string Name => "DefaultProducerPlugin";
    public string Version => "1.0.0";

    public DefaultProducerPlugin()
    {
        var logPath = Path.Combine("logs", $"producer_plugin_{DateTime.UtcNow:yyyyMMdd}.log");
        _logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
        
        var rateLimit = GetType().GetCustomAttribute<RateLimit>();
        _rateLimiter = new SemaphoreSlim(rateLimit?.MaxConcurrentThreads ?? 5);
    }

    public void Configure(string brokerUrl, string topic, string consumerGroup)
    {
        _brokerUrl = brokerUrl;
        _httpClient = new HttpClient { BaseAddress = new Uri(_brokerUrl) };
        _logger.LogInfo($"Producer configured with broker URL: {_brokerUrl}");
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(_brokerUrl))
            throw new InvalidOperationException("Broker URL must be configured before initialization");
        _logger.LogInfo($"DefaultProducerPlugin initialized");
    }

    public async Task ExecuteAsync(IMessage message)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var retryAttr = GetType().GetCustomAttribute<ProducerImplementationData>();
            var retryCount = retryAttr?.RetryNumber ?? 3;
            
            while (true)
            {
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        var messageDto = new MessageDto
                        {
                            Id = message.Id.ToString(),
                            Topic = message.Topic,
                            Content = System.Text.Encoding.UTF8.GetString(message.Payload),
                            Timestamp = DateTime.UtcNow
                        };

                        var response = await _httpClient!.PostAsJsonAsync("api/messagebroker/messages", messageDto);
                        response.EnsureSuccessStatusCode();
                        
                        _logger.LogInfo($"Message {message.Id} published successfully");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Attempt {i + 1}/{retryCount} failed: {ex.Message}", ex);
                        if (i < retryCount - 1)
                            await Task.Delay(1000);
                    }
                }
                
                _logger.LogWarning($"All {retryCount} attempts failed, waiting {RETRY_DELAY_MS}ms before trying again");
                await Task.Delay(RETRY_DELAY_MS);
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public void Shutdown()
    {
        _httpClient?.Dispose();
        _rateLimiter.Dispose();
        _logger.LogInfo("DefaultProducerPlugin shutdown");
    }
}

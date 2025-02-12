using System.Reflection;
using MessageBroker.Consumer.Library;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;

[MessageBrokerPlugin("DefaultConsumerPlugin", "Default message consumer implementation")]
[RateLimit(maxConcurrentThreads: 3)]
public class DefaultConsumerPlugin : IMessageBrokerPlugin
{
    private readonly IMyCustomLogger _logger;
    private DefaultConsumer? _consumer;
    private string _topic = string.Empty;
    private string _consumerGroup = string.Empty;
    private string _brokerUrl = string.Empty;
    private readonly SemaphoreSlim _rateLimiter;
    private const int SERVER_DOWN_RETRY_DELAY_MS = 5000;

    public string Name => "DefaultConsumerPlugin";
    public string Version => "1.0.0";

    public DefaultConsumerPlugin()
    {
        var logPath = Path.Combine("logs", $"consumer_plugin_{DateTime.UtcNow:yyyyMMdd}.log");
        _logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
        
        var rateLimit = GetType().GetCustomAttribute<RateLimit>();
        _rateLimiter = new SemaphoreSlim(rateLimit?.MaxConcurrentThreads ?? 3);
    }

    public void Configure(string brokerUrl, string topic, string consumerGroup)
    {
        if (string.IsNullOrEmpty(brokerUrl))
            throw new ArgumentException("Broker URL cannot be empty", nameof(brokerUrl));
            
        _brokerUrl = brokerUrl;
        _topic = topic;
        _consumerGroup = consumerGroup;
        _logger.LogInfo($"Plugin configured with broker URL: {_brokerUrl}");
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(_brokerUrl))
            throw new InvalidOperationException("Plugin must be configured before initialization");
            
        InitializeConsumer();
    }

    private void InitializeConsumer()
    {
        while (true)
        {
            try
            {
                _consumer = new DefaultConsumer(_logger, _topic, _consumerGroup, _brokerUrl);
                _consumer.RegisterAsync().GetAwaiter().GetResult();
                _logger.LogInfo($"DefaultConsumerPlugin initialized for topic: {_topic}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize consumer: {ex.Message}", ex);
                _logger.LogInfo($"Retrying in {SERVER_DOWN_RETRY_DELAY_MS}ms...");
                Thread.Sleep(SERVER_DOWN_RETRY_DELAY_MS);
            }
        }
    }

    public async Task ExecuteAsync(IMessage message)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            while (true)
            {
                try
                {
                    if (_consumer == null)
                    {
                        InitializeConsumer();
                    }
                    
                    await _consumer.ProcessMessageAsync(message);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing message: {ex.Message}", ex);
                    _logger.LogInfo($"Waiting {SERVER_DOWN_RETRY_DELAY_MS}ms before retrying");
                    await Task.Delay(SERVER_DOWN_RETRY_DELAY_MS);
                    
                    _consumer?.Dispose();
                    _consumer = null;
                }
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public void Shutdown()
    {
        _consumer?.Dispose();
        _rateLimiter.Dispose();
        _logger.LogInfo("DefaultConsumer plugin shutdown");
    }
}

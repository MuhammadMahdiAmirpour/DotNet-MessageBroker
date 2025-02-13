using MessageBroker.Core.Attributes;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;
using MessageBroker.Producer.Library;

[MessageBrokerPlugin("DefaultProducerPlugin", "Default message producer implementation")]
[ProducerImplementationData(retryNumber: 3)]
[RateLimit(maxConcurrentThreads: 5)]
public class DefaultProducerPlugin : IMessageBrokerPlugin
{
    private readonly IMyCustomLogger _logger;
    private DefaultProducer? _producer;
    private string _brokerUrl = string.Empty;
    private string _topic = string.Empty;
    private string _consumerGroup = string.Empty;

    public string Name => "DefaultProducerPlugin";
    public string Version => "1.0.0";

    public DefaultProducerPlugin()
    {
        var logPath = Path.Combine("logs", $"producer_plugin_{DateTime.UtcNow:yyyyMMdd}.log");
        _logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
    }

    public void Configure(string brokerUrl, string topic, string consumerGroup)
    {
        _brokerUrl = brokerUrl;
        _topic = topic;
        _consumerGroup = consumerGroup;
        _producer = new DefaultProducer(_logger, _brokerUrl);
        _logger.LogInfo($"Producer configured with broker URL: {_brokerUrl}, topic: {_topic}, consumer group: {_consumerGroup}");
    }

    public void Initialize()
    {
        if (_producer == null)
            throw new InvalidOperationException("Producer must be configured before initialization");
            
        _producer.InitializeAsync().GetAwaiter().GetResult();
        _logger.LogInfo($"DefaultProducerPlugin initialized");
    }

    public async Task ExecuteAsync(IMessage message)
    {
        if (_producer == null)
            throw new InvalidOperationException("Producer not initialized");

        await _producer.ProduceMessageAsync(message);
    }

    public void Shutdown()
    {
        _producer?.Dispose();
        _logger.LogInfo("DefaultProducerPlugin shutdown");
    }
}

using System.CommandLine;
using MessageBroker.Logging;

namespace MessageBroker.Consumer.App;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        var topicOption = new Option<string>(
            "--topic",
            getDefaultValue: () => "test-topic",
            description: "The topic to consume messages from"
        );

        var groupOption = new Option<string>(
            "--group",
            getDefaultValue: () => "test-group",
            description: "The consumer group name"
        );

        var urlOption = new Option<string>(
            "--url",
            getDefaultValue: () => "http://localhost:5000",
            description: "The message broker URL"
        );

        var rootCommand = new RootCommand("Message Broker Consumer Application")
        {
            topicOption,
            groupOption,
            urlOption
        };

        rootCommand.SetHandler(async (string topic, string group, string url) =>
        {
            await RunConsumer(topic, group, url);
        }, topicOption, groupOption, urlOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunConsumer(string topic, string group, string brokerUrl)
    {
        var startTime = DateTime.UtcNow;
        var currentUser = Environment.UserName;

        // Setup logger
        IMyCustomLogger logger = new MyCustomLogger("consumer.log", MyCustomLogLevel.Info);
        logger.LogInfo($"Starting consumer application at {startTime:yyyy-MM-dd HH:mm:ss}");
        logger.LogInfo($"User: {currentUser}");
        logger.LogInfo($"Broker URL: {brokerUrl}");
        logger.LogInfo($"Topic: {topic}");
        logger.LogInfo($"Group: {group}");

        DefaultConsumer? consumer = null;

        try
        {
            // Create consumer instance
            logger.LogInfo("Creating consumer instance");
            consumer = new DefaultConsumer(logger, topic, group, brokerUrl);

            // Subscribe to message events
            consumer.OnMessageReceived += (sender, message) =>
            {
                var content = System.Text.Encoding.UTF8.GetString(message.Payload);
                Console.WriteLine($"\nReceived message on topic '{message.Topic}':");
                Console.WriteLine($"ID: {message.Id}");
                Console.WriteLine($"Content: {content}");
                Console.WriteLine($"Timestamp: {message.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("----------------------------------------");
            };

            // Check connection before attempting to register
            logger.LogInfo("Checking connection to broker...");
            if (!await consumer.IsConnectedAsync())
            {
                throw new Exception($"Failed to connect to message broker at {brokerUrl}");
            }

            logger.LogInfo("Successfully connected to broker");

            // Register consumer with broker
            logger.LogInfo("Registering consumer with broker...");
            if (!await consumer.RegisterAsync())
            {
                throw new Exception("Failed to register consumer with broker");
            }

            logger.LogInfo("Consumer registered successfully");
            logger.LogInfo("Starting to poll for messages...");

            // Set up cancellation token to handle application shutdown
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.LogInfo("Shutdown requested by user");
            };

            // Start polling for messages
            await consumer.StartPollingAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer error: {ex.Message}", ex);
            logger.LogError($"Fatal error at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\nError: {ex.Message}", ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            if (consumer != null)
            {
                try
                {
                    consumer.Dispose();
                    logger.LogInfo("Consumer disposed");
                }
                catch (Exception ex)
                {
                    logger.LogError("Error disposing consumer", ex);
                }
            }
            logger.LogInfo("Application shutting down");
        }
    }
}
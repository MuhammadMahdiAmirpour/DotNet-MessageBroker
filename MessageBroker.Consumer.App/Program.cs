using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;
using System.CommandLine;
using System.Reflection;

namespace MessageBroker.Consumer.App
{
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

            rootCommand.SetHandler(async (topic, group, url) =>
            {
                await RunConsumer(topic, group, url);
            }, topicOption, groupOption, urlOption);

            return await rootCommand.InvokeAsync(args);
        }

        static async Task RunConsumer(string topic, string group, string brokerUrl)
        {
            var startTime = DateTime.UtcNow;
            var currentUser = "MuhammadMahdiAmirpour";
            IConsumer? consumer = null;
            IMyCustomLogger? logger = null;

            try
            {
                // Create logs directory if it doesn't exist
                var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);

                // Setup logger
                var logFileName = $"consumer_{currentUser}_{startTime:yyyyMMdd}.log";
                var logPath = Path.Combine(logsDir, logFileName);
                
                logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
                logger.LogInfo($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {startTime:yyyy-MM-dd HH:mm:ss}");
                logger.LogInfo($"Current User's Login: {currentUser}");
                logger.LogInfo($"Topic: {topic}");
                logger.LogInfo($"Group: {group}");
                logger.LogInfo($"Broker URL: {brokerUrl}");

                // Load consumer library dynamically
                var consumerAssembly = Assembly.Load("MessageBroker.Consumer.Library");
                var consumerType = consumerAssembly.GetType("MessageBroker.Consumer.Library.DefaultConsumer");
                
                if (consumerType == null)
                {
                    throw new Exception("Could not find DefaultConsumer type in library");
                }

                // Create consumer instance using reflection
                consumer = (IConsumer)Activator.CreateInstance(consumerType, 
                    new object[] { logger, topic, group, brokerUrl });
                
                if (consumer == null)
                {
                    throw new Exception("Failed to create consumer instance");
                }

                // Get required methods using reflection
                var isConnectedMethod = consumerType.GetMethod("IsConnectedAsync");
                var registerMethod = consumerType.GetMethod("RegisterAsync");

                if (isConnectedMethod == null || registerMethod == null)
                {
                    throw new Exception("Could not find required methods on consumer instance");
                }

                // Subscribe to consumer errors
                consumer.OnError += (sender, ex) =>
                {
                    logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer error: {ex.Message}", ex);
                };

                // Check connection
                var isConnected = await (Task<bool>)isConnectedMethod.Invoke(consumer, null);
                if (!isConnected)
                {
                    throw new Exception("Failed to connect to message broker");
                }

                // Register consumer
                var isRegistered = await (Task<bool>)registerMethod.Invoke(consumer, null);
                if (!isRegistered)
                {
                    throw new Exception("Failed to register consumer");
                }

                logger.LogInfo("Consumer started successfully");
                Console.WriteLine("\nConsumer started successfully!");
                Console.WriteLine($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Current User's Login: {currentUser}");
                Console.WriteLine($"Topic: {topic}");
                Console.WriteLine($"Group: {group}");
                Console.WriteLine("\nPress Ctrl+C to stop the consumer...");

                // Setup cancellation
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Shutdown requested by user");
                };

                try
                {
                    // Keep the application running until cancelled
                    await Task.Delay(-1, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer stopping...");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Fatal error: {ex.Message}", ex);
                Console.WriteLine($"\nFatal error at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
                Environment.Exit(1);
            }
            finally
            {
                if (consumer != null)
                {
                    var disposeMethod = consumer.GetType().GetMethod("Dispose");
                    disposeMethod?.Invoke(consumer, null);
                }
                logger?.LogInfo($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Consumer application shutting down");
            }
        }
    }
}
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;
using System.Text;

namespace MessageBroker.Producer.App
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var startTime = DateTime.UtcNow;
            var currentUser = Environment.UserName;
            IProducer? producer = null;
            IMyCustomLogger? logger = null;

            try
            {
                // Parse command line arguments with defaults
                var brokerUrl = GetArgValue(args, "--broker") ?? "http://localhost:5000";
                var topic = GetArgValue(args, "--topic") ?? "test-topic";

                // Create logs directory if it doesn't exist
                var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);

                // Setup logger
                var logFileName = $"producer_{currentUser}_{startTime:yyyyMMdd}.log";
                var logPath = Path.Combine(logsDir, logFileName);
                
                logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
                logger.LogInfo($"Starting producer application at {startTime:yyyy-MM-dd HH:mm:ss}");
                logger.LogInfo($"User: {currentUser}");
                logger.LogInfo($"Broker URL: {brokerUrl}");
                logger.LogInfo($"Topic: {topic}");

                // Create producer instance
                logger.LogInfo("Creating producer instance");
                producer = new DefaultProducer(logger, topic, brokerUrl);

                // Subscribe to producer errors
                producer.OnError += (sender, ex) =>
                {
                    logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Producer error: {ex.Message}", ex);
                };

                // Check connection
                logger.LogInfo("Checking connection to broker...");
                if (!await producer.IsConnectedAsync())
                {
                    throw new Exception($"Failed to connect to message broker at {brokerUrl}");
                }

                logger.LogInfo("Producer started successfully");
                Console.WriteLine("\nProducer started successfully!");
                Console.WriteLine($"Current Time (UTC): {startTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"User: {currentUser}");
                Console.WriteLine($"Topic: {topic}");
                Console.WriteLine("\nPress 'Q' to quit, 'Enter' to send a message, 'I' for information");

                // Main loop
                while (true)
                {
                    Console.Write("Enter message content (or Q to quit, I for info): ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input))
                        continue;

                    if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"Shutdown requested by user at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                        break;
                    }

                    if (input.Equals("I", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nProducer Status:");
                        Console.WriteLine($"Current Time (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine($"User: {currentUser}");
                        Console.WriteLine($"Topic: {topic}");
                        Console.WriteLine($"Broker URL: {brokerUrl}");
                        Console.WriteLine($"Connected: {await producer.IsConnectedAsync()}");
                        Console.WriteLine();
                        continue;
                    }

                    var message = new Message
                    {
                        Id = Guid.NewGuid(),
                        Topic = topic,
                        ConsumerGroup = string.Empty,
                        Priority = 0,
                        Payload = Encoding.UTF8.GetBytes(input),
                        Timestamp = DateTime.UtcNow
                    };

                    var success = await producer.ProduceAsync(message);
                    if (success)
                    {
                        logger.LogInfo($"Message sent successfully: {message.Id}");
                        Console.WriteLine($"Message sent successfully! ID: {message.Id}");
                    }
                    else
                    {
                        logger.LogError($"Failed to send message: {message.Id}", new Exception("Message sending failed"));
                        Console.WriteLine("Failed to send message.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"Fatal error at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\nError: {ex.Message}", ex);
                Console.WriteLine($"Fatal error at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
            finally
            {
                producer?.Dispose();
            }
        }

        private static string? GetArgValue(string[] args, string key)
        {
            var index = Array.IndexOf(args, key);
            if (index >= 0 && index < args.Length - 1)
                return args[index + 1];
            return null;
        }
    }
}
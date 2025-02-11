using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Models;
using MessageBroker.Logging;
using System.Reflection;
using System.Text;

namespace MessageBroker.Producer.App
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var startTime = DateTime.UtcNow;
            var currentUser = "MuhammadMahdiAmirpour";
            IProducer? producer = null;
            IMyCustomLogger? logger = null;

            try
            {
                // Parse command line arguments with defaults
                var brokerUrl = GetArgValue(args, "--broker") ?? "http://localhost:5000";

                // Create logs directory if it doesn't exist
                var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);

                // Setup logger
                var logFileName = $"producer_{currentUser}_{startTime:yyyyMMdd}.log";
                var logPath = Path.Combine(logsDir, logFileName);
                
                logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
                logger.LogInfo($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {startTime:yyyy-MM-dd HH:mm:ss}");
                logger.LogInfo($"Current User's Login: {currentUser}");
                logger.LogInfo($"Broker URL: {brokerUrl}");

                // Load producer library dynamically
                var producerAssembly = Assembly.Load("MessageBroker.Producer.Library");
                var producerType = producerAssembly.GetType("MessageBroker.Producer.Library.DefaultProducer");
                
                if (producerType == null)
                {
                    throw new Exception("Could not find DefaultProducer type in library");
                }

                // Create producer instance using reflection
                producer = (IProducer)Activator.CreateInstance(producerType, new object[] { logger, brokerUrl });
                
                if (producer == null)
                {
                    throw new Exception("Failed to create producer instance");
                }

                // Get InitializeAsync method using reflection
                var initializeMethod = producerType.GetMethod("InitializeAsync");
                if (initializeMethod == null)
                {
                    throw new Exception("Could not find InitializeAsync method");
                }

                // Initialize the producer
                await (Task)initializeMethod.Invoke(producer, null);

                // Subscribe to producer errors
                producer.OnError += (sender, ex) =>
                {
                    logger.LogError($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Producer error: {ex.Message}", ex);
                };

                logger.LogInfo("Producer started successfully");
                Console.WriteLine("\nProducer started successfully!");
                Console.WriteLine($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Current User's Login: {currentUser}");
                Console.WriteLine("\nEnter 'Q' to quit, topic:message to send, 'I' for information");

                while (true)
                {
                    Console.Write("\nEnter command: ");
                    var input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                        continue;

                    if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"Shutdown requested by user");
                        break;
                    }

                    if (input.Equals("I", StringComparison.OrdinalIgnoreCase))
                    {
                        var isConnectedMethod = producerType.GetMethod("IsConnectedAsync");
                        var isConnected = await (Task<bool>)isConnectedMethod.Invoke(producer, null);

                        Console.WriteLine("\nProducer Status:");
                        Console.WriteLine($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine($"Current User's Login: {currentUser}");
                        Console.WriteLine($"Broker URL: {brokerUrl}");
                        Console.WriteLine($"Connected: {isConnected}");
                        continue;
                    }

                    var parts = input.Split(':', 2);
                    if (parts.Length != 2)
                    {
                        Console.WriteLine("Invalid format. Use 'topic:message'");
                        continue;
                    }

                    var message = new Message
                    {
                        Id = Guid.NewGuid(),
                        Topic = parts[0].Trim(),
                        Payload = Encoding.UTF8.GetBytes(parts[1].Trim()),
                        Timestamp = DateTime.UtcNow
                    };

                    var produceMethod = producerType.GetMethod("ProduceAsync");
                    var success = await (Task<bool>)produceMethod.Invoke(producer, new object[] { message });

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
                logger?.LogError($"Fatal error: {ex.Message}", ex);
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
                if (producer != null)
                {
                    var disposeMethod = producer.GetType().GetMethod("Dispose");
                    disposeMethod?.Invoke(producer, null);
                }
                logger?.LogInfo("Producer application shutting down");
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
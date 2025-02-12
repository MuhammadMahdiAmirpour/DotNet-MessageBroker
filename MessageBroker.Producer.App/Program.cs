using MessageBroker.Core.Models;
using MessageBroker.Logging;
using System.Reflection;
using System.Text;
using MessageBroker.Producer.Library;

namespace MessageBroker.Producer.App;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var startTime = DateTime.UtcNow;
        var currentUser = Environment.UserName;
        DefaultProducer? producer = null;
        IMyCustomLogger? logger = null;

        try
        {
            Console.WriteLine("\n=== Message Broker Producer ===");
            Console.WriteLine("This application will send messages to a specific topic.");
            Console.WriteLine("All consumer groups listening to your topic will receive these messages.\n");

            Console.Write("Enter topic name (default: test-topic): ");
            var topic = Console.ReadLine()?.Trim() ?? "test-topic";

            Console.Write("Enter number of messages to send (default: 1000): ");
            var messageCountStr = Console.ReadLine()?.Trim() ?? "1000";
            var messageCount = int.TryParse(messageCountStr, out var n) ? n : 1000;

            Console.WriteLine($"\nConfiguration:");
            Console.WriteLine($"- Topic: '{topic}'");
            Console.WriteLine($"- Messages to send: {messageCount}");
            Console.WriteLine($"- Current user: {currentUser}");
            
            Console.Write("\nPress Enter to start sending messages...");
            Console.ReadLine();

            var brokerUrl = GetArgValue(args, "--broker") ?? "http://localhost:5000";
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);

            var logFileName = $"producer_{currentUser}_{startTime:yyyyMMdd}.log";
            var logPath = Path.Combine(logsDir, logFileName);
            
            logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
            logger.LogInfo($"Starting producer at: {startTime:yyyy-MM-dd HH:mm:ss}");

            var producerAssembly = Assembly.Load("MessageBroker.Producer.Library");
            var producerType = producerAssembly.GetType("MessageBroker.Producer.Library.DefaultProducer");
            
            producer = (DefaultProducer)Activator.CreateInstance(
                type: producerType,
                args: [logger, brokerUrl]
            );

            
            if (producer != null)
            {
                await producer.InitializeAsync();
                producer.OnError += (sender, ex) => { logger.LogError($"Producer error: {ex.Message}", ex); };

                Console.WriteLine($"\nStarting to send {messageCount} messages to topic '{topic}'...\n");
                var successCount = 0;
                var failCount = 0;

                for (int i = 1; i <= messageCount; i++)
                {
                    var message = new Message
                    {
                        Id = Guid.NewGuid(),
                        Topic = topic,
                        Payload = Encoding.UTF8.GetBytes(
                            $"Message {i}/{messageCount} for topic '{topic}' sent by {currentUser} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"),
                        Timestamp = DateTime.UtcNow
                    };

                    var success = await producer.ProduceAsync(message);

                    if (success)
                    {
                        successCount++;
                        logger.LogInfo($"Message {i}/{messageCount} sent successfully. ID: {message.Id}");
                    }
                    else
                    {
                        failCount++;
                        logger.LogError($"Failed to send message {i}/{messageCount}. ID: {message.Id}",
                            new Exception("Send failed"));
                    }

                    if (i % 100 == 0)
                    {
                        Console.WriteLine(
                            $"Progress: {i}/{messageCount} messages sent. Success: {successCount}, Failed: {failCount}");
                    }

                    await Task.Delay(100); // Rate limiting
                }

                Console.WriteLine("\nFinished sending messages!");
                Console.WriteLine($"Total Success: {successCount}");
                Console.WriteLine($"Total Failed: {failCount}");
                logger.LogInfo($"Completed sending messages. Success: {successCount}, Failed: {failCount}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"Fatal error: {ex.Message}", ex);
            Console.WriteLine($"\nFatal error: {ex.Message}");
            Environment.Exit(1);
        }
        finally
        {
            producer?.Dispose();
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

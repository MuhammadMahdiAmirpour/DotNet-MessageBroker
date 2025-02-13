using MessageBroker.Core.Models;
using MessageBroker.Core.Plugin;
using MessageBroker.Logging;
using System.Text;

namespace MessageBroker.Producer.App;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var startTime = DateTime.UtcNow;
        var currentUser = Environment.UserName;
        IMyCustomLogger? logger = null;

        try
        {
            Console.WriteLine("\n=== Message Broker Producer ===");
            Console.WriteLine("This application will send messages to multiple topics.");
            Console.WriteLine("All consumer groups listening to these topics will receive these messages.\n");

            Console.Write("Enter topics (comma-separated) to send messages to (default: analytics,logging): ");
            var topicsInput = Console.ReadLine()?.Trim() ?? "analytics,logging";
            var topics = topicsInput.Split(',').Select(t => t.Trim()).ToList();

            Console.Write("Enter number of messages to send per topic (default: 1000): ");
            var messageCountStr = Console.ReadLine()?.Trim() ?? "1000";
            var messageCount = int.TryParse(messageCountStr, out var n) ? n : 1000;

            Console.WriteLine($"\nConfiguration:");
            Console.WriteLine($"- Topics: {string.Join(", ", topics)}");
            Console.WriteLine($"- Messages to send per topic: {messageCount}");
            Console.WriteLine($"- Current user: {currentUser}");
            
            Console.Write("\nPress Enter to start sending messages...");
            Console.ReadLine();

            var brokerUrl = GetArgValue(args, "--broker") ?? "http://localhost:5000";
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            Directory.CreateDirectory(logsDir);
            Directory.CreateDirectory(pluginsDir);

            var logFileName = $"producer_{currentUser}_{startTime:yyyyMMdd}.log";
            var logPath = Path.Combine(logsDir, logFileName);
            
            logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
            logger.LogInfo($"Starting producer at: {startTime:yyyy-MM-dd HH:mm:ss}");

            var pluginLoader = new PluginLoader(logger, pluginsDir);
            await pluginLoader.LoadPluginsAsync();

            var producers = topics.ToDictionary(
                topic => topic,
                topic => pluginLoader.GetPlugin("DefaultProducerPlugin", brokerUrl, topic, "producer-group")
            );

            foreach(var producer in producers.Values)
            {
                producer.Initialize();
            }

            Console.WriteLine($"\nStarting to send {messageCount} messages to {topics.Count} topics...\n");
            var successCount = 0;
            var failCount = 0;
            var totalMessages = messageCount * topics.Count;

            for (var i = 1; i <= messageCount; i++)
            {
                foreach (var topic in topics)
                {
                    var message = new Message
                    {
                        Id = Guid.NewGuid(),
                        Topic = topic,
                        Payload = Encoding.UTF8.GetBytes(
                            $"Message {i}/{messageCount} for topic '{topic}' sent by {currentUser} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"),
                        Timestamp = DateTime.UtcNow
                    };

                    try
                    {
                        await producers[topic].ExecuteAsync(message);
                        successCount++;
                        logger.LogInfo($"Message {i}/{messageCount} sent successfully to topic '{topic}'. ID: {message.Id}");
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        logger.LogError($"Failed to send message {i}/{messageCount} to topic '{topic}'. ID: {message.Id}", ex);
                    }
                }

                if (i % 100 == 0)
                {
                    var progress = (i * topics.Count);
                    Console.WriteLine($"Progress: {progress}/{totalMessages} messages sent. Success: {successCount}, Failed: {failCount}");
                }

                await Task.Delay(100); // Rate limiting
            }

            Console.WriteLine("\nFinished sending messages!");
            Console.WriteLine($"Total Success: {successCount}");
            Console.WriteLine($"Total Failed: {failCount}");
            logger.LogInfo($"Completed sending messages. Success: {successCount}, Failed: {failCount}");
        }
        catch (Exception ex)
        {
            logger?.LogError($"Fatal error: {ex.Message}", ex);
            Console.WriteLine($"\nFatal error: {ex.Message}");
            Environment.Exit(1);
        }
        finally
        {
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

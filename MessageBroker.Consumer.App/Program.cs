using MessageBroker.Consumer.Library;
using MessageBroker.Logging;
using System.Reflection;

namespace MessageBroker.Consumer.App;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("\n=== Message Broker Consumer ===");
        Console.WriteLine("This application will receive messages from a specific topic.");
        Console.WriteLine("Consumers in the same group share the workload - each message goes to only one consumer in the group.");
        Console.WriteLine("Different groups get their own copy of all messages.\n");

        Console.Write("Enter topic to listen to (default: test-topic): ");
        var topic = Console.ReadLine()?.Trim() ?? "test-topic";

        Console.Write("Enter consumer group name (default: test-group): ");
        var group = Console.ReadLine()?.Trim() ?? "test-group";

        var brokerUrl = GetArgValue(args, "--broker") ?? "http://localhost:5000";

        Console.WriteLine($"\nConfiguration:");
        Console.WriteLine($"- Topic: '{topic}'");
        Console.WriteLine($"- Consumer Group: '{group}'");
        Console.WriteLine($"- Broker URL: {brokerUrl}");

        Console.Write("\nPress Enter to start consuming messages...");
        Console.ReadLine();

        await RunConsumers(topic, group, brokerUrl);
    }

    private static async Task RunConsumers(string topic, string group, string brokerUrl)
    {
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        var logger = new MyCustomLogger(Path.Combine(logsDir, $"consumer_main_{DateTime.UtcNow:yyyyMMdd}.log"), MyCustomLogLevel.Info);
        var cts = new CancellationTokenSource();

        var consumerAssembly = Assembly.Load("MessageBroker.Consumer.Library");
        var consumerType = consumerAssembly.GetType("MessageBroker.Consumer.Library.DefaultConsumer");

        Console.WriteLine("\nStarting consumer...");

        var consumerLogger = new MyCustomLogger(Path.Combine(logsDir, $"consumer_{DateTime.UtcNow:yyyyMMdd}.log"), MyCustomLogLevel.Info);
        var consumer = (DefaultConsumer)Activator.CreateInstance(
            type: consumerType!,
            args: [consumerLogger, topic, group, brokerUrl])!;

        try 
        {
            if (await consumer.IsConnectedAsync() && await consumer.RegisterAsync())
            {
                Console.WriteLine("Consumer started successfully.");
                Console.WriteLine("Press Ctrl+C to stop...\n");

                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogInfo("Shutdown requested");
                    Console.WriteLine("\nShutdown requested. Cleaning up...");
                }
            }
            else
            {
                Console.WriteLine("Consumer could not be started. Check broker connection and try again.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to start consumer: {ex.Message}", ex);
        }
        finally
        {
            consumer.Dispose();
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

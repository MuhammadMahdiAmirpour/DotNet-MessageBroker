🚀 Message Broker System

    A high-performance, plugin-based message broker system with multi-consumer support

🌟 Features

    🔌 Plugin-based architecture
    🔄 Multiple producer/consumer support
    ⚡ Configurable thread count per plugin
    🔁 Automatic retry mechanism
    🎚️ Rate limiting
    📝 Detailed logging
    📨 Multiple topic support
    ⚖️ Consumer group load balancing

🏗️ Project Structure

MessageBroker/
├── MessageBroker.API                    # Message broker server
├── MessageBroker.Consumer.App           # Consumer application
├── MessageBroker.Consumer.Library       # Consumer core library
├── MessageBroker.Core                   # Core interfaces and models
├── MessageBroker.Logging                # Logging infrastructure
├── MessageBroker.Plugins.DefaultConsumer # Default consumer plugin
├── MessageBroker.Plugins.DefaultProducer # Default producer plugin
├── MessageBroker.Producer.App           # Producer application
├── MessageBroker.Producer.Library       # Producer core library
└── MessageBroker.Storage                # Message storage implementation

🚀 Quick Start

    Launch the Message Broker API

cd MessageBroker.API
dotnet run

    Start Consumer(s)

cd MessageBroker.Consumer.App
dotnet run

    💡 Enter topic name and consumer group when prompted

    Start Producer

cd MessageBroker.Producer.App
dotnet run

    💡 Enter topics to send messages to when prompted

⚙️ Configuration Examples
🔄 Multiple Consumer Setup

# Analytics Consumer
dotnet run
Topic: orders
Group: analytics-group

# Logging Consumer
dotnet run
Topic: orders
Group: logging-group

📨 Multi-Topic Producer

dotnet run
Topics: orders,analytics,logging
Messages: 1000

🔍 Monitoring
Component	Location
Logs	logs directory
Status	GET /api/messagebroker/status
Topics	GET /api/messagebroker/topics
Consumers	GET /api/messagebroker/consumers
🛠️ Performance Tuning

// Increase thread count for higher throughput
[RateLimit(maxConcurrentThreads: 10)]
public class HighPerformancePlugin : IMessageBrokerPlugin
{
    // Implementation
}

📈 Best Practices

    Start API before consumers and producers
    Use different consumer groups for different processing needs
    Monitor logs for performance optimization
    Implement proper error handling in plugins

🤝 Contributing

    Fork repository
    Create feature branch
    Commit changes
    Push to branch
    Create Pull Request

📄 License

MIT License - feel free to use in your projects!

Built with ❤️ using .NET 9.0

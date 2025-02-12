namespace MessageBroker.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class RateLimit(int maxConcurrentThreads) : Attribute
{
    public int MaxConcurrentThreads { get; } = maxConcurrentThreads;
}
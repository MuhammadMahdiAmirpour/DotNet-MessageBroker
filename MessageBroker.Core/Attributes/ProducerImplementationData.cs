namespace MessageBroker.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ProducerImplementationData(int retryNumber) : Attribute
{
    public int RetryNumber { get; } = retryNumber;
}


namespace MessageBroker.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ProducerImplementationData : Attribute
    {
        public int RetryNumber { get; }

        public ProducerImplementationData(int retryNumber)
        {
            RetryNumber = retryNumber;
        }
    }
}
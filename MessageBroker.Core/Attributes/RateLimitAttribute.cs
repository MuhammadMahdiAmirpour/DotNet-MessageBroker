namespace MessageBroker.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RateLimitAttribute : Attribute
    {
        public int ConcurrentThreads { get; set; }

        public RateLimitAttribute(int concurrentThreads)
        {
            if (concurrentThreads <= 0)
                throw new ArgumentException("Concurrent threads must be greater than 0");
            
            ConcurrentThreads = concurrentThreads;
        }
    }
}
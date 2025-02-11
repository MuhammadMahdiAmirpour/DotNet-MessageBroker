using System.Collections.Concurrent;
using MessageBroker.Core.Interfaces;
using MessageBroker.Core.Attributes;
using MessageBroker.Logging;

namespace MessageBroker.Core.Base
{
    public abstract class ThreadedConsumerBase : IConsumer
    {
        protected readonly IMyCustomLogger MyCustomLogger;
        protected readonly BlockingCollection<IMessage> _messageQueue;
        protected readonly CancellationTokenSource _cancellationTokenSource;
        protected readonly Task[] _workerTasks;
        private bool _disposed;
        
        public event EventHandler<Exception> OnError = delegate { };
        public string ConsumerGroup { get; }

        protected ThreadedConsumerBase(IMyCustomLogger myCustomLogger, string consumerGroup)
        {
            MyCustomLogger = myCustomLogger ?? throw new ArgumentNullException(nameof(myCustomLogger));
            ConsumerGroup = consumerGroup ?? throw new ArgumentNullException(nameof(consumerGroup));
            _messageQueue = new BlockingCollection<IMessage>();
            _cancellationTokenSource = new CancellationTokenSource();
            _disposed = false;

            var rateLimitAttr = (RateLimitAttribute)Attribute.GetCustomAttribute(
                GetType(), typeof(RateLimitAttribute));
            
            if (rateLimitAttr == null)
                throw new InvalidOperationException("Consumer must specify RateLimit attribute");

            var threadCount = rateLimitAttr.ConcurrentThreads;
            _workerTasks = new Task[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                _workerTasks[i] = Task.Run(() => WorkerThread(_cancellationTokenSource.Token));
            }

            MyCustomLogger.LogInfo("Consumer " + ConsumerGroup + " started with " + threadCount + " worker threads");
        }

        protected virtual void RaiseOnError(Exception ex)
        {
            OnError(this, ex);
        }

        private async Task WorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_messageQueue.TryTake(out var message, -1, cancellationToken))
                    {
                        await ProcessMessageAsync(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MyCustomLogger.LogError("Error in consumer worker thread: " + ex.Message, ex);
                    RaiseOnError(ex);
                }
            }
        }

        protected abstract Task<bool> ProcessMessageAsync(IMessage message);

        public Task<bool> ConsumeAsync(IMessage message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadedConsumerBase));

            _messageQueue.Add(message);
            return Task.FromResult(true);
        }

        public abstract Task<bool> IsConnectedAsync();

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();
                    Task.WhenAll(_workerTasks).Wait();
                    _messageQueue.Dispose();
                    _cancellationTokenSource.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ThreadedConsumerBase()
        {
            Dispose(false);
        }
    }
}
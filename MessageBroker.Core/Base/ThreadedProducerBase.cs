using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;

namespace MessageBroker.Core.Base
{
    public abstract class ThreadedProducerBase : IProducer
    {
        protected readonly IMyCustomLogger Logger;
        private bool _disposed;

        public event EventHandler<Exception>? OnError;

        protected ThreadedProducerBase(IMyCustomLogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ProduceAsync(IMessage message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            return await ProduceMessageAsync(message);
        }

        public abstract Task<bool> ProduceMessageAsync(IMessage message);

        public abstract Task<bool> IsConnectedAsync();

        protected virtual void RaiseOnError(Exception ex)
        {
            OnError?.Invoke(this, ex);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
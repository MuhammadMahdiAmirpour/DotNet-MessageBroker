namespace MessageBroker.Logging
{
    public interface IMyCustomLogger
    {
        void Log(MyCustomLogLevel level, string message);
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
        void LogCritical(string message);
        void LogFatal(string message, Exception? ex = null);
    }
}
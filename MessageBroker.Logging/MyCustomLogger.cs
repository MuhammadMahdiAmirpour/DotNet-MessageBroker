using System.Collections.Concurrent;

namespace MessageBroker.Logging;

public class MyCustomLogger : IMyCustomLogger
{
    private readonly string _logFilePath;
    private readonly MyCustomLogLevel _minimumLogLevel;
    private readonly ConcurrentQueue<string> _logQueue;
    private readonly int _maxQueueSize;
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private bool _isDisposed;

    public MyCustomLogger(string logFilePath, MyCustomLogLevel minimumLogLevel)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        _minimumLogLevel = minimumLogLevel;
        _logQueue = new ConcurrentQueue<string>();
        _maxQueueSize = 1000; // Configure based on your needs

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Initialize timer to flush logs every 5 seconds
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public void Log(MyCustomLogLevel level, string message)
    {
        if (level >= _minimumLogLevel)
        {
            var logEntry = FormatLogMessage(level, message);
            EnqueueLog(logEntry);
                
            // Write immediately to console
            WriteToConsole(logEntry, level);
                
            // Flush immediately for Critical and Error levels
            if (level >= MyCustomLogLevel.Error)
            {
                FlushLogs(null);
            }
        }
    }

    public void LogDebug(string message) => Log(MyCustomLogLevel.Debug, message);
    public void LogInfo(string message) => Log(MyCustomLogLevel.Info, message);
    public void LogWarning(string message) => Log(MyCustomLogLevel.Warning, message);
        
    public void LogError(string message, Exception? ex = null)
    {
        var fullMessage = ex != null 
            ? $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}"
            : message;
        Log(MyCustomLogLevel.Error, fullMessage);
    }
        
    public void LogCritical(string message) => Log(MyCustomLogLevel.Critical, message);
        
    public void LogFatal(string message, Exception? ex = null)
    {
        var fullMessage = ex != null
            ? $"FATAL: {message}\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}"
            : $"FATAL: {message}";
        Log(MyCustomLogLevel.Critical, fullMessage);
        FlushLogs(null); // Ensure fatal errors are written immediately
    }

    private string FormatLogMessage(MyCustomLogLevel level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        return $"{timestamp} [{level.ToString().ToUpper()}] {message}";
    }

    private void EnqueueLog(string logEntry)
    {
        _logQueue.Enqueue(logEntry);
            
        // If queue is too large, force a flush
        if (_logQueue.Count >= _maxQueueSize)
        {
            FlushLogs(null);
        }
    }

    private void WriteToConsole(string message, MyCustomLogLevel level)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = GetColorForLogLevel(level);
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    private ConsoleColor GetColorForLogLevel(MyCustomLogLevel level) => level switch
    {
        MyCustomLogLevel.Debug => ConsoleColor.Gray,
        MyCustomLogLevel.Info => ConsoleColor.White,
        MyCustomLogLevel.Warning => ConsoleColor.Yellow,
        MyCustomLogLevel.Error => ConsoleColor.Red,
        MyCustomLogLevel.Critical => ConsoleColor.DarkRed,
        _ => ConsoleColor.White
    };

    private void FlushLogs(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var logsToWrite = new List<string>();
            while (_logQueue.TryDequeue(out var log))
            {
                logsToWrite.Add(log);
            }

            if (logsToWrite.Count > 0)
            {
                lock (_writeLock)
                {
                    File.AppendAllLines(_logFilePath, logsToWrite);
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to flush logs to file: {ex.Message}";
            Console.WriteLine(errorMessage);
                
            // Try to write failed logs to a backup file
            try
            {
                var backupPath = _logFilePath + ".backup";
                File.AppendAllText(backupPath, $"\n{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [ERROR] {errorMessage}");
            }
            catch
            {
                // If backup fails, we can't do much more
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
            
        _isDisposed = true;
        FlushLogs(null); // Final flush
        _flushTimer.Dispose();
    }
}
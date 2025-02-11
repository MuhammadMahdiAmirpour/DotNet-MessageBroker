using System.Reflection;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;

namespace MessageBroker.Core.DynamicLoading;

public class AssemblyLoader
{
    private readonly IMyCustomLogger _myCustomLogger;

    public AssemblyLoader(IMyCustomLogger myCustomLogger)
    {
        _myCustomLogger = myCustomLogger;
    }

    public IEnumerable<Type> LoadProducers(string dllPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            return assembly.GetTypes()
                .Where(t => typeof(IProducer).IsAssignableFrom(t) && !t.IsInterface);
        }
        catch (Exception ex)
        {
            _myCustomLogger.LogError($"Error loading producer assembly: {ex.Message}", ex);
            throw;
        }
    }

    public IEnumerable<Type> LoadConsumers(string dllPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            return assembly.GetTypes()
                .Where(t => typeof(IConsumer).IsAssignableFrom(t) && !t.IsInterface);
        }
        catch (Exception ex)
        {
            _myCustomLogger.LogError($"Error loading consumer assembly: {ex.Message}", ex);
            throw;
        }
    }
}
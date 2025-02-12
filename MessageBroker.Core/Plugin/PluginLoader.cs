using System.Reflection;
using MessageBroker.Core.Attributes;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;

namespace MessageBroker.Core.Plugin;

public class PluginLoader(IMyCustomLogger logger, string pluginPath)
{
    private readonly Dictionary<string, IMessageBrokerPlugin> _loadedPlugins = new();

    public IMessageBrokerPlugin GetPlugin(string name, string brokerUrl, string topic, string consumerGroup)
    {
        if (!_loadedPlugins.TryGetValue(name, out var plugin))
            throw new KeyNotFoundException($"Plugin '{name}' not found");
        plugin.Configure(brokerUrl, topic, consumerGroup);
        return plugin;
    } 
    
    public async Task LoadPluginsAsync()
    {
        logger.LogInfo($"Searching for plugins in: {pluginPath}");
        var pluginFiles = Directory.GetFiles(pluginPath, "*.dll");
        logger.LogInfo($"Found DLL files: {string.Join(", ", pluginFiles)}");
    
        foreach (var file in pluginFiles)
        {
            try
            {
                logger.LogInfo($"Loading assembly from: {file}");
                var assembly = Assembly.LoadFrom(file);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<MessageBrokerPluginAttribute>() != null
                                && typeof(IMessageBrokerPlugin).IsAssignableFrom(t));

                logger.LogInfo($"Found plugin types: {string.Join(", ", pluginTypes.Select(t => t.Name))}");

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = (IMessageBrokerPlugin)Activator.CreateInstance(pluginType)!;
                    _loadedPlugins[plugin.Name] = plugin;
                    logger.LogInfo($"Successfully loaded plugin: {plugin.Name} v{plugin.Version}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to load plugin from {file}: {ex.Message}", ex);
            }
        }
    }

    public async Task ExecutePluginsAsync(IMessage message)
    {
        foreach (var plugin in _loadedPlugins.Values)
        {
            await plugin.ExecuteAsync(message);
        }
    }

    public void ShutdownPlugins()
    {
        foreach (var plugin in _loadedPlugins.Values)
        {
            plugin.Shutdown();
        }
        _loadedPlugins.Clear();
    }
}

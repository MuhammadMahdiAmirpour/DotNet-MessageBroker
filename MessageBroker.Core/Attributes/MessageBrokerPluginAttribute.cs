namespace MessageBroker.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class MessageBrokerPluginAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    
    public MessageBrokerPluginAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

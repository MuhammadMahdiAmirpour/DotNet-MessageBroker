namespace MessageBroker.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class EndpointAttribute : Attribute
{
    public string Path { get; }
    public EndpointAttribute(string path) => Path = path;
}
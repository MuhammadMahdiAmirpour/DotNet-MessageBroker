namespace MessageBroker.API
{
    public class AppSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:5000";
        public string LogsDirectory { get; set; } = "logs";
        public string MessageStoragePath { get; set; } = "messageStorage";
    }
}
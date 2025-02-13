using MessageBroker.API.Services;
using MessageBroker.Core.Interfaces;
using MessageBroker.Logging;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add HTTP logging
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure logging
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "messagebroker.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? string.Empty);
var logger = new MyCustomLogger(logPath, MyCustomLogLevel.Info);
builder.Services.AddSingleton<IMyCustomLogger>(logger);

builder.Services.AddSingleton<IMessageBrokerServer, MessageBrokerServer>();

// Configure URL
builder.WebHost.UseUrls("http://localhost:5000");

var app = builder.Build();

// Initialize MessageBrokerServer
var server = app.Services.GetRequiredService<IMessageBrokerServer>();
await server.StartAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpLogging();
app.UseCors();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

try
{
    var startTime = DateTime.UtcNow;
    var currentUser = Environment.UserName;

    logger.LogInfo($"Starting message broker at {startTime:yyyy-MM-dd HH:mm:ss}");
    logger.LogInfo($"User: {currentUser}");
    logger.LogInfo("Server URL: http://localhost:5000");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical($"Message broker terminated unexpectedly: {ex.Message}");
    throw;
}
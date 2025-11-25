using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

// Set self log for Serilog debugging
Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    var redactionFields = context.Configuration.GetSection("LoggerConfiguration:FieldsToRedact").Get<string[]>();

    // Add enricher to redact specified fields in JSON logs
    loggerConfiguration.Enrich.With(new JsonRedactionEnricher(redactionFields));

    loggerConfiguration.WriteTo.Console();
});

var app = builder.Build();

// Test logging with sensitive data in JSON
var sensitiveData = new JsonExample
{
    Id = Guid.NewGuid(),
    CreditCard = "4111-1111-1111-1111",
    Nested = new JsonNestedExample
    {
        SecretValue = "This is a secret"
    }
};
var stringifiedJson = System.Text.Json.JsonSerializer.Serialize(sensitiveData);

var logger = app.Services.GetRequiredService<ILogger<JsonExample>>();
logger.LogWarning("Logging sensitive data: {SensitiveData}", stringifiedJson);

var doubleStringifiedJson = System.Text.Json.JsonSerializer.Serialize(stringifiedJson);
logger.LogWarning("Logging double stringified sensitive data: {SensitiveData}", doubleStringifiedJson);

var list = JsonSerializer.Serialize(new List<JsonExample>() { sensitiveData });
logger.LogWarning("Logging array stringified sensitive data: {SensitiveData}", list);

var doubleSerializedList = System.Text.Json.JsonSerializer.Serialize(list);
logger.LogWarning("Logging array double stringified sensitive data: {SensitiveData}", doubleSerializedList);

using var stream = new MemoryStream(Encoding.UTF8.GetBytes(stringifiedJson));
using var reader = new StreamReader(stream);
var streamText = reader.ReadToEnd();
logger.LogWarning("Logging sensitive data from stream: {SensitiveData}", streamText);

var doubleStream = new MemoryStream(Encoding.UTF8.GetBytes(doubleStringifiedJson));
using var doubleReader = new StreamReader(doubleStream);
var streamDoubleText = doubleReader.ReadToEnd();
logger.LogWarning("Logging double serialized sensitive data from stream: {SensitiveData}", streamDoubleText);

app.Run();
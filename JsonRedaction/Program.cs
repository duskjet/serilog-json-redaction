using Serilog;
using System.Diagnostics;
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

app.Run();
using Serilog;
using System.Diagnostics;

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

app.Run();
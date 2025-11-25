using Serilog.Events;
using Serilog.Parsing;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JsonRedaction.Tests
{
    public class JsonRedactionEnricherTests
    {
        [Fact]
        public void Enrich_ShouldRedactSensitiveFieldsInLogEvent()
        {
            // Arrange
            var sensitiveData = new JsonExample
            {
                Id = Guid.NewGuid(),
                CreditCard = "4111-1111-1111-1111",
                Nested = new JsonNestedExample
                {
                    SecretValue = "This is a secret"
                }
            };

            var jsonData = JsonSerializer.Serialize(sensitiveData);
            var logEvent = BuildLogEvent(jsonData);

            var propertyFactory = new TestLogEventPropertyFactory();
            var fieldsToRedact = new[] { "CreditCard", "nested.SecretValue" };
            var enricher = new JsonRedactionEnricher(fieldsToRedact);

            // Act
            enricher.Enrich(logEvent, propertyFactory);

            // Assert
            Assert.True(logEvent.Properties.TryGetValue("Request", out var redactedValue));
            Assert.IsType<ScalarValue>(redactedValue);
            var redactedJson = ((ScalarValue)redactedValue).Value as string;

            Assert.NotNull(redactedJson);
            Assert.DoesNotContain("4111-1111-1111-1111", redactedJson);
            Assert.DoesNotContain("This is a secret", redactedJson);
            Assert.Contains("\"CreditCard\":\"*******************\"", redactedJson);
            Assert.Contains("\"SecretValue\":\"****************\"", redactedJson);
        }

        [Theory]
        [InlineData("John")]
        [InlineData("A")]
        [InlineData("")]
        public void Redact_String_ThroughEnricher(string original)
        {
            // Arrange
            var json = JsonSerializer.Serialize(new { name = original });
            var logEvent = BuildLogEvent(json);
            var enricher = new JsonRedactionEnricher(["name"]);

            // Act
            enricher.Enrich(logEvent, new TestLogEventPropertyFactory());
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            // Assert
            string expected = new string('*', original.Length);
            Assert.Contains($"\"name\":\"{expected}\"", redacted);
        }

        [Theory]
        [InlineData(123)]
        [InlineData(98765)]
        [InlineData(3.14)]
        [InlineData(100.0)]
        [InlineData(0)]
        public void Redact_Number_ThroughEnricher(double number)
        {
            // Arrange
            var json = JsonSerializer.Serialize(new { value = number });
            var logEvent = BuildLogEvent(json);
            var enricher = new JsonRedactionEnricher(["value"]);

            // Act
            enricher.Enrich(logEvent, new TestLogEventPropertyFactory());
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            // Assert
            string expected = new string('*', number.ToString().Length);
            Assert.Contains($"\"value\":\"{expected}\"", redacted);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Redact_Boolean_ThroughEnricher(bool flag)
        {
            // Arrange
            var json = JsonSerializer.Serialize(new { flag });
            var logEvent = BuildLogEvent(json);
            var enricher = new JsonRedactionEnricher(["flag"]);

            // Act
            enricher.Enrich(logEvent, new TestLogEventPropertyFactory());
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            // Assert
            Assert.Contains($"\"flag\":\"****\"", redacted);
        }

        [Fact]
        public void Redact_Null_ThroughEnricher()
        {
            // Arrange
            var json = "{\"item\": null}";
            var logEvent = BuildLogEvent(json);
            var enricher = new JsonRedactionEnricher(["item"]);

            // Act
            enricher.Enrich(logEvent, new TestLogEventPropertyFactory());
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            // Assert
            Assert.Contains($"\"item\":\"****\"", redacted);
        }

        [Fact]
        public void Enrich_ShouldRedactSingleTopLevelField()
        {
            // Arrange
            var data = new JsonExample
            {
                Id = Guid.NewGuid(),
                CreditCard = "4111-1111-1111-1111",
                Nested = new JsonNestedExample { SecretValue = "Secret" }
            };

            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "CreditCard" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.DoesNotContain("4111-1111-1111-1111", redacted);
            Assert.Contains("\"CreditCard\":\"*******************\"", redacted);
        }

        [Fact]
        public void Enrich_ShouldRedactNestedField()
        {
            // Arrange
            var data = new JsonExample
            {
                Id = Guid.NewGuid(),
                CreditCard = "4111-1111-1111-1111",
                Nested = new JsonNestedExample { SecretValue = "TopSecret" }
            };

            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "nested.SecretValue" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.DoesNotContain("TopSecret", redacted);
            Assert.Contains("\"SecretValue\":\"*********", redacted);
        }

        [Fact]
        public void Enrich_ShouldRedactMultipleFields()
        {
            // Arrange
            var data = new JsonExample
            {
                Id = Guid.NewGuid(),
                CreditCard = "4111-1111-1111-1111",
                Nested = new JsonNestedExample { SecretValue = "TopSecret" }
            };

            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "CreditCard", "nested.SecretValue" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            Assert.DoesNotContain("4111-1111-1111-1111", redacted);
            Assert.DoesNotContain("TopSecret", redacted);
            Assert.Contains("\"CreditCard\":\"*******************\"", redacted);
            Assert.Contains("\"SecretValue\":\"*********", redacted);
        }

        [Fact]
        public void Enrich_ShouldNotFailForMissingField()
        {
            // Arrange
            var data = new JsonExample { Id = Guid.NewGuid(), CreditCard = "4111-1111-1111-1111", Nested = null };
            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "nested.SecretValue", "NonExistentField" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.NotNull(redacted);
            Assert.Contains("4111-1111-1111-1111", redacted); // unchanged top-level
        }

        [Fact]
        public void Enrich_ShouldHandleNullFieldValue()
        {
            // Arrange
            var data = new JsonExample { Id = Guid.NewGuid(), CreditCard = null, Nested = new JsonNestedExample { SecretValue = null } };
            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "CreditCard", "nested.SecretValue" });
            var factory = new TestLogEventPropertyFactory();
            
            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.NotNull(redacted);
            Assert.Contains("\"CreditCard\":\"****\"", redacted);
            Assert.Contains("\"SecretValue\":\"****\"", redacted);
        }

        [Fact]
        public void Enrich_ShouldRedactFieldsInArray()
        {
            // Arrange
            var data = new JsonArrayExample
            {
                Items = new List<JsonNestedExample>
                {
                    new JsonNestedExample { SecretValue = "Secret1" },
                    new JsonNestedExample { SecretValue = "Secret2" }
                }
            };

            var json = JsonSerializer.Serialize(data);
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "items.SecretValue" });
            var factory = new TestLogEventPropertyFactory();
            
            // Act
            enricher.Enrich(logEvent, factory);

            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;

            // Assert
            Assert.DoesNotContain("Secret1", redacted);
            Assert.DoesNotContain("Secret2", redacted);
            Assert.Contains("\"SecretValue\":\"*******\"", redacted);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("{}")]
        public void Enrich_ShouldHandleEmptyJson(string json)
        {
            // Arrange
            LogEvent logEvent = BuildLogEvent(json);
            var enricher = new JsonRedactionEnricher(new[] { "Anything" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            enricher.Enrich(logEvent, factory);

            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.Equal(json, redacted);
        }

        [Theory]
        [InlineData("Not a JSON string")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{ invalid json }")]
        public void Enrich_ShouldNotThrowForInvalidJson(string json)
        {
            // Arrange
            var logEvent = BuildLogEvent(json);

            var enricher = new JsonRedactionEnricher(new[] { "CreditCard" });
            var factory = new TestLogEventPropertyFactory();

            // Act
            var ex = Record.Exception(() => enricher.Enrich(logEvent, factory));
            Assert.Null(ex);

            // Assert
            var value = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.Equal(json, value);
        }

        [Fact]
        public void Enrich_ShouldRedactStringifiedJson()
        {
            // Arrange
            var original = new { CreditCard = "4111-1111-1111-1111" };
            var json = JsonSerializer.Serialize(original);
            var stringifiedJson = JsonSerializer.Serialize(json); // double serialization
            var logEvent = BuildLogEvent(stringifiedJson);
            var enricher = new JsonRedactionEnricher(new[] { "CreditCard" });
            var factory = new TestLogEventPropertyFactory();
            // Act
            enricher.Enrich(logEvent, factory);
            // Assert
            var redacted = ((ScalarValue)logEvent.Properties["Request"]).Value as string;
            Assert.DoesNotContain("4111-1111-1111-1111", redacted);
            Assert.Contains("\"CreditCard\":\"*******************\"", redacted);
        }

        private static LogEvent BuildLogEvent(string json)
        {
            return new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Information,
                null,
                new MessageTemplate(json, Enumerable.Empty<MessageTemplateToken>()),
                new List<LogEventProperty> { new LogEventProperty("Request", new ScalarValue(json)) }
            );
        }
    }
}

public class JsonArrayExample
{
    public List<JsonNestedExample> Items { get; set; }
}

public class JsonExample
{
    public Guid Id { get; set; }
    public string CreditCard { get; set; }
    public JsonNestedExample Nested { get; set; }
}

public class JsonNestedExample
{
    public string SecretValue { get; set; }
}
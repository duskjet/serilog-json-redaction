using Microsoft.AspNetCore.JsonPatch;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Nodes;

public class JsonRedactionEnricher : ILogEventEnricher
{
    private readonly string[] _fieldsToRedact;

    public JsonRedactionEnricher(IEnumerable<string> fieldsToRedact)
    {
        _fieldsToRedact = fieldsToRedact.ToArray();
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties)
        {
            if (property.Value is ScalarValue scalarValue &&
                scalarValue.Value is string message &&
                LooksLikeJson(message, out bool serializedTwice))
            {
                JsonNode? json;
                try
                {
                    if (serializedTwice)
                    {
                        message = JsonSerializer.Deserialize<string>(message)!;
                    }
                    json = JsonNode.Parse(message);

                    if (json is null)
                    {
                        return;
                    }
                }
                catch (JsonException)
                {
                    // Ignore parsing errors and continue
                    return;
                }

                foreach (var field in _fieldsToRedact)
                {
                    RedactPath(json, field);
                }

                message = json.ToJsonString();
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(property.Key, message));
            }
        }
    }

    private bool LooksLikeJson(string input, out bool serializedTwice)
    {
        serializedTwice = false;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        ReadOnlySpan<char> span = input.AsSpan().Trim();
        ReadOnlySpan<char> doubleSerializePattern = "\\u0022".AsSpan();
        ReadOnlySpan<char> doubleSerializePatternUnsafe = "\\\"".AsSpan();

        if (span.IndexOf(doubleSerializePattern) >= 0)
        {
            serializedTwice = true;
            return true;
        }
        else if (span.IndexOf(doubleSerializePatternUnsafe) >= 0)
        {
            serializedTwice = true;
            return true;
        }

        return (span.Length > 1) &&
               ((span[0] == '{' && span[^1] == '}') ||
                (span[0] == '[' && span[^1] == ']'));
    }

    public static bool RedactPath(JsonNode? root, string path)
    {
        if (root is null)
        {
            return false;
        }

        string[] parts = path.Split('.');

        return RedactRecursive(root, parts, 0);
    }

    private static bool RedactRecursive(JsonNode? node, string[] parts, int index)
    {
        if (node is null)
        {
            return false;
        }

        // Done? nothing to do
        if (index >= parts.Length)
        {
            return false;
        }

        string part = parts[index];
        bool modified = false;

        //
        // If this node is ARRAY → recursively apply same path part to each element
        //
        if (node is JsonArray arr)
        {
            foreach (var element in arr)
            {
                modified |= RedactRecursive(element, parts, index);
            }

            return modified;
        }

        //
        // Must be an object now
        //
        if (node is not JsonObject obj)
        {
            return false;
        }

        // Case-insensitive lookup
        var match = obj.FirstOrDefault(p => string.Equals(p.Key, part, StringComparison.OrdinalIgnoreCase));

        if (match.Key == null)
        {
            return false;
        }

        JsonNode? valueNode = match.Value;

        //
        // If this is the last segment → perform redaction
        //
        if (index == parts.Length - 1)
        {
            if (valueNode is JsonValue value)
            {
                switch (value.GetValueKind())
                {
                    case JsonValueKind.String:
                        if (value.TryGetValue<string>(out string strVal))
                        {
                            obj[match.Key] = new string('*', strVal.Length);
                            return true;
                        }
                        return false;

                    case JsonValueKind.Number:
                        if (value.TryGetValue<double>(out double number))
                        {
                            obj[match.Key] = new string('*', number.ToString().Length);
                            return true;
                        }
                        return false;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        obj[match.Key] = "****";
                        return true;
                }
            }

            if (valueNode is null)
            {
                obj[match.Key] = "****";
                return true;
            }

            return false;
        }

        //
        // Otherwise continue recursion:
        // If field is object → normal dive
        // If field is array → apply next path part to every element
        //
        if (valueNode is JsonObject childObj)
        {
            modified |= RedactRecursive(childObj, parts, index + 1);
        }
        else if (valueNode is JsonArray childArr)
        {
            foreach (var element in childArr)
            {
                modified |= RedactRecursive(element, parts, index + 1);
            }
        }

        return modified;
    }

    //public static void RedactPath(JsonNode? root, string path)
    //{
    //    if (root is not JsonObject obj)
    //    {
    //        return;
    //    }

    //    string[] parts = path.Split('.');

    //    for (int i = 0; i < parts.Length; i++)
    //    {
    //        string part = parts[i];

    //        // Case-insensitive lookup
    //        var match = obj.FirstOrDefault(p =>
    //            string.Equals(p.Key, part, StringComparison.OrdinalIgnoreCase));

    //        if (match.Key == null)
    //        {
    //            return;
    //        }

    //        if (i == parts.Length - 1)
    //        {
    //            if (obj[match.Key] is JsonValue value)
    //            {
    //                switch (value.GetValueKind())
    //                {
    //                    case JsonValueKind.String:
    //                        if (value.TryGetValue<string>(out string text))
    //                        {
    //                            obj[match.Key] = new string('*', text.Length);
    //                        }
    //                        return;
    //                    case JsonValueKind.Number:
    //                        if (value.TryGetValue<double>(out double number))
    //                        {
    //                            obj[match.Key] = new string('*', number.ToString().Length);
    //                        }
    //                        return;
    //                    case JsonValueKind.True:
    //                    case JsonValueKind.False:
    //                    case JsonValueKind.Null:
    //                        obj[match.Key] = "****";
    //                        return;
    //                }
    //            }
    //        }

    //        if (match.Value is JsonObject next)
    //        {
    //            obj = next;
    //        }
    //        else
    //        {
    //            return;
    //        }
    //    }
    //}
}
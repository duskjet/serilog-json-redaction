using Serilog.Core;
using Serilog.Events;

public sealed class TestLogEventPropertyFactory : ILogEventPropertyFactory
{
    public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
    {
        return new LogEventProperty(name, ConvertToLogEventPropertyValue(value));
    }

    private LogEventPropertyValue ConvertToLogEventPropertyValue(object? value)
    {
        if (value == null)
            return new ScalarValue(null);

        return value switch
        {
            LogEventPropertyValue pv => pv,

            // Simple scalar types
            string s => new ScalarValue(s),
            int i => new ScalarValue(i),
            long l => new ScalarValue(l),
            double d => new ScalarValue(d),
            float f => new ScalarValue(f),
            bool b => new ScalarValue(b),
            decimal m => new ScalarValue(m),

            // Arrays → SequenceValue
            IEnumerable<object?> enumerable =>
                new SequenceValue(enumerable.Select(ConvertToLogEventPropertyValue).ToList()),

            // Fallback → ToString() scalar (good for tests)
            _ => new ScalarValue(value.ToString())
        };
    }
}

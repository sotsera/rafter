using System.Globalization;

namespace Sotsera.Rafter.Internal;

internal static class EnvironmentInputs
{
    public static void Resolve(OptionSnapshot[] options, Dictionary<OptionDeclaration, object?> values,
        Dictionary<OptionDeclaration, InputSource> sources, Func<string, string?> readEnvironment,
        List<string> diagnostics)
    {
        // Capture each selected variable once, even when several options map the same name.
        var environment = new Dictionary<string, string?>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var option in options)
        {
            var source = sources[option.Declaration];
            if (source.Kind == InputSourceKind.CommandLine)
            {
                continue;
            }
            if (option.EnvironmentVariable is { } name)
            {
                if (!environment.TryGetValue(name, out var text))
                {
                    text = readEnvironment(name);
                    environment.Add(name, text);
                }
                if (text is not null)
                {
                    sources[option.Declaration] = new(InputSourceKind.Environment, name, text.Length == 0);
                    if (TryConvert(text, option.ValueType, out var value))
                    {
                        values[option.Declaration] = value;
                    }
                    else
                    {
                        var expected = option.ValueType == typeof(int) ? "an integer" : "true or false";
                        var reason = text.Length == 0 ? "is empty" : "has an invalid value";
                        diagnostics.Add($"{option.Spellings[0]}: environment variable {name} {reason}; expected {expected}.");
                    }
                    continue;
                }
            }
            if (option.Required)
            {
                diagnostics.Add($"{option.Spellings[0]} is required.");
            }
        }
    }

    private static bool TryConvert(string text, Type type, out object? value)
    {
        value = null;
        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        var trimmed = text.Trim();
        if (type == typeof(int) && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var integer))
        {
            value = integer;
            return true;
        }
        if (type == typeof(bool) && (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)))
        {
            value = trimmed.Equals("true", StringComparison.OrdinalIgnoreCase);
            return true;
        }
        return false;
    }
}

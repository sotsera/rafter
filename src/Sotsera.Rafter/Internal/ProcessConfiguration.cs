namespace Sotsera.Rafter.Internal;

internal static class ProcessConfiguration
{
    public static void CheckText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
        {
            throw new ArgumentException("The value must contain non-whitespace text and must not contain NUL.", name);
        }
    }

    public static void CheckEnvironmentName(string name)
    {
        CheckText(name, nameof(name));
        if (name.Contains('='))
        {
            throw new ArgumentException("Environment names must not contain '='.", nameof(name));
        }
    }

    public static void CheckEnvironment(string name, string value)
    {
        CheckEnvironmentName(name);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        {
            throw new ArgumentException("Environment values must not contain NUL.", nameof(value));
        }
    }

    public static string[] CopyArguments(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var copy = arguments.ToArray();
        foreach (var argument in copy)
        {
            if (argument is null || argument.Contains('\0'))
            {
                throw new ArgumentException("Argument tokens must not be null or contain NUL.", nameof(arguments));
            }
        }
        return copy;
    }

    public static void AddEnvironment(Dictionary<string, string?> changes, string name, string? value)
    {
        if (!changes.TryAdd(name, value))
        {
            throw new InvalidOperationException($"Environment variable '{name}' was already configured in this layer.");
        }
    }
}

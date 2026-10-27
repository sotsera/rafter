namespace Sotsera.Rafter.Internal;

internal sealed record ExecutableDefaults(string? Directory, TimeSpan? Timeout,
    IReadOnlyDictionary<string, string?> Environment)
{
    public static ExecutableDefaults Empty { get; } = new(null, null,
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
}

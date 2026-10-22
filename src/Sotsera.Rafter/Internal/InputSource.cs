namespace Sotsera.Rafter.Internal;

internal enum InputSourceKind
{
    NotSupplied,
    CommandLine,
    Environment,
    Default
}

internal sealed record InputSource(InputSourceKind Kind, string? Variable = null, bool Empty = false)
{
    public string Description => Kind switch
    {
        InputSourceKind.CommandLine => "command line",
        InputSourceKind.Environment => "environment " + Variable + (Empty ? " (empty)" : ""),
        InputSourceKind.Default => "default",
        _ => "not supplied"
    };
}

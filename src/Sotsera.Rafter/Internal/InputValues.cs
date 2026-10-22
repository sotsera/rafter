namespace Sotsera.Rafter.Internal;

internal sealed class InputValues(IReadOnlyDictionary<OptionDeclaration, object?> values,
    IReadOnlyDictionary<OptionDeclaration, InputSource>? sources = null)
{
    private readonly IReadOnlyDictionary<OptionDeclaration, object?> _values = values;
    private readonly IReadOnlyDictionary<OptionDeclaration, InputSource>? _sources = sources;

    public InputSource Source(OptionDeclaration declaration)
        => _sources is not null && _sources.TryGetValue(declaration, out var source)
            ? source : new(InputSourceKind.NotSupplied);

    public string DiagnosticName(OptionSnapshot option)
    {
        var source = Source(option.Declaration);
        return option.Spellings[0] + (source.Kind == InputSourceKind.Environment
            ? $" (environment {source.Variable})" : "");
    }

    public T GetRequired<T>(ValueOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        var value = Read(option.Declaration)
            ?? throw new InvalidOperationException("This invocation has no value for the required/defaulted handle.");
        return (T)value;
    }

    public string? GetString(Option<string> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return (string?)Read(option.Declaration);
    }

    public T? GetOptional<T>(Option<T> option) where T : struct
    {
        ArgumentNullException.ThrowIfNull(option);
        var value = Read(option.Declaration);
        return value is null ? null : (T)value;
    }

    public object? Read(OptionDeclaration declaration)
    {
        if (!_values.TryGetValue(declaration, out var value))
        {
            throw new InvalidOperationException("The option does not belong to this invocation's declaration snapshot.");
        }
        return value;
    }
}

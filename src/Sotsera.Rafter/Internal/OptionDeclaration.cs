namespace Sotsera.Rafter.Internal;

internal sealed class OptionDeclaration(object gate, string name, string description, Type valueType)
{
    private readonly object _gate = gate;
    private readonly string _name = name;
    private readonly string _description = description;
    private readonly Type _valueType = valueType;
    private readonly List<string> _aliases = [];
    private readonly List<ValueRule> _rules = [];
    private int _requiredCalls;
    private int _defaultCalls;
    private object? _defaultValue;
    private int _environmentCalls;
    private string? _environmentVariable;
    private bool _nullAliases;

    public void AddAliases(string[]? aliases)
    {
        lock (_gate)
        {
            if (aliases is null)
            {
                _nullAliases = true;
            }
            else
            {
                _aliases.AddRange(aliases);
            }
        }
    }

    public void AddRule<T>(string description, Func<T, bool>? predicate)
    {
        lock (_gate)
        {
            _rules.Add(new(description, predicate is null ? null : value => predicate((T)value)));
        }
    }

    public void Require()
    {
        lock (_gate)
        {
            _requiredCalls++;
        }
    }

    public void SetDefault(object? value)
    {
        lock (_gate)
        {
            _defaultCalls++;
            _defaultValue = value;
        }
    }

    public void SetEnvironment(string? name)
    {
        lock (_gate)
        {
            _environmentCalls++;
            _environmentVariable = name;
        }
    }

    public OptionSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new(this, _name, _description, _valueType, [.. _aliases], [.. _rules],
                _requiredCalls, _defaultCalls, _defaultValue, _nullAliases, _environmentCalls, _environmentVariable);
        }
    }
}

internal sealed record ValueRule(string Description, Func<object, bool>? Predicate);
internal sealed record CombinedRule(string Description, Func<InputValidationContext, bool>? Predicate);

internal sealed record OptionSnapshot(
    OptionDeclaration Declaration,
    string Name,
    string Description,
    Type ValueType,
    string[] Aliases,
    ValueRule[] Rules,
    int RequiredCalls,
    int DefaultCalls,
    object? DefaultValue,
    bool NullAliases,
    int EnvironmentCalls,
    string? EnvironmentVariable)
{
    public string[] Spellings { get; private set; } = [];
    public EnumChoices? EnumChoices { get; private set; }
    public bool Required => RequiredCalls > 0;
    public bool HasDefault => DefaultCalls > 0;

    public void ValidateDeclaration(int index, List<string> diagnostics, HashSet<string> ownedSpellings)
    {
        var location = $"Option declaration {index + 1}";
        var spellings = new List<string>();
        foreach (var rawName in new[] { Name }.Concat(Aliases))
        {
            var spelling = Normalize(rawName);
            if (spelling is null)
            {
                diagnostics.Add($"{location}: Names and aliases must use lowercase letters/digits and single hyphens.");
            }
            else if (spelling.TrimStart('-') is "help" or "quiet" or "verbose" or "plain")
            {
                diagnostics.Add($"{location}: {spelling} is reserved for Rafter.");
            }
            else if (!spellings.Contains(spelling, StringComparer.Ordinal))
            {
                spellings.Add(spelling);
            }
        }
        Spellings = [.. spellings];
        foreach (var spelling in spellings)
        {
            if (!ownedSpellings.Add(spelling))
            {
                diagnostics.Add($"{location}: {spelling} belongs to more than one option.");
            }
        }
        if (string.IsNullOrWhiteSpace(Description))
        {
            diagnostics.Add($"{location}: Description must contain non-whitespace text.");
        }
        if (ValueType.IsEnum)
        {
            EnumChoices = new(ValueType);
            EnumChoices.Validate(location, HasDefault, DefaultValue, diagnostics);
        }
        else if (ValueType != typeof(string) && ValueType != typeof(int) && ValueType != typeof(bool)
            && ValueType != typeof(TimeSpan))
        {
            diagnostics.Add($"{location}: Supported supplied types are string, int, bool, named enums, and TimeSpan, "
                + "without nullable value types.");
        }
        if (NullAliases)
        {
            diagnostics.Add($"{location}: Alias arrays must not be null.");
        }
        if (RequiredCalls + DefaultCalls > 1)
        {
            diagnostics.Add($"{location}: Required or Default may be configured only once.");
        }
        if (HasDefault && DefaultValue is null)
        {
            diagnostics.Add($"{location}: Default must not be null.");
        }
        if (EnvironmentCalls > 1)
        {
            diagnostics.Add($"{location}: FromEnvironment may be configured only once.");
        }
        if (EnvironmentCalls > 0 && (string.IsNullOrWhiteSpace(EnvironmentVariable)
            || char.IsWhiteSpace(EnvironmentVariable[0]) || char.IsWhiteSpace(EnvironmentVariable[^1])
            || EnvironmentVariable.Any(character => character == '=' || char.IsControl(character))))
        {
            diagnostics.Add($"{location}: Environment names must be nonblank, without surrounding whitespace, '=' or control characters.");
        }
        foreach (var rule in Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Description) || rule.Predicate is null)
            {
                diagnostics.Add($"{location}: Rules require a nonblank description and a non-null predicate.");
            }
        }
    }

    private static string? Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        var prefixLength = name.TakeWhile(character => character == '-').Count();
        var bare = name[prefixLength..];
        if (prefixLength > 2 || !DeclarationName.IsValid(bare))
        {
            return null;
        }
        return prefixLength > 0 ? name : (bare.Length == 1 ? "-" : "--") + bare;
    }
}

using System.Reflection;

namespace Sotsera.Rafter.Internal;

internal sealed class EnumChoices
{
    private readonly Type _type;
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<object> _distinctValues = [];

    public string[] Names { get; }

    public EnumChoices(Type type)
    {
        _type = type;
        // On .NET 10 GetFields follows metadata order, preserving C# declaration order rather than numeric order.
        var members = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Names = members.Select(member => member.Name).ToArray();
        foreach (var member in members)
        {
            var value = member.GetValue(null)!;
            _values.TryAdd(member.Name, value);
            _distinctValues.Add(value);
        }
    }

    public void Validate(string location, bool hasDefault, object? defaultValue, List<string> diagnostics)
    {
        if (_type.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            diagnostics.Add($"{location}: Flags enums are not supported.");
        }
        if (Names.Length == 0)
        {
            diagnostics.Add($"{location}: Enums must declare at least one member name.");
        }
        if (_values.Count != Names.Length)
        {
            diagnostics.Add($"{location}: Enum member names must be unique ignoring case.");
        }
        if (_distinctValues.Count != Names.Length)
        {
            diagnostics.Add($"{location}: Enum members must have distinct numeric values.");
        }
        if (hasDefault && defaultValue is not null && !_distinctValues.Contains(defaultValue))
        {
            diagnostics.Add($"{location}: Enum defaults must identify a declared member.");
        }
    }

    public bool TryConvert(string text, out object? value) => _values.TryGetValue(text.Trim(), out value);
}

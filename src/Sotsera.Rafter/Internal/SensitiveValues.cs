using System.Text;

namespace Sotsera.Rafter.Internal;

// Populated during input preparation only, before user callbacks; execution reads an immutable array.
// Owned by one invocation, including its cleanup and fallback reporting.
internal sealed class SensitiveValues
{
    public const string Marker = "[REDACTED]";
    private string[] _patterns = [];

    public void Add(string? value)
    {
        if (!string.IsNullOrEmpty(value) && !_patterns.Contains(value, StringComparer.Ordinal))
        {
            _patterns = [.. _patterns.Append(value).OrderByDescending(pattern => pattern.Length)];
        }
    }

    public IEnumerable<(int Start, int Length)> Matches(string text)
    {
        var offset = 0;
        while (offset < text.Length)
        {
            var start = -1;
            var length = 0;
            foreach (var pattern in _patterns)
            {
                var candidate = text.IndexOf(pattern, offset, StringComparison.Ordinal);
                if (candidate >= 0 && (start < 0 || candidate < start))
                {
                    start = candidate;
                    length = pattern.Length;
                }
            }
            if (start < 0)
            {
                yield break;
            }
            yield return (start, length);
            offset = start + length;
        }
    }

    public string Redact(string text)
    {
        StringBuilder? result = null;
        var offset = 0;
        foreach (var (start, length) in Matches(text))
        {
            result ??= new StringBuilder();
            result.Append(text, offset, start - offset);
            result.Append(Marker);
            offset = start + length;
        }
        if (result is null)
        {
            return text;
        }
        result.Append(text, offset, text.Length - offset);
        return result.ToString();
    }
}

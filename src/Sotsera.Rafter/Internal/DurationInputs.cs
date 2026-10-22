using System.Globalization;
using System.Numerics;
using System.Text;

namespace Sotsera.Rafter.Internal;

internal static class DurationInputs
{
    private static readonly (string Symbol, long Ticks)[] _units =
    [
        ("h", TimeSpan.TicksPerHour),
        ("m", TimeSpan.TicksPerMinute),
        ("s", TimeSpan.TicksPerSecond),
        ("ms", TimeSpan.TicksPerMillisecond)
    ];

    public static bool TryParse(string text, out TimeSpan value)
    {
        value = default;
        var input = text.AsSpan().Trim();
        var negative = false;
        if (!input.IsEmpty && input[0] is '+' or '-')
        {
            negative = input[0] == '-';
            input = input[1..];
        }
        if (input.IsEmpty)
        {
            return false;
        }

        var position = 0;
        var previousUnit = -1;
        var totalTicks = BigInteger.Zero;
        while (position < input.Length)
        {
            if (!ReadNumber(input, ref position, out var numerator, out var denominator, out var fractional))
            {
                return false;
            }
            var unit = ReadUnit(input, ref position);
            if (unit < 0 || unit <= previousUnit || (fractional && position != input.Length))
            {
                return false;
            }
            previousUnit = unit;
            // Integer arithmetic preserves every input digit: no floating-point rounding or decimal truncation.
            var ticks = BigInteger.DivRem(numerator * _units[unit].Ticks, denominator, out var remainder);
            if (!remainder.IsZero)
            {
                return false;
            }
            totalTicks += ticks;
        }
        if (negative)
        {
            totalTicks = -totalTicks;
        }
        if (totalTicks < long.MinValue || totalTicks > long.MaxValue)
        {
            return false;
        }
        value = TimeSpan.FromTicks((long)totalTicks);
        return true;
    }

    public static string Format(TimeSpan value)
    {
        if (value == TimeSpan.Zero)
        {
            return "0s";
        }
        var ticks = value.Ticks;
        // Avoid negating long.MinValue when formatting the full negative TimeSpan boundary.
        var remaining = ticks < 0 ? (ulong)(-(ticks + 1)) + 1 : (ulong)ticks;
        var result = new StringBuilder(ticks < 0 ? "-" : "");
        for (var index = 0; index < _units.Length - 1; index++)
        {
            var unitTicks = (ulong)_units[index].Ticks;
            var component = remaining / unitTicks;
            remaining %= unitTicks;
            if (component > 0)
            {
                result.Append(component.ToString(CultureInfo.InvariantCulture));
                result.Append(_units[index].Symbol);
            }
        }
        if (remaining > 0)
        {
            var milliseconds = (decimal)remaining / TimeSpan.TicksPerMillisecond;
            result.Append(milliseconds.ToString("0.####", CultureInfo.InvariantCulture));
            result.Append("ms");
        }
        return result.ToString();
    }

    private static bool ReadNumber(ReadOnlySpan<char> input, ref int position,
        out BigInteger numerator, out BigInteger denominator, out bool fractional)
    {
        var start = position;
        while (position < input.Length && char.IsAsciiDigit(input[position]))
        {
            position++;
        }
        denominator = BigInteger.One;
        fractional = position < input.Length && input[position] == '.';
        if (!BigInteger.TryParse(input[start..position], NumberStyles.None, CultureInfo.InvariantCulture, out numerator))
        {
            return false;
        }
        if (!fractional)
        {
            return true;
        }
        position++;
        start = position;
        while (position < input.Length && char.IsAsciiDigit(input[position]))
        {
            position++;
        }
        if (start == position)
        {
            return false;
        }
        var end = position;
        while (end > start && input[end - 1] == '0')
        {
            end--;
        }
        if (end > start)
        {
            denominator = BigInteger.Pow(10, end - start);
            if (!BigInteger.TryParse(input[start..end], NumberStyles.None, CultureInfo.InvariantCulture, out var fraction))
            {
                return false;
            }
            numerator = numerator * denominator + fraction;
        }
        return true;
    }

    private static int ReadUnit(ReadOnlySpan<char> input, ref int position)
    {
        // Match ms before m, while retaining descending h/m/s/ms indices for ordering validation.
        for (var index = _units.Length - 1; index >= 0; index--)
        {
            if (input[position..].StartsWith(_units[index].Symbol, StringComparison.OrdinalIgnoreCase))
            {
                position += _units[index].Symbol.Length;
                return index;
            }
        }
        return -1;
    }
}

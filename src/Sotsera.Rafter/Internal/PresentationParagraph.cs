using Spectre.Console;
using Spectre.Console.Rendering;

namespace Sotsera.Rafter.Internal;

// Retain the original text across style boundaries until masking is complete, before Spectre lays it out.
internal sealed class PresentationParagraph(SensitiveValues values, string text = "", Style? style = null) : IRenderable
{
    private readonly List<Part> _parts = [new(text, style)];

    public Overflow Overflow { get; set; } = Overflow.Fold;

    public PresentationParagraph Append(string text, Style? style = null)
    {
        _parts.Add(new(text, style));
        return this;
    }

    public PresentationParagraph AppendMask()
    {
        _parts.Add(new(SensitiveValues.Marker, null, true));
        return this;
    }

    public Measurement Measure(RenderOptions options, int maxWidth) => ((IRenderable)Create()).Measure(options, maxWidth);

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth) =>
        ((IRenderable)Create()).Render(options, maxWidth);

    private Paragraph Create()
    {
        var result = new Paragraph { Overflow = Overflow };
        var groupStart = 0;
        for (var index = 0; index <= _parts.Count; index++)
        {
            if (index == _parts.Count || _parts[index].Masked)
            {
                AppendMasked(result, _parts.GetRange(groupStart, index - groupStart));
                if (index < _parts.Count)
                {
                    result.Append(SensitiveValues.Marker);
                }
                groupStart = index + 1;
            }
        }
        return result;
    }

    private void AppendMasked(Paragraph result, List<Part> parts)
    {
        var original = string.Concat(parts.Select(part => part.Text));
        var offset = 0;
        foreach (var (start, length) in values.Matches(original))
        {
            AppendSlice(result, parts, offset, start - offset);
            result.Append(SensitiveValues.Marker, StyleAt(parts, start));
            offset = start + length;
        }
        AppendSlice(result, parts, offset, original.Length - offset);
    }

    private static Style? StyleAt(List<Part> parts, int index)
    {
        foreach (var part in parts)
        {
            if (index < part.Text.Length)
            {
                return part.Style;
            }
            index -= part.Text.Length;
        }
        return null;
    }

    private static void AppendSlice(Paragraph destination, List<Part> parts, int start, int length)
    {
        foreach (var part in parts)
        {
            if (length == 0)
            {
                return;
            }
            if (start >= part.Text.Length)
            {
                start -= part.Text.Length;
                continue;
            }
            var take = Math.Min(length, part.Text.Length - start);
            destination.Append(part.Text.Substring(start, take), part.Style);
            length -= take;
            start = 0;
        }
    }

    private sealed record Part(string Text, Style? Style, bool Masked = false);
}

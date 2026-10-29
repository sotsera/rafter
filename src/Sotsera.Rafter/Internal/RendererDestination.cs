using Spectre.Console;
using Spectre.Console.Rendering;

namespace Sotsera.Rafter.Internal;

internal sealed record DestinationPolicy(bool Rich, bool NoColor)
{
    public static DestinationPolicy Detect(TextWriter originalWriter, bool redirected, bool plain, bool noColor)
    {
        var capabilities = AnsiCapabilities.Create(originalWriter);
        return new(!redirected && !plain && capabilities.Ansi, noColor);
    }
}

// Shared only by destinations painting the same terminal. All state is accessed under Gate.
internal sealed class DestinationState
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public bool LineStart { get; set; } = true;
}

// One state per physical destination coordinates layout, literal text, prefixes, and final flushing.
// The caller owns the writer and must await its producers before CompleteAsync.
internal sealed class RendererDestination
{
    private readonly TextWriter _writer;
    private readonly DestinationPolicy _policy;
    private readonly IAnsiConsole _console;
    private readonly AnsiWriter _ansi;
    private readonly DestinationState _state;
    private readonly SensitiveValues? _sensitiveValues;
    private bool _completed;
    private Exception? _failure;

    public Exception? Failure => _failure;

    public RendererDestination(TextWriter writer, DestinationPolicy policy, int width = 80,
        Dictionary<string, string>? environment = null, DestinationState? state = null,
        SensitiveValues? sensitiveValues = null)
    {
        _sensitiveValues = sensitiveValues;
        _state = state ?? new DestinationState();
        _writer = writer;
        _policy = policy;
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = policy.Rich ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = policy.NoColor ? ColorSystemSupport.NoColors : ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            EnvironmentVariables = environment
        });
        _console.Profile.Width = width;
        _ansi = new AnsiWriter(writer, new AnsiCapabilities
        {
            Ansi = policy.Rich,
            ColorSystem = policy.NoColor ? ColorSystem.NoColors : ColorSystem.TrueColor
        });
    }

    public void Framework(string text, Style style)
    {
        Layout(new Paragraph(_sensitiveValues?.Redact(text) ?? text, style) { Overflow = Overflow.Fold });
    }

    public void Layout(IRenderable content, int indentation = 0)
    {
        _state.Gate.Wait();
        try
        {
            ThrowIfCompleted();
            if (!_state.LineStart)
            {
                // A framework message starts its own line after either stream's unterminated terminal chunk.
                _writer.Write(Environment.NewLine);
            }
            var layoutLineStart = true;
            var wroteContent = false;
            var width = Math.Max(1, _console.Profile.Width - indentation);
            foreach (var segment in content.Render(RenderOptions.Create(_console), width))
            {
                if (segment.IsLineBreak)
                {
                    _writer.Write(Environment.NewLine);
                    layoutLineStart = true;
                    wroteContent = true;
                    continue;
                }

                if (segment.Text.Length == 0)
                {
                    continue;
                }

                if (layoutLineStart && indentation > 0)
                {
                    _writer.Write(new string(' ', indentation));
                }

                if (_policy.Rich)
                {
                    _ansi.Write(segment.Text, segment.Style);
                }
                else
                {
                    // Rendering segments directly avoids the legacy backend and its Console color side effects.
                    _writer.Write(segment.Text);
                }

                layoutLineStart = false;
                wroteContent = true;
            }

            if (!wroteContent || !layoutLineStart)
            {
                _writer.Write(Environment.NewLine);
            }
            _state.LineStart = true;
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public void Literal(string text, string prefix = "  > ")
    {
        _state.Gate.Wait();
        try
        {
            ThrowIfCompleted();
            if (!_policy.Rich)
            {
                _writer.Write(text);
                if (text.Length > 0)
                {
                    _state.LineStart = text[^1] == '\n';
                }
                return;
            }

            // LF starts a logical line; CR is preserved without adding another prefix or rewriting child content.
            var start = 0;
            while (start < text.Length)
            {
                if (_state.LineStart)
                {
                    _ansi.Write(prefix, new Style(Color.Grey));
                    _state.LineStart = false;
                }

                var newline = text.IndexOf('\n', start);
                var end = newline < 0 ? text.Length : newline + 1;
                _writer.Write(text.AsSpan(start, end - start));
                _state.LineStart = newline >= 0;
                start = end;
            }
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task CompleteAsync()
    {
        await _state.Gate.WaitAsync();
        try
        {
            ThrowIfCompleted();
            _completed = true;
            await _writer.FlushAsync();
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The invocation output has completed.");
        }
    }
}

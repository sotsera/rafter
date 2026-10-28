using System.Globalization;
using Spectre.Console;

namespace Sotsera.Rafter.Internal;

internal sealed class InvocationOutput
{
    private readonly RendererDestination _output;
    private readonly RendererDestination _error;
    private readonly InvocationArguments _arguments;
    private bool _targetStarted;

    public Exception? Failure => _output.Failure ?? _error.Failure;

    public InvocationOutput(InvocationHost host, InvocationArguments arguments)
    {
        _arguments = arguments;
        // Both handles paint the same terminal. Keep each render/chunk together, including its ANSI prefix/reset.
        // Redirected destinations retain independent gates so a blocked pipe cannot hold the other stream's gate.
        var terminalState = !host.OutputRedirected && !host.ErrorRedirected ? new DestinationState() : null;
        _output = new(host.Output,
            DestinationPolicy.Detect(host.Output, host.OutputRedirected, arguments.Plain, host.NoColor), host.Width,
            state: terminalState);
        _error = new(host.Error,
            DestinationPolicy.Detect(host.Error, host.ErrorRedirected, arguments.Plain, host.NoColor), host.Width,
            state: terminalState);
    }

    public void WriteLine(string? text) => _output.Literal((text ?? "") + Environment.NewLine, "  | ");

    public void Help(string description, OptionSnapshot[] options, CombinedRule[] rules, TargetPlan? plan = null,
        CleanupDeclaration? cleanup = null)
    {
        _output.Framework(description, new Style(decoration: Decoration.Bold));
        _output.Framework("", Style.Plain);
        Section("Command options");
        if (options.Any(option => option.EnvironmentVariable is not null))
        {
            _output.Layout(new Paragraph("Source order: command line, environment, default.")
                { Overflow = Overflow.Fold }, 2);
        }
        if (options.Length == 0)
        {
            _output.Framework("  None.", Style.Plain);
        }
        for (var index = 0; index < options.Length; index++)
        {
            if (index > 0 && options[index - 1].Rules.Length > 0)
            {
                _output.Framework("", Style.Plain);
            }
            Input(options[index]);
        }
        _output.Framework("", Style.Plain);
        Section("Execution");
        if (plan is null)
        {
            _output.Framework("  Run the supplied command body.", Style.Plain);
        }
        else
        {
            TargetHelp(plan);
        }
        if (cleanup is { Calls: > 0 })
        {
            _output.Layout(new Paragraph("Command cleanup: " + cleanup.Description) { Overflow = Overflow.Fold }, 2);
        }
        if (rules.Length > 0)
        {
            _output.Framework("  Combined input rules:", Style.Plain);
            foreach (var rule in rules)
            {
                _output.Layout(new Paragraph("- " + rule.Description) { Overflow = Overflow.Fold }, 4);
            }
        }
        _output.Framework("", Style.Plain);
        Section("Rafter options");
        Control("--help", plan is null ? "Show help without executing the command body."
            : "Show help without executing targets.");
        Control("--quiet", "Hide routine lifecycle output; keep results and failures.");
        Control("--verbose", "Show additional execution context.");
        Control("--plain", "Disable styling and presentation prefixes.");
    }

    public void Start(string description, TargetPlan? plan = null)
    {
        if (!_arguments.Quiet)
        {
            if (plan is not null)
            {
                var heading = new Paragraph("> " + plan.EntryNames, new Style(decoration: Decoration.Bold));
                if (plan.Entries.Length == 1)
                {
                    heading.Append(plan.DependencySuffix(plan.Entries[0]), new Style(Color.Grey));
                }
                _error.Layout(heading);
            }
            _error.Framework(description, new Style(decoration: Decoration.Bold));
            _error.Framework("", Style.Plain);
        }
    }

    public void Invalid(string heading, IEnumerable<string> diagnostics)
    {
        _error.Framework(heading, new Style(Color.Red, decoration: Decoration.Bold));
        foreach (var diagnostic in diagnostics)
        {
            _error.Layout(new Paragraph(diagnostic) { Overflow = Overflow.Fold }, 2);
        }
    }

    public void InputSources(OptionSnapshot[] options, InputValues values)
    {
        if (!_arguments.Verbose || options.Length == 0)
        {
            return;
        }
        _error.Framework("Input sources", new Style(decoration: Decoration.Bold));
        foreach (var option in options)
        {
            var source = values.Source(option.Declaration);
            var row = new Paragraph(option.Spellings[0], new Style(new Color(173, 214, 240)))
                { Overflow = Overflow.Fold };
            row.Append(": ", Style.Plain);
            if (source.Kind == InputSourceKind.Environment)
            {
                row.Append("environment ", Style.Plain);
                row.Append(source.Variable!, new Style(new Color(173, 214, 240)));
                if (source.Empty)
                {
                    row.Append(" (empty)", Style.Plain);
                }
            }
            else
            {
                row.Append(source.Description, Style.Plain);
            }
            _error.Layout(row, 2);
        }
        _error.Framework("", Style.Plain);
    }

    public void CancellationRequested()
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("Cancellation requested.", new Style(Color.Yellow));
        }
    }

    public void Exception(Exception exception, string scope)
    {
        if (exception is ProcessCaptureLimitException limit)
        {
            _error.Framework("  Process capture limit exceeded | " + limit.Executable, new Style(Color.Red));
            _error.Framework($"  Stream: {StreamName(limit.Stream)} | limit: {limit.Limit} UTF-16 code units", Style.Plain);
            CaptureCounts(limit.PartialStandardOutput, limit.PartialStandardError, partial: true);
            ObservedExit(limit.ObservedExitCode);
            return;
        }
        if (exception is ProcessTimeoutException timeout)
        {
            _error.Framework("  " + timeout.Message, new Style(Color.Red));
            ProcessTimeout(timeout.Timeout, timeout.Elapsed);
            if (timeout is CapturedProcessTimeoutException captured)
            {
                CaptureCounts(captured.PartialStandardOutput, captured.PartialStandardError, partial: true);
                CaptureOverflow(captured.OverflowedStream, captured.CaptureLimit);
            }
            if (timeout.ObservedExitCode is { } observed)
            {
                _error.Framework($"  Observed exit code: {observed}", Style.Plain);
            }
            return;
        }
        if (exception is ProcessFailureException rejected)
        {
            ProcessRejected(rejected, ProcessFailureAction.Throw);
            return;
        }
        if (exception is ProcessLaunchException launch)
        {
            _error.Framework("  " + launch.Message, new Style(Color.Red));
            _error.Framework("  " + launch.InnerException!.Message, Style.Plain);
            _error.Framework("  Working directory: " + launch.Directory, Style.Plain);
            return;
        }
        if (exception is ProcessHandlerException handler)
        {
            _error.Framework("  " + handler.Message, new Style(Color.Red));
            ProcessExit(handler.Failure);
            _error.Literal(handler.InnerException + Environment.NewLine, "  ");
            return;
        }
        if (exception is ProcessCompletionException completion)
        {
            _error.Framework("  " + completion.Message, new Style(Color.Red));
            if (completion.Timeout is { } configured && completion.Elapsed is { } elapsed)
            {
                ProcessTimeout(configured, elapsed);
            }
            if (completion.Capture is { } partial)
            {
                CaptureCounts(partial.Output, partial.Error, partial: true);
                CaptureOverflow(partial.OverflowedStream, partial.Limit);
            }
            if (completion.ExitCode is { } code)
            {
                _error.Framework($"  Observed exit code: {code}", Style.Plain);
            }
            if (completion.InnerException is { } cause)
            {
                _error.Framework("  " + cause.Message, Style.Plain);
            }
            return;
        }
        _error.Framework($"  {scope} failed", new Style(Color.Red));
        _error.Literal(exception.ToString() + Environment.NewLine, "  ");
    }

    public void ProcessStarted(string executable, string directory)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("  Process | " + executable, new Style(decoration: Decoration.Bold));
            if (_arguments.Verbose)
            {
                _error.Framework("  Working directory: " + directory, Style.Plain);
            }
        }
    }

    public void ProcessOutput(string text, bool error) => (error ? _error : _output).Literal(text);

    public void ProcessDeadlineReached(string executable)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("  Process deadline reached | " + executable, new Style(Color.Yellow));
        }
    }

    public void ProcessCaptureLimitReached(string executable)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("  Capture limit exceeded | " + executable, new Style(Color.Yellow));
        }
    }

    public void ProcessCaptured(ProcessCaptureSnapshot capture)
    {
        if (_arguments.Verbose)
        {
            CaptureCounts(capture.Output, capture.Error, partial: false);
        }
    }

    public void ProcessCaptureCancelled(string executable, ProcessCaptureSnapshot capture, int? exitCode)
    {
        if (_arguments.Verbose)
        {
            _error.Framework("  Process cancelled | " + executable, new Style(Color.Yellow));
            CaptureCounts(capture.Output, capture.Error, partial: true);
            CaptureOverflow(capture.OverflowedStream, capture.Limit);
            ObservedExit(exitCode);
        }
    }

    private void CaptureCounts(CapturedStream output, CapturedStream error, bool partial)
    {
        var label = partial ? "Partial capture" : "Capture";
        _error.Framework($"  {label} (UTF-16 code units): stdout {output.Text.Length} | stderr {error.Text.Length}",
            Style.Plain);
    }

    private void CaptureOverflow(ProcessOutputStream? stream, int limit)
    {
        if (stream is { } overflowed)
        {
            _error.Framework($"  Capture limit also exceeded: {StreamName(overflowed)} | limit: {limit} UTF-16 code units",
                Style.Plain);
        }
    }

    private void ObservedExit(int? exitCode)
    {
        if (exitCode is { } observed)
        {
            _error.Framework($"  Observed exit code: {observed}", Style.Plain);
        }
    }

    private static string StreamName(ProcessOutputStream stream)
        => stream == ProcessOutputStream.StandardOutput ? "stdout" : "stderr";

    private void ProcessTimeout(TimeSpan timeout, TimeSpan elapsed)
    {
        _error.Layout(new Paragraph($"Timeout: {DurationInputs.Format(timeout)} | elapsed: {DurationInputs.Format(elapsed)}")
            { Overflow = Overflow.Fold }, 2);
    }

    public void ProcessRejected(ProcessFailureException failure, ProcessFailureAction action)
    {
        var heading = action == ProcessFailureAction.Continue ? "Process failure handled" : "Process failed";
        _error.Framework($"  {heading} | {failure.Executable}",
            new Style(action == ProcessFailureAction.Continue ? Color.Yellow : Color.Red));
        ProcessExit(failure);
        if (action != ProcessFailureAction.Throw)
        {
            _error.Framework($"  Action: {action}", Style.Plain);
        }
    }

    private void ProcessExit(ProcessFailureException failure)
    {
        _error.Framework($"  Exit code: {failure.Result.ExitCode} | accepted: {string.Join(", ", failure.AcceptedExitCodes)}",
            Style.Plain);
        if (failure.Result is CapturedProcessResult captured)
        {
            CaptureCounts(captured.StandardOutput, captured.StandardError, partial: false);
        }
    }

    public void Summary(int code, TimeSpan elapsed, TargetExecution? targets = null)
    {
        if (code == 0 && _arguments.Quiet)
        {
            return;
        }
        var (label, color) = code switch
        {
            0 => ("[OK] Completed", Color.Green),
            130 => ("[Cancelled] Cancelled", Color.Yellow),
            _ => ("[Failed] Failed | invocation", Color.Red)
        };
        if (targets is not null)
        {
            var status = code == 0 ? "OK" : code == 130 ? "Cancelled" : "Failed";
            var detail = code == 0 ? $"{targets.CompletedBodies} target bodies completed"
                : targets.Current is { } current ? $"stopped in {current.Name}" : "invocation";
            if (code == 0 && targets.SkippedBodies > 0)
            {
                detail += $", {targets.SkippedBodies} skipped";
            }
            label = $"[{status}] {targets.Plan.EntryNames} | {detail}";
        }
        _error.Framework("", Style.Plain);
        _error.Framework($"{label} | {Duration(elapsed)}", new Style(color));
    }

    public void TargetStarted(TargetSnapshot target, string dependencySuffix)
    {
        if (_arguments.Quiet)
        {
            return;
        }
        if (_targetStarted)
        {
            _error.Framework("", Style.Plain);
        }
        _targetStarted = true;
        Heading(_error, target, dependencySuffix);
        _error.Framework(target.Description, Style.Plain);
        _error.Framework("", Style.Plain);
    }

    public void TargetCompleted(string name, TimeSpan elapsed)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework($"[OK] {name} | {Duration(elapsed)}", new Style(Color.Green));
        }
    }

    public void TargetSkipped(TargetSnapshot target)
    {
        if (_arguments.Quiet)
        {
            return;
        }
        if (_targetStarted)
        {
            _error.Framework("", Style.Plain);
        }
        _targetStarted = true;
        var text = $"[{target.Name}] Skipped: condition not met ({target.ConditionDescription})";
        _error.Layout(new Paragraph(text, new Style(Color.Grey)) { Overflow = Overflow.Fold }, 0);
    }

    public void CleanupStarted(string owner, string description)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework($"[Cleanup] {owner} | {description}", Style.Plain);
        }
    }

    public void CleanupCompleted(string owner)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework($"[OK] Cleanup | {owner}", new Style(Color.Green));
        }
    }

    public Task CompleteOutputAsync() => _output.CompleteAsync();

    public Task CompleteErrorAsync() => _error.CompleteAsync();

    private void Section(string text) => _output.Framework(text, new Style(Color.Teal, decoration: Decoration.Bold));

    private void TargetHelp(TargetPlan plan)
    {
        var execution = plan.Order.Any(target => target.Condition is not null)
            ? "Each eligible target body runs once; conditions are checked after dependencies."
            : "Each target body runs once.";
        _output.Layout(new Paragraph($"The script selects {plan.EntryNames}. {execution}")
            { Overflow = Overflow.Fold }, 2);
        foreach (var target in plan.Entries.Where(target => target.Body is null).Concat(plan.Order)
            .DistinctBy(target => target.Target))
        {
            _output.Framework("", Style.Plain);
            Heading(_output, target, plan.DependencySuffix(target), 2);
            var description = target.Description + (target.Body is null ? " (Group; no body.)" : "");
            _output.Layout(new Paragraph(description) { Overflow = Overflow.Fold }, 4);
            if (target.ConditionDescription is { } conditionDescription)
            {
                _output.Layout(new Paragraph("When: " + conditionDescription) { Overflow = Overflow.Fold }, 4);
            }
            if (target.Cleanup.Calls > 0)
            {
                _output.Layout(new Paragraph("Cleanup: " + target.Cleanup.Description) { Overflow = Overflow.Fold }, 4);
            }
        }
        _output.Framework("", Style.Plain);
        var bodyOrder = string.Join(", ", plan.Order.Where(target => target.Body is not null).Select(target => target.Name));
        _output.Layout(new Paragraph($"Body order: {bodyOrder}.") { Overflow = Overflow.Fold }, 2);
    }

    private static void Heading(RendererDestination destination, TargetSnapshot target, string suffix, int indent = 0)
    {
        var name = target.Body is null ? "> " + target.Name : $"[{target.Name}]";
        var heading = new Paragraph(name, new Style(decoration: Decoration.Bold)) { Overflow = Overflow.Fold };
        heading.Append(suffix, new Style(Color.Grey));
        destination.Layout(heading, indent);
    }

    private static string Duration(TimeSpan elapsed) => elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";

    private void Control(string name, string description)
    {
        _output.Framework("  " + name, new Style(Color.LightSkyBlue1));
        _output.Layout(new Paragraph(description) { Overflow = Overflow.Fold }, 4);
    }

    private void Input(OptionSnapshot option)
    {
        var paragraph = new Paragraph { Overflow = Overflow.Fold };
        for (var index = 0; index < option.Spellings.Length; index++)
        {
            if (index > 0)
            {
                paragraph.Append(", ", new Style(Color.Grey));
            }
            paragraph.Append(option.Spellings[index], new Style(new Color(173, 214, 240)));
        }
        var typeHint = option.ValueType == typeof(bool) ? " [bool]"
            : option.ValueType == typeof(int) ? " <int>"
            : option.ValueType == typeof(TimeSpan) ? " <duration>"
            : option.EnumChoices is not null ? " <value>" : " <string>";
        paragraph.Append(typeHint, new Style(new Color(223, 196, 255)));
        _output.Layout(paragraph, 2);
        var policy = option.Required ? "Required." : option.HasDefault
            ? $"Default: {FormatDefault(option.DefaultValue)}." : "Optional; no default.";
        _output.Layout(new Paragraph($"{option.Description} {policy}") { Overflow = Overflow.Fold }, 4);
        if (option.EnumChoices is { } choices)
        {
            _output.Layout(new Paragraph($"Allowed: {string.Join(", ", choices.Names)} (case-insensitive).")
                { Overflow = Overflow.Fold }, 4);
        }
        if (option.ValueType == typeof(TimeSpan))
        {
            _output.Layout(new Paragraph("Units: ms, s, m, h.") { Overflow = Overflow.Fold }, 4);
        }
        if (option.ValueType == typeof(bool))
        {
            var flagHelp = option.EnvironmentVariable is null
                ? "Bare flag means true; explicit true/false allowed."
                : "Command line: bare flag means true; explicit true/false allowed.";
            _output.Layout(new Paragraph(flagHelp)
                { Overflow = Overflow.Fold }, 4);
        }
        if (option.EnvironmentVariable is { } environment)
        {
            var mapping = new Paragraph("Environment: ") { Overflow = Overflow.Fold };
            mapping.Append(environment, new Style(new Color(173, 214, 240)));
            mapping.Append(option.ValueType == typeof(bool) ? "; requires true or false." : ".");
            _output.Layout(mapping, 4);
        }
        if (option.Rules.Length > 0)
        {
            _output.Framework(option.Required || option.HasDefault ? "    Rules:" : "    Rules when supplied:",
                Style.Plain);
            foreach (var rule in option.Rules)
            {
                _output.Layout(new Paragraph("- " + rule.Description) { Overflow = Overflow.Fold }, 6);
            }
        }
    }

    private static string FormatDefault(object? value) => value switch
    {
        bool boolean => boolean ? "true" : "false",
        string text => "\"" + text + "\"",
        TimeSpan duration => DurationInputs.Format(duration),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}

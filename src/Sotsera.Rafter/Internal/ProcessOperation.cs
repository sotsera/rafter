using System.Diagnostics;

namespace Sotsera.Rafter.Internal;

internal static class ProcessOperation
{
    public static async Task<ProcessResult> RunAsync(ProcessSettings settings, BodyProcesses body)
    {
        if (settings.Timeout is { } timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        }
        var invocation = body.Invocation;
        ThrowIfCancelled(settings, body);
        if (body.Shutdown.Started)
        {
            throw new OperationCanceledException("The owning body has finished.");
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Owned process execution is currently implemented on Windows only.");
        }
        invocation.Output.ProcessStarted(settings.Executable, settings.Directory);
        ThrowIfCancelled(settings, body);
        if (body.Shutdown.Started)
        {
            throw new OperationCanceledException("The owning body has finished.");
        }
        WindowsProcess process;
        try
        {
            process = WindowsProcess.Start(settings.Executable, settings.Arguments, settings.Directory, settings.Environment,
                removedEnvironment: settings.RemovedEnvironment);
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            throw new ProcessLaunchException(settings.Executable, settings.Directory, exception);
        }
        return await CompleteAsync(settings, body, process).ConfigureAwait(false);
    }

    internal static async Task<ProcessResult> CompleteAsync(ProcessSettings settings, BodyProcesses body,
        IOwnedProcess process, TimeProvider? clock = null)
    {
        var invocation = body.Invocation;
        var cancellation = invocation.Cancellation;
        using (process)
        using (var readerCancellation = new CancellationTokenSource())
        {
            // A caller-supplied TextWriter can block synchronously. Keep the lifecycle supervisor independent
            // of both pumps, then join them even if a drain deadline expires while that writer is still running.
            var outputReader = process.Output;
            var errorReader = process.Error;
            var stop = new ProcessStopState();
            var capture = settings.Capture is { } captureSettings
                ? new ProcessCapture(captureSettings, cancellation, stop, invocation.Output.ProcessOutput) : null;
            void Write(string text, bool error)
            {
                if (capture is null)
                {
                    invocation.Output.ProcessOutput(text, error);
                }
                else
                {
                    capture.Record(text, error);
                }
            }
            var stdout = Task.Run(() => PumpAsync(outputReader, text => Write(text, false),
                readerCancellation.Token));
            var stderr = Task.Run(() => PumpAsync(errorReader, text => Write(text, true),
                readerCancellation.Token));
            var pumps = Task.WhenAll(stdout, stderr);
            var readers = capture?.CompleteOutputAsync(pumps) ?? pumps;
            var deadline = new ProcessDeadline(settings.Timeout, process.ExecutionStarted, clock ?? TimeProvider.System, stop);
            var deadlineNotice = Task.CompletedTask;
            var overflowNotice = Task.CompletedTask;
            var overflowReported = false;
            var localShutdown = new ShutdownWindow();
            var graceDeadline = long.MaxValue;
            var verificationDeadline = long.MaxValue;
            var signalled = false;
            var forced = false;
            var empty = false;
            int? exitCode = null;
            Exception? nativeFailure = null;
            Exception? outputFailure = null;
            try
            {
                while (true)
                {
                    try
                    {
                        exitCode ??= process.ExitCode;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        nativeFailure = exception;
                        localShutdown.Request(force: true);
                    }
                    try
                    {
                        empty = process.IsEmpty;
                        if (deadline.Observe(empty, cancellation, () => process.IsEmpty))
                        {
                            localShutdown.Request();
                            // Reporting can block in a caller's writer; it must not stop lifecycle supervision.
                            deadlineNotice = Task.Run(() => invocation.Output.ProcessDeadlineReached(settings.Executable));
                        }
                        empty |= deadline.ExecutionCompleted;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        nativeFailure = exception;
                        localShutdown.Request(force: true);
                    }
                    if (exitCode.HasValue)
                    {
                        localShutdown.Request();
                    }
                    if (capture?.Overflowed == true)
                    {
                        localShutdown.Request();
                        if (!overflowReported && stop.Reason == ProcessStopReason.CaptureLimit)
                        {
                            overflowReported = true;
                            overflowNotice = Task.Run(() => invocation.Output.ProcessCaptureLimitReached(settings.Executable));
                        }
                    }
                    if (stdout.IsFaulted || stderr.IsFaulted || capture?.Echo.IsFaulted == true)
                    {
                        localShutdown.Request(force: true);
                    }
                    var windows = new[] { cancellation.Processes, body.Shutdown, localShutdown };
                    var active = windows.Where(window => window.Started).ToArray();
                    if (active.Length > 0)
                    {
                        graceDeadline = Math.Min(graceDeadline, active.Min(window => window.GraceDeadline));
                        if (!signalled)
                        {
                            signalled = true;
                            var graceful = false;
                            try
                            {
                                graceful = empty || process.RequestGracefulStop();
                            }
                            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                            {
                                nativeFailure = exception;
                            }
                            if (!graceful)
                            {
                                foreach (var window in active)
                                {
                                    window.Request(force: true);
                                }
                                graceDeadline = Stopwatch.GetTimestamp();
                            }
                        }
                        if (empty || Stopwatch.GetTimestamp() >= graceDeadline)
                        {
                            var verificationWindows = empty ? new[] { localShutdown } : active;
                            foreach (var window in verificationWindows)
                            {
                                window.Request(force: !empty);
                                window.BeginVerification();
                            }
                            if (!empty && !forced)
                            {
                                forced = true;
                                try
                                {
                                    process.ForceStop();
                                }
                                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                                {
                                    nativeFailure = exception;
                                }
                            }
                        }
                        verificationDeadline = Math.Min(verificationDeadline,
                            active.Min(window => window.VerificationDeadline));
                    }
                    var capturedStop = capture is not null &&
                        (stop.Reason == ProcessStopReason.CaptureLimit || cancellation.Token.IsCancellationRequested);
                    if (empty && readers.IsCompleted && (exitCode.HasValue || deadline.Committed || capturedStop))
                    {
                        break;
                    }
                    if (Stopwatch.GetTimestamp() >= verificationDeadline)
                    {
                        break;
                    }
                    await Task.Delay(20).ConfigureAwait(false);
                }
            }
            finally
            {
                // Cancellation interrupts overlapped pipe reads. It does not prove that a pump or its writer
                // has returned, so always await both before releasing readers or allowing final output flush.
                var incomplete = !readers.IsCompleted;
                if (incomplete)
                {
                    await readerCancellation.CancelAsync().ConfigureAwait(false);
                }
                try
                {
                    await readers.ConfigureAwait(false);
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    outputFailure = exception;
                }
                try
                {
                    await deadlineNotice.ConfigureAwait(false);
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    outputFailure ??= exception;
                }
                if (!overflowReported && capture?.Overflowed == true && stop.Reason == ProcessStopReason.CaptureLimit)
                {
                    overflowReported = true;
                    overflowNotice = Task.Run(() => invocation.Output.ProcessCaptureLimitReached(settings.Executable));
                }
                try
                {
                    await overflowNotice.ConfigureAwait(false);
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    outputFailure ??= exception;
                }
                if (!empty || incomplete)
                {
                    var failure = new ProcessCompletionException(settings.Executable, exitCode, !empty,
                        nativeFailure ?? outputFailure, deadline.Committed ? settings.Timeout : null,
                        deadline.Committed ? deadline.Elapsed : null, capture?.Snapshot());
                    invocation.Fail(failure);
                    throw failure;
                }
            }
            if (outputFailure is not null)
            {
                var failure = new ProcessCompletionException(settings.Executable, exitCode, false, outputFailure,
                    deadline.Committed ? settings.Timeout : null, deadline.Committed ? deadline.Elapsed : null,
                    capture?.Snapshot());
                invocation.Fail(failure);
                throw failure;
            }
            var captured = capture?.Snapshot();
            if (captured is { StopReason: ProcessStopReason.CaptureLimit, OverflowedStream: { } stream })
            {
                throw new ProcessCaptureLimitException(settings.Executable, stream, captured.Limit,
                    captured.Output, captured.Error, exitCode);
            }
            if (deadline.Committed)
            {
                if (captured is not null)
                {
                    throw new CapturedProcessTimeoutException(settings.Executable, settings.Timeout!.Value,
                        deadline.Elapsed, exitCode, captured.Output, captured.Error, captured.Limit,
                        captured.OverflowedStream);
                }
                throw new ProcessTimeoutException(settings.Executable, settings.Timeout!.Value, deadline.Elapsed, exitCode);
            }
            if (!exitCode.HasValue)
            {
                if (captured is not null)
                {
                    ThrowIfCancelled(settings, body, captured);
                }
                throw new InvalidOperationException($"Process exit code unavailable | {settings.Executable}", nativeFailure);
            }
            ThrowIfCancelled(settings, body, captured, exitCode);
            if (body.Shutdown.Started)
            {
                throw new OperationCanceledException("The owning body has finished.");
            }
            var succeeded = settings.AcceptedExitCodes.Contains(exitCode.Value);
            var result = captured is null ? new ProcessResult(exitCode.Value, succeeded)
                : new CapturedProcessResult(exitCode.Value, succeeded, captured.Output, captured.Error);
            if (result.Succeeded)
            {
                if (captured is not null)
                {
                    invocation.Output.ProcessCaptured(captured);
                }
                return result;
            }
            var rejected = new ProcessFailureException(settings.Executable, settings.AcceptedExitCodes, result);
            var action = ProcessFailureAction.Throw;
            if (settings.FailureHandler is not null)
            {
                try
                {
                    action = settings.FailureHandler(result);
                    if (!Enum.IsDefined(action))
                    {
                        throw new InvalidOperationException("The process failure handler returned an unknown action.");
                    }
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    throw new ProcessHandlerException(rejected, exception);
                }
            }
            if (action == ProcessFailureAction.Throw)
            {
                throw rejected;
            }
            if (action == ProcessFailureAction.Fail)
            {
                invocation.Fail();
            }
            invocation.Output.ProcessRejected(rejected, action);
            ThrowIfCancelled(settings, body, captured, exitCode);
            if (body.Shutdown.Started)
            {
                throw new OperationCanceledException("The owning body has finished.");
            }
            return result;
        }
    }

    private static void ThrowIfCancelled(ProcessSettings settings, BodyProcesses body,
        ProcessCaptureSnapshot? captured = null, int? exitCode = null)
    {
        var cancellation = body.Invocation.Cancellation;
        if (!cancellation.Token.IsCancellationRequested)
        {
            return;
        }
        if (settings.Capture is { } configuration)
        {
            captured ??= new(CapturedStream.Empty, CapturedStream.Empty, configuration.Limit, null,
                ProcessStopReason.Cancellation);
            body.Invocation.Output.ProcessCaptureCancelled(settings.Executable, captured, exitCode);
            throw new CapturedProcessCanceledException(settings.Executable, exitCode, captured.Output,
                captured.Error, captured.Limit, captured.OverflowedStream, cancellation.Token);
        }
        cancellation.Token.ThrowIfCancellationRequested();
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> write, CancellationToken token)
    {
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }
            write(new string(buffer, 0, count));
        }
    }
}

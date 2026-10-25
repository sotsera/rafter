#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rafter.Verification;

// Test/tool supervision only. Process.Kill(tree) is best effort, not native descendant containment.
public static class VerificationProcess
{
    public static async Task<VerificationProcessResult> RunAsync(ProcessStartInfo start, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("Verification process did not start.");
        using var readerCancellation = new CancellationTokenSource();
        using var exitCancellation = new CancellationTokenSource();
        var output = new StringBuilder();
        var error = new StringBuilder();
        var stdout = start.RedirectStandardOutput
            ? ReadAsync(process.StandardOutput, output, readerCancellation.Token) : Task.CompletedTask;
        var stderr = start.RedirectStandardError
            ? ReadAsync(process.StandardError, error, readerCancellation.Token) : Task.CompletedTask;
        var readers = Task.WhenAll(stdout, stderr);
        var exit = process.WaitForExitAsync(exitCancellation.Token);
        var completion = Task.WhenAll(exit, readers);
        try
        {
            // Root exit and EOF must both fit inside the same execution budget.
            await completion.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return new VerificationProcessResult(process.ExitCode, output.ToString(), error.ToString());
        }
        catch (Exception failure) when (IsRecoverable(failure))
        {
            Exception? cleanupFailure = null;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                await exit.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                cleanupFailure = exception;
            }
            // These cancellations settle our I/O and wait operations, not the descendants. A descendant
            // may already be orphaned after root exit, so callers must retain workspaces on this path.
            await readerCancellation.CancelAsync().ConfigureAwait(false);
            await exitCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await completion.ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                if (exception is not OperationCanceledException)
                {
                    cleanupFailure ??= exception;
                }
            }
            throw new VerificationProcessException(process.Id, exit.IsCompletedSuccessfully,
                output.ToString(), error.ToString(), failure, cleanupFailure);
        }
    }

    private static async Task ReadAsync(StreamReader reader, StringBuilder destination, CancellationToken token)
    {
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }
            destination.Append(buffer, 0, count);
        }
    }

    private static bool IsRecoverable(Exception exception)
    {
        return exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
    }
}

public sealed class VerificationProcessResult
{
    public int ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }

    public VerificationProcessResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }
}

public sealed class VerificationProcessException : Exception
{
    public int ProcessId { get; }
    public bool RootExited { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }

    public VerificationProcessException(int processId, bool rootExited, string standardOutput, string standardError,
        Exception failure, Exception? cleanupFailure)
        : base($"Verification did not complete: {failure.Message} Root PID: {processId}; "
            + $"root exit confirmed: {rootExited}. Descendant cleanup is unverified; retain the workspace."
            + (cleanupFailure is null ? "" : $" Cleanup: {cleanupFailure.Message}"), failure)
    {
        ProcessId = processId;
        RootExited = rootExited;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }
}

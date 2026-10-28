using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Sotsera.Rafter.Internal;

internal sealed class TargetExecution(TargetPlan plan)
{
    public TargetPlan Plan { get; } = plan;
    public TargetSnapshot? Current { get; private set; }
    public int CompletedBodies { get; private set; }
    public int SkippedBodies { get; private set; }

    public async Task RunAsync(RafterContext context, InvocationOutput output)
    {
        foreach (var target in Plan.Order)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (target.Body is null)
            {
                continue;
            }
            Current = target;
            if (target.Condition is { } condition)
            {
                var shouldRun = condition(context);
                if (output.Failure is { } conditionOutputFailure)
                {
                    ExceptionDispatchInfo.Capture(conditionOutputFailure).Throw();
                }
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!shouldRun)
                {
                    SkippedBodies++;
                    output.TargetSkipped(target);
                    Current = null;
                    continue;
                }
            }
            output.TargetStarted(target, Plan.DependencySuffix(target));
            var elapsed = Stopwatch.StartNew();
            try
            {
                await context.RunBodyAsync(target.Body, $"Target '{target.Name}'").ConfigureAwait(false);
                elapsed.Stop();
                if (context.ProcessFailure)
                {
                    return;
                }
                if (output.Failure is { } failure)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
                context.CancellationToken.ThrowIfCancellationRequested();
                CompletedBodies++;
                output.TargetCompleted(target.Name, elapsed.Elapsed);
            }
            finally
            {
                await context.RunCleanupAsync(target.Cleanup, $"Target '{target.Name}'").ConfigureAwait(false);
            }
            if (context.ExecutionFailed)
            {
                return;
            }
            context.CancellationToken.ThrowIfCancellationRequested();
            Current = null;
        }
    }
}

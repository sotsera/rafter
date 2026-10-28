using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Targets;

public sealed class StateCleanupTests
{
    [Theory]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, false, false, false, 0)]
    [InlineData(false, true, false, false, 1)]
    [InlineData(false, false, true, false, 1)]
    [InlineData(false, true, true, false, 1)]
    [InlineData(false, true, true, true, 1)]
    [InlineData(true, true, true, false, 0)]
    [InlineData(false, false, false, true, 1)]
    public async Task Shared_state_and_cleanup_follow_entered_body_order_and_preserve_independent_errors(
        bool skip, bool bodyFailure, bool targetFailure, bool commandFailure, int expected)
    {
        using var host = new CleanupHost();
        var factoryCalls = 0;
        var handle = host.Command.State(() =>
        {
            factoryCalls++;
            return new Box();
        });
        Box? shared = null;
        var order = new List<string>();
        host.Command.Cleanup("Release shared.", context =>
        {
            context.GetState(handle).Should().BeSameAs(shared);
            order.Add("command cleanup");
            if (commandFailure)
            {
                throw new IOException("command cleanup failure");
            }
        });
        var prepare = host.Command.Target("prepare", "Prepare.").Run(context =>
        {
            shared = context.GetState(handle);
            shared.Count++;
            order.Add("prepare");
        }).Cleanup("Release preparation.", context =>
        {
            context.GetState(handle).Count.Should().Be(1);
            order.Add("prepare cleanup");
        });
        var work = host.Command.Target("work", "Work.").DependsOn(prepare)
            .When("Work enabled.", _ => !skip)
            .Run((Action<RafterContext>)(context =>
            {
                context.GetState(handle).Should().BeSameAs(shared);
                order.Add("work");
                if (bodyFailure)
                {
                    throw new InvalidOperationException("body failure");
                }
            }))
            .Cleanup("Release work.", context =>
            {
                context.GetState(handle).Should().BeSameAs(shared);
                order.Add("work cleanup");
                if (targetFailure)
                {
                    throw new IOException("target cleanup failure");
                }
            });
        var finish = host.Command.Target("finish", "Finish.").DependsOn(work).Run(context =>
        {
            context.GetState(handle).Should().BeSameAs(shared);
            order.Add("finish");
        });
        (await host.Command.RunAsync([], finish, work)).Should().Be(expected);
        var expectedOrder = new List<string> { "prepare", "prepare cleanup" };
        if (!skip)
        {
            expectedOrder.AddRange(["work", "work cleanup"]);
        }
        if (skip || (!bodyFailure && !targetFailure))
        {
            expectedOrder.Add("finish");
        }
        expectedOrder.Add("command cleanup");
        order.Should().Equal(expectedOrder);
        factoryCalls.Should().Be(1);
        var error = host.Error.ToString();
        if (!skip && bodyFailure)
        {
            error.Should().Contain("body failure").And.Contain("Target 'work'");
        }
        if (!skip && targetFailure)
        {
            error.Should().Contain("target cleanup failure").And.Contain("Target 'work' cleanup | Release work.");
        }
        if (commandFailure)
        {
            error.Should().Contain("command cleanup failure").And.Contain("Command cleanup | Release shared.");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Help_and_invalid_invocations_do_not_initialize_state_or_clean_up(int kind)
    {
        using var host = new CleanupHost();
        var calls = 0;
        host.Command.State(() =>
        {
            calls++;
            return new Box();
        });
        host.Command.Option<int>("count", "Count.").Default(1).Validate("Positive.", value => value > 0);
        host.Command.Cleanup("Command release.", _ => { calls++; });
        var target = host.Command.Target("target", "Target.").Run(_ => { calls++; })
            .Cleanup("Target release.", _ => { calls++; });
        if (kind == 3)
        {
            target.DependsOn(target);
        }
        if (kind == 4)
        {
            host.Command.RootDirectory(" ");
        }
        string[] arguments = kind switch
        {
            0 => ["--help"],
            1 => ["--count=bad"],
            2 => ["--count=-1"],
            _ => []
        };
        (await host.Command.RunAsync(arguments, target)).Should().Be(kind == 0 ? 0 : 2);
        calls.Should().Be(0);
        if (kind == 0)
        {
            host.Output.ToString().Should().Contain("Command cleanup: Command release.")
                .And.Contain("Cleanup: Target release.");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Invalid_state_or_cleanup_declarations_are_rejected_even_when_unused(int kind)
    {
        using var host = new CleanupHost();
        var calls = 0;
        var target = host.Command.Target("unused", "Unused.");
        if (kind != 7)
        {
            target.Run(_ => { calls++; });
        }
        else
        {
            target.DependsOn(host.Command.Target("leaf", "Leaf.").Run(_ => { calls++; }));
        }
        switch (kind)
        {
            case 0:
                host.Command.State<Box>(null!);
                break;
            case 1:
                host.Command.State(() => Task.FromResult(new Box()));
                break;
            case 2:
                host.Command.Cleanup(" ", _ => { calls++; });
                break;
            case 3:
                host.Command.Cleanup("Release.", (Action<RafterContext>)null!);
                break;
            case 4:
                host.Command.Cleanup("Release.", _ => { }).Cleanup("Again.", _ => { });
                break;
            case 5:
                target.Cleanup("Release.", (Func<RafterContext, Task>)null!);
                break;
            case 6:
                target.Cleanup("Release.", _ => { }).Cleanup("Again.", _ => { });
                break;
            default:
                target.Cleanup("Group release.", _ => { });
                break;
        }
        (await host.Command.RunAsync(["--help"], _ => { calls++; })).Should().Be(2);
        (await host.Command.RunAsync([], _ => { calls++; })).Should().Be(2);
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_null_state_initialization_enters_no_body_or_cleanup(bool returnNull)
    {
        using var host = new CleanupHost();
        var factoryCalls = 0;
        var work = 0;
        host.Command.State<Box>(() =>
        {
            factoryCalls++;
            if (returnNull)
            {
                return null!;
            }
            throw new IOException("state initialization failed");
        });
        host.Command.Cleanup("Never.", _ => { work++; });
        (await host.Command.RunAsync([], _ => { work++; })).Should().Be(1);
        work.Should().Be(0);
        factoryCalls.Should().Be(1);
        host.Error.ToString().Should().Contain("State initialization");
    }

    [Fact]
    public async Task Object_typed_factories_cannot_publish_tasks_as_initialized_state()
    {
        using var host = new CleanupHost();
        var factoryCalls = 0;
        var entered = false;
        host.Command.State<object>(() =>
        {
            factoryCalls++;
            return Task.CompletedTask;
        });
        host.Command.Cleanup("Must not enter.", _ => { entered = true; });
        (await host.Command.RunAsync([], _ => { entered = true; })).Should().Be(1);
        factoryCalls.Should().Be(1);
        entered.Should().BeFalse();
        host.Error.ToString().Should().Contain("State initialization").And.Contain("returned a Task");
    }

    [Fact]
    public async Task Factories_run_after_input_root_preparation_and_cancel_before_work_entry()
    {
        using var host = new CleanupHost();
        var validated = false;
        var entered = false;
        host.Command.Validate("Prepare inputs.", _ =>
        {
            validated = true;
            return true;
        });
        host.Command.State(() =>
        {
            validated.Should().BeTrue();
            host.Signal();
            return new Box();
        });
        host.Command.Cleanup("Not entered.", _ => { entered = true; });
        (await host.Command.RunAsync([], _ => { entered = true; })).Should().Be(130);
        entered.Should().BeFalse();
    }

    [Fact]
    public async Task State_handles_are_typed_owned_and_reset_on_repeated_and_overlapping_runs()
    {
        using var host = new CleanupHost();
        using var other = new CleanupHost();
        var handle = host.Command.State(() => new Box());
        var foreign = other.Command.State(() => new Box());
        var id = host.Command.Option<int>("id", "Invocation.").Default(0);
        var instances = new List<Box>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Command.Cleanup("Inspect state.", context =>
        {
            context.GetState(handle).Count.Should().Be(1);
            context.CancellationToken.CanBeCanceled.Should().BeFalse();
        });
        async Task Body(RafterContext context)
        {
            var value = context.GetState(handle);
            value.Count.Should().Be(0);
            value.Count++;
            instances.Add(value);
            Action wrong = () => context.GetState(foreign);
            wrong.Should().Throw<InvalidOperationException>();
            Action missing = () => context.GetState((State<Box>)null!);
            missing.Should().Throw<ArgumentNullException>();
            if (context.GetValue(id) == 1)
            {
                await release.Task;
            }
            context.GetState(handle).Should().BeSameAs(value);
        }
        var first = host.Command.RunAsync(["--id=1"], Body);
        try
        {
            (await host.Command.RunAsync(["--id=2"], Body)).Should().Be(0);
            first.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.TrySetResult();
            (await first).Should().Be(0);
        }
        (await host.Command.RunAsync([], Body)).Should().Be(0);
        instances.Should().HaveCount(3);
        instances.Distinct(ReferenceEqualityComparer.Instance).Should().HaveCount(3);
    }

    [Fact]
    public async Task State_and_cleanup_declarations_are_snapshotted_before_input_callbacks()
    {
        using var host = new CleanupHost();
        var changes = false;
        State<Box>? later = null;
        var factoryCalls = 0;
        var cleanups = 0;
        var target = host.Command.Target("target", "Target.").Run(context =>
        {
            if (factoryCalls == 0)
            {
                Action read = () => context.GetState(later!);
                read.Should().Throw<InvalidOperationException>();
            }
            else
            {
                context.GetState(later!).Count.Should().Be(0);
            }
        });
        host.Command.Validate("Modify next invocation.", _ =>
        {
            if (!changes)
            {
                changes = true;
                later = host.Command.State(() =>
                {
                    factoryCalls++;
                    return new Box();
                });
                target.Cleanup("Target later.", _ => { cleanups++; });
                host.Command.Cleanup("Command later.", _ => { cleanups++; });
            }
            return true;
        });
        (await host.Command.RunAsync([], target)).Should().Be(0);
        factoryCalls.Should().Be(0);
        cleanups.Should().Be(0);
        (await host.Command.RunAsync([], target)).Should().Be(0);
        factoryCalls.Should().Be(1);
        cleanups.Should().Be(2);
    }

    [Theory]
    [InlineData(false, false, false, 130)]
    [InlineData(true, false, false, 1)]
    [InlineData(false, true, false, 1)]
    [InlineData(false, false, true, 1)]
    public async Task Cleanup_is_uncancelled_and_failures_keep_precedence_over_work_cancellation(
        bool bodyFailure, bool cleanupFailure, bool commandFailure, int expected)
    {
        using var host = new CleanupHost();
        var order = new List<string>();
        host.Command.Cleanup("Command release.", context =>
        {
            context.CancellationToken.IsCancellationRequested.Should().BeFalse();
            order.Add("command");
            if (commandFailure)
            {
                throw new IOException("command release failed after cancellation");
            }
        });
        var target = host.Command.Target("target", "Target.").Run((Action<RafterContext>)(context =>
        {
            host.Signal();
            if (bodyFailure)
            {
                throw new IOException("independent body failure");
            }
            context.CancellationToken.ThrowIfCancellationRequested();
        })).Cleanup("Target release.", async context =>
        {
            context.CancellationToken.CanBeCanceled.Should().BeFalse();
            await Task.Delay(1, context.CancellationToken);
            order.Add("target");
            if (cleanupFailure)
            {
                throw new OperationCanceledException("cleanup cancellation is an independent failure");
            }
        });
        (await host.Command.RunAsync([], target)).Should().Be(expected);
        order.Should().Equal("target", "command");
    }

    [Fact]
    public async Task Async_target_and_command_cleanup_are_joined_before_continuation_and_final_output()
    {
        using var host = new CleanupHost();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var later = false;
        host.Command.Cleanup("Command release.", async _ =>
        {
            commandEntered.SetResult();
            await commandRelease.Task;
        });
        var target = host.Command.Target("target", "Target.").Run(_ => { })
            .Cleanup("Target release.", async _ =>
            {
                entered.SetResult();
                await release.Task;
            });
        var next = host.Command.Target("next", "Next.").DependsOn(target).Run(_ => { later = true; });
        var run = host.Command.RunAsync([], next);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
            later.Should().BeFalse();
            release.SetResult();
            await commandEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            later.Should().BeTrue();
            run.IsCompleted.Should().BeFalse();
            host.Error.ToString().Should().NotContain("target bodies completed");
        }
        finally
        {
            release.TrySetResult();
            commandRelease.TrySetResult();
            (await run).Should().Be(0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_cleanup_tasks_are_failures_and_do_not_hide_body_errors(bool commandCleanup)
    {
        using var host = new CleanupHost();
        var target = host.Command.Target("target", "Target.")
            .Run((Action<RafterContext>)(_ => throw new IOException("original body failure")));
        if (commandCleanup)
        {
            host.Command.Cleanup("Null task.", (Func<RafterContext, Task>)(_ => null!));
        }
        else
        {
            target.Cleanup("Null task.", (Func<RafterContext, Task>)(_ => null!));
        }
        (await host.Command.RunAsync([], target)).Should().Be(1);
        host.Error.ToString().Should().Contain("original body failure").And.Contain("Cleanup returned a null task");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_process_launch_is_unavailable_and_quiet_retains_script_output(bool quiet)
    {
        using var host = new CleanupHost();
        var target = host.Command.Target("target", "Target.").Run(_ => { })
            .Cleanup("Literal [red]release[/].", context =>
            {
                Action launch = () => context.Executable("must-not-start");
                launch.Should().Throw<InvalidOperationException>();
                context.WriteLine("released");
            });
        (await host.Command.RunAsync(quiet ? ["--quiet"] : ["--plain"], target)).Should().Be(0);
        host.Output.ToString().Should().Be("released" + Environment.NewLine);
        if (quiet)
        {
            host.Error.ToString().Should().BeEmpty();
        }
        else
        {
            host.Error.ToString().Should().Contain("Literal [red]release[/].").And.NotContain("\x1b");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_uncertain_native_termination_prevents_cleanup(bool uncertain)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var host = new InvocationHost(Directory.GetCurrentDirectory(), output, error, true, true, false, 100,
            _ => throw new NotSupportedException());
        var rendering = new InvocationOutput(host, new(false, false, false, true, []));
        using var cancellation = new InvocationCancellation(rendering);
        var invocation = new ProcessInvocation(cancellation, rendering);
        invocation.Fail(new ProcessCompletionException("controlled", null, uncertain));
        var resources = InvocationResources.Initialize([]);
        var context = new RafterContext(Directory.GetCurrentDirectory(), rendering, CancellationToken.None,
            new(new Dictionary<OptionDeclaration, object?>()), invocation, resources: resources);
        var ran = false;
        await context.RunCleanupAsync(new("Release resources.", _ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, 1),
            "Command");
        ran.Should().Be(!uncertain);
        resources.Failed.Should().Be(uncertain);
        if (uncertain)
        {
            resources.Diagnostics.Should().ContainSingle().Which.Exception.Message.Should().Contain("not safely attempted");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Predicate_false_or_failure_enters_no_target_cleanup_but_command_cleanup_runs(bool fail)
    {
        using var host = new CleanupHost();
        var calls = 0;
        var commandCalls = 0;
        host.Command.Cleanup("Command.", _ => { commandCalls++; });
        var target = host.Command.Target("target", "Target.").When("Guard.", _ =>
        {
            if (fail)
            {
                throw new IOException("predicate failure");
            }
            return false;
        }).Run(_ => { calls++; }).Cleanup("Target.", _ => { calls++; });
        host.Command.Target("unused", "Unused.").Run(_ => { calls++; }).Cleanup("Unused.", _ => { calls++; });
        (await host.Command.RunAsync([], target)).Should().Be(fail ? 1 : 0);
        calls.Should().Be(0);
        commandCalls.Should().Be(1);
    }

    [Fact]
    public async Task Two_state_handles_with_the_same_factory_remain_distinct_and_state_is_not_implicitly_disposed()
    {
        using var host = new CleanupHost();
        Func<DisposableBox> factory = () => new DisposableBox();
        var first = host.Command.State(factory);
        var second = host.Command.State(factory);
        DisposableBox? value = null;
        (await host.Command.RunAsync([], context =>
        {
            value = context.GetState(first);
            context.GetState(second).Should().NotBeSameAs(value);
        })).Should().Be(0);
        value!.Disposed.Should().BeFalse("state storage does not claim ownership of arbitrary state objects");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_waits_for_active_cancellation_registrations_and_preserves_their_errors(bool fail)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var host = new InvocationHost(Directory.GetCurrentDirectory(), output, error, true, true, false, 100,
            _ => throw new NotSupportedException());
        var rendering = new InvocationOutput(host, new(false, false, false, true, []));
        using var cancellation = new InvocationCancellation(rendering);
        var invocation = new ProcessInvocation(cancellation, rendering);
        var resources = InvocationResources.Initialize([]);
        var context = new RafterContext(Directory.GetCurrentDirectory(), rendering, CancellationToken.None,
            new(new Dictionary<OptionDeclaration, object?>()), invocation, resources: resources);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var registration = cancellation.Token.Register(() =>
        {
            entered.TrySetResult();
            release.Wait();
            if (fail)
            {
                throw new IOException("cancellation callback failed");
            }
        });
        cancellation.Request();
        var ran = false;
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cleanup = context.RunCleanupAsync(new("Release.", _ =>
            {
                ran = true;
                return Task.CompletedTask;
            }, 1),
                "Command");
            cleanup.IsCompleted.Should().BeFalse();
            ran.Should().BeFalse();
        }
        finally
        {
            release.Set();
            if (cleanup is not null)
            {
                await cleanup;
            }
        }
        ran.Should().BeTrue();
        resources.Failed.Should().Be(fail);
        if (fail)
        {
            resources.Diagnostics.Should().ContainSingle().Which.Scope.Should().Be("Cancellation callback");
        }
    }

    [Fact]
    public async Task A_failed_cleanup_heading_does_not_prevent_resource_release_or_command_cleanup()
    {
        using var error = new CleanupHeadingWriter();
        using var host = new CleanupHost(error);
        var releases = 0;
        host.Command.Cleanup("Command release.", _ => { releases++; });
        var target = host.Command.Target("target", "Target.").Run(_ => { })
            .Cleanup("Target release.", _ => { releases++; });
        (await host.Command.RunAsync([], target)).Should().Be(1);
        releases.Should().Be(2);
    }

    [Fact]
    public async Task Body_executable_defaults_cannot_be_reused_from_cleanup_after_their_owner_has_settled()
    {
        using var host = new CleanupHost();
        Executable? executable = null;
        var target = host.Command.Target("target", "Target.").Run(context =>
        {
            executable = context.Executable("must-not-start");
        }).Cleanup("Release.", async _ =>
        {
            Func<Task> launch = async () => { await executable!.RunAsync(); };
            await launch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*has finished*");
        });
        (await host.Command.RunAsync([], target)).Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_during_command_cleanup_does_not_start_old_work_registrations()
    {
        using var host = new CleanupHost();
        var registeredCalls = 0;
        CancellationTokenRegistration registration = default;
        host.Command.Cleanup("Command release.", context =>
        {
            host.Signal();
            registeredCalls.Should().Be(0);
            context.CancellationToken.CanBeCanceled.Should().BeFalse();
        });
        try
        {
            (await host.Command.RunAsync([], context =>
            {
                registration = context.CancellationToken.Register(() => { registeredCalls++; });
            })).Should().Be(130);
            registeredCalls.Should().Be(0);
        }
        finally
        {
            registration.Dispose();
        }
    }

    [Fact]
    public async Task Rethrowing_the_same_exception_from_work_and_both_cleanup_scopes_preserves_all_scopes()
    {
        using var host = new CleanupHost();
        var failure = new IOException("shared failure instance");
        host.Command.Cleanup("Command release.", (Action<RafterContext>)(_ => throw failure));
        var target = host.Command.Target("target", "Target.")
            .Run((Action<RafterContext>)(_ => throw failure))
            .Cleanup("Target release.", (Action<RafterContext>)(_ => throw failure));
        (await host.Command.RunAsync([], target)).Should().Be(1);
        host.Error.ToString().Should().Contain("Target 'target' failed")
            .And.Contain("Target 'target' cleanup | Target release.")
            .And.Contain("Command cleanup | Command release.");
    }

    private sealed class DisposableBox : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class CleanupHeadingWriter : StringWriter
    {
        public override void Write(string? value)
        {
            if (value?.Contains("[Cleanup]", StringComparison.Ordinal) == true)
            {
                throw new IOException("cleanup heading failed");
            }
            base.Write(value);
        }
    }

    private sealed class Box
    {
        public int Count { get; set; }
    }

    private sealed class CleanupHost : IDisposable
    {
        private readonly List<Action> _signals = [];
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; }
        public Command Command { get; }

        public CleanupHost(StringWriter? error = null)
        {
            Error = error ?? new StringWriter();
            Command = new("State and cleanup verification.", () => new InvocationHost(Directory.GetCurrentDirectory(),
                Output, Error, true, true, false, 120, signal =>
                {
                    _signals.Add(signal);
                    return new Subscription(() => _signals.Remove(signal));
                }));
        }

        public void Signal()
        {
            foreach (var signal in _signals.ToArray())
            {
                signal();
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

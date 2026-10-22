using Sotsera.Rafter;

internal static class Scenario
{
    public static async Task<int> RunAsync(string[] args)
    {
        var scenario = args.FirstOrDefault() ?? "sync";
        var controls = args.Skip(1).ToArray();
        if (scenario == "environment")
        {
            return await EnvironmentAsync(controls);
        }
        if (scenario == "enums")
        {
            return await EnumsAsync(controls);
        }
        if (scenario == "review-child")
        {
            Console.WriteLine("  Child [red]literal[/] output.");
            Console.Error.WriteLine("Child diagnostic.");
            return int.Parse(controls[0]);
        }
        if (scenario.StartsWith("review-", StringComparison.Ordinal))
        {
            return await ReviewAsync(scenario, controls);
        }
        var command = Rafter.Command("Print a greeting.");
        var output = Console.Out;
        var error = Console.Error;
        var cwd = Directory.GetCurrentDirectory();
        int result;

        if (scenario is "targets" or "targets-failure")
        {
            var name = command.Option<string>("name", "Name.").Default("Rafter");
            var first = command.Target("first", "Prepare a result.").Run(context =>
            {
                context.WriteLine("First: " + context.GetValue(name));
                if (scenario == "targets-failure")
                {
                    throw new InvalidOperationException("Target failed after output.");
                }
            });
            var second = command.Target("second", "Await and write the next result.").DependsOn(first)
                .Run(async context =>
                {
                    await Task.Yield();
                    context.WriteLine("Second: " + context.GetValue(name));
                });
            var group = command.Target("group", "Run both bodies.").DependsOn(first, second);
            result = await command.RunAsync(controls, group, second, first);
        }
        else if (scenario == "process")
        {
            result = await command.RunAsync(controls, async context =>
            {
                var process = await context.Process("dotnet").Arguments("--version").RunAsync();
                context.WriteLine($"Process result: {process.ExitCode}:{process.Succeeded}");
            });
        }
        else if (scenario == "typed")
        {
            var text = command.Option<string>("text", "Optional text.");
            var number = command.Option<int>("number", "Optional integer.");
            var flag = command.Option<bool>("flag", "Optional Boolean.");
            var name = command.Option<string>("name", "Required text.").Required()
                .Aliases("n").Validate("Nonempty.", value => value.Length > 0);
            var count = command.Option<int>("count", "Defaulted integer.").Default(4)
                .Aliases("c").Validate("Positive.", value => value > 0);
            var enabled = command.Option<bool>("enabled", "Required Boolean.").Required();
            command.Validate("Types are available during validation.", inputs =>
            {
                string? optionalText = inputs.GetValue(text);
                int? optionalNumber = inputs.GetValue(number);
                bool? optionalFlag = inputs.GetValue(flag);
                string requiredName = inputs.GetValue(name);
                int defaultCount = inputs.GetValue(count);
                bool requiredFlag = inputs.GetValue(enabled);
                return optionalText is null && optionalNumber is null && optionalFlag is null
                    && requiredName.Length > 0 && defaultCount > 0 && !requiredFlag;
            });
            result = await command.RunAsync(controls, context =>
            {
                string? optionalText = context.GetValue(text);
                int? optionalNumber = context.GetValue(number);
                bool? optionalFlag = context.GetValue(flag);
                string requiredName = context.GetValue(name);
                int defaultCount = context.GetValue(count);
                bool requiredFlag = context.GetValue(enabled);
                context.WriteLine($"{requiredName}:{defaultCount}:{requiredFlag}:{optionalText}:{optionalNumber}:{optionalFlag}");
            });
        }
        else if (scenario == "invalid")
        {
            result = await Rafter.Command(" ").RunAsync(controls, context => context.WriteLine("NOT EXECUTED"));
        }
        else if (scenario == "async")
        {
            result = await command.RunAsync(controls, async context =>
            {
                await Task.Delay(25, context.CancellationToken);
                context.WriteLine("Async body finished.");
            });
        }
        else if (scenario == "failure")
        {
            result = await command.RunAsync(controls, (Action<RafterContext>)(context =>
            {
                context.WriteLine("Before failure.");
                try
                {
                    throw new ArgumentException("Inner [red]literal[/] cause.");
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("Outer cause.", inner);
                }
            }));
        }
        else if (scenario == "cancel")
        {
            result = await command.RunAsync(controls, async context =>
            {
                context.WriteLine("Ready for Ctrl+C.");
                try
                {
                    await Task.Delay(Timeout.Infinite, context.CancellationToken);
                }
                finally
                {
                    context.WriteLine("Managed cleanup finished.");
                }
            });
        }
        else
        {
            result = await command.RunAsync(controls, context =>
                context.WriteLine("  Hello [red]literal[/].\n\n界面 cafe\u0301"));
        }

        if (!ReferenceEquals(output, Console.Out) || !ReferenceEquals(error, Console.Error)
            || cwd != Directory.GetCurrentDirectory())
        {
            throw new InvalidOperationException("Rafter changed host state.");
        }
        // Optional test evidence for terminal hosts that translate a native exit code after Ctrl+C.
        if (Environment.GetEnvironmentVariable("RAFTER_FIXTURE_RESULT") is { Length: > 0 } resultPath)
        {
            await File.WriteAllTextAsync(resultPath, result.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return result;
    }

    private static async Task<int> EnumsAsync(string[] arguments)
    {
        var variable = arguments[0];
        var command = Rafter.Command("Resolve enum inputs.");
        var optional = command.Option<BuildConfiguration>("optional", "Optional configuration.");
        var original = command.Option<BuildConfiguration>("configuration", "Build configuration.")
            .FromEnvironment(variable).Aliases("c");
        var configuration = original.Default(BuildConfiguration.Release);
        var required = command.Option<BuildConfiguration>("required", "Required configuration.").Required()
            .Validate("Debug is required.", value => value == BuildConfiguration.Debug);
        command.Validate("Prepared enum types.", inputs =>
        {
            BuildConfiguration? absent = inputs.GetValue(optional);
            BuildConfiguration? retained = inputs.GetValue(original);
            BuildConfiguration actual = inputs.GetValue(configuration);
            BuildConfiguration supplied = inputs.GetValue(required);
            return absent is null && retained == actual && supplied == BuildConfiguration.Debug;
        });
        return await command.RunAsync(arguments[1..], context =>
        {
            BuildConfiguration? absent = context.GetValue(optional);
            BuildConfiguration? retained = context.GetValue(original);
            BuildConfiguration actual = context.GetValue(configuration);
            BuildConfiguration supplied = context.GetValue(required);
            Environment.SetEnvironmentVariable(variable, "changed after preparation");
            if (context.GetValue(configuration) != actual || retained != actual)
            {
                throw new InvalidOperationException("Prepared enum values changed during execution.");
            }
            context.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                Optional = absent?.ToString(),
                Configuration = actual.ToString(),
                Required = supplied.ToString()
            }));
        });
    }

    private static async Task<int> EnvironmentAsync(string[] arguments)
    {
        var prefix = arguments[0];
        var command = Rafter.Command("Resolve environment inputs.");
        var original = command.Option<string>("name", "Name.").Aliases("n").FromEnvironment(prefix + "_NAME");
        var name = original.Required();
        var count = command.Option<int>("count", "Count.").Default(3).FromEnvironment(prefix + "_COUNT")
            .Validate("Must be nonnegative.", value => value >= 0);
        var flag = command.Option<bool>("flag", "Flag.").FromEnvironment(prefix + "_FLAG");
        return await command.RunAsync(arguments[1..], context =>
        {
            string actualName = context.GetValue(name);
            string? optionalName = context.GetValue(original);
            int actualCount = context.GetValue(count);
            bool? actualFlag = context.GetValue(flag);
            Environment.SetEnvironmentVariable(prefix + "_NAME", "changed after preparation");
            if (context.GetValue(name) != actualName || optionalName != actualName)
            {
                throw new InvalidOperationException("Prepared values changed during execution.");
            }
            context.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = actualName,
                Count = actualCount,
                Flag = actualFlag
            }));
        });
    }

    private static async Task<int> ReviewAsync(string scenario, string[] controls)
    {
        var command = Rafter.Command("Review actual runtime output with dependencies and child processes.");
        var name = command.Option<string>("name",
                "Name with literal [red]brackets[/], Unicode 界面, and a long description that wraps.")
            .Aliases("n", "recipient")
            .Default("Ada")
            .Validate("Name must not be blank.", value => !string.IsNullOrWhiteSpace(value));
        command.Option<bool>("enabled", "Enable the sample behavior.").Default(false);
        command.Validate("Review combined input rules without executing them during help.", _ => true);
        var build = command.Target("build", "Run a deterministic child fixture.")
            .Run(async context =>
        {
            var executable = scenario == "review-launch" ? "rafter-missing-review-tool" : "dotnet";
            var process = context.Process(executable)
                .Arguments(typeof(Scenario).Assembly.Location, "review-child", scenario is "review-help" or "review-success" ? "0" : "1")
                .Environment("RAFTER_CAPTURE", "");
            if (scenario == "review-continue")
            {
                process.OnFailure(_ => ProcessFailureAction.Continue);
            }
            if (scenario == "review-fail")
            {
                process.OnFailure(_ => ProcessFailureAction.Fail);
            }
            if (scenario == "review-handler")
            {
                process.OnFailure(_ => throw new InvalidOperationException("Handler failed.", new ArgumentException("Inner cause.")));
            }
            await process.RunAsync();
            context.WriteLine("Result for " + context.GetValue(name) + ".");
        });
        var test = command.Target("test", "Run after build.")
            .DependsOn(build)
            .Run(context =>
        {
            context.WriteLine("Next target ran.");
        });
        var verify = command.Target("verify", "Review both target bodies.").DependsOn(build, test);
        return await command.RunAsync(controls, verify);
    }

    private enum BuildConfiguration
    {
        Debug = 20,
        Release = -1
    }
}

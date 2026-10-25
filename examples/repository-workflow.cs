#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using System.Globalization;
using System.Text.Json;

// Revisit process setup as its authoring APIs evolve; see the development plan.
var command = Rafter.Command("Build and test Rafter, inspect package metadata, and optionally pack locally.");

var root = command.Option<string>("root", "Repository root; relative paths use the invocation directory.")
    .Default(".")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value));

command.RootDirectory(root);

var configuration = command.Option<BuildConfiguration>("configuration", "Build and test configuration.")
    .Default(BuildConfiguration.Release);

var fullTests = command.Option<bool>("full-tests", "Include the long-running consumer and native deadline tests.")
    .Default(false);

var pack = command.Option<bool>("pack", "Create a local package in this run's evidence directory after tests pass.")
    .Default(false);

var timeout = command.Option<TimeSpan>("timeout", "Execution deadline for each child process, including output drain.")
    .Default(TimeSpan.FromMinutes(10))
    .Validate("Must be greater than zero.", value => value > TimeSpan.Zero);

return await command.RunAsync(args, async context =>
{
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException("This workflow uses Rafter's Windows process ownership.");
    }

    var repository = context.RootDirectory;
    var solution = context.File("Rafter.slnx");
    var project = context.File("src", "Sotsera.Rafter", "Sotsera.Rafter.csproj");
    if (!solution.Exists() || !project.Exists())
    {
        throw new InvalidOperationException("The selected root must contain Rafter.slnx and the Rafter runtime project.");
    }

    var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)
        + "-" + Guid.NewGuid().ToString("N");
    var evidence = context.Directory("artifacts", "evidence", "workflows", "repository", runId)
        .EnsureExists();
    context.WriteLine($"Workflow evidence: {evidence.FullPath}");

    var buildConfiguration = context.GetValue(configuration).ToString();
    var record = new WorkflowRecord
    {
        Repository = repository,
        Configuration = buildConfiguration,
        FullTests = context.GetValue(fullTests),
        PackRequested = context.GetValue(pack)
    };

    ProcessCommand Dotnet(params string[] arguments)
    {
        return context.Process("dotnet")
            .Environment("DOTNET_NOLOGO", "1")
            .Environment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .Environment("DOTNET_CLI_USE_MSBUILD_SERVER", "0")
            .Timeout(context.GetValue(timeout))
            .Arguments(arguments);
    }

    try
    {
        var metadata = await Dotnet("msbuild", project.FullPath, "-nologo", "-getProperty:PackageId,Version,TargetFramework",
                "-property:Configuration=" + buildConfiguration)
            .RunAndCaptureAsync(maxCharactersPerStream: 16 * 1024);
        using (var document = JsonDocument.Parse(metadata.StandardOutput.Text))
        {
            var properties = document.RootElement.GetProperty("Properties");
            record.PackageId = properties.GetProperty("PackageId").GetString();
            record.Version = properties.GetProperty("Version").GetString();
            record.TargetFramework = properties.GetProperty("TargetFramework").GetString();
        }
        await evidence.File("package-properties.json")
            .WriteAllTextAsync(metadata.StandardOutput.Text, context.CancellationToken);
        record.CompletedStages.Add("inspect");
        context.WriteLine($"Package: {record.PackageId} {record.Version} ({record.TargetFramework})");

        await Dotnet("build", solution.FullPath, "--configuration", buildConfiguration, "--disable-build-servers", "-m:1",
                "-flp:LogFile=" + evidence.File("build.log").FullPath + ";Verbosity=normal")
            .RunAsync();
        record.CompletedStages.Add("build");

        var testArguments = new List<string>
        {
            "test", "--solution", solution.FullPath, "--configuration", buildConfiguration, "--no-build",
            "--minimum-expected-tests", "1", "--max-parallel-test-modules", "1",
            "--report-trx", "--results-directory", evidence.Directory("tests").FullPath
        };
        if (record.FullTests)
        {
            testArguments.Add("-p:IncludeLongRunningTests=true");
        }
        await Dotnet(testArguments.ToArray()).RunAsync();
        record.CompletedStages.Add("test");

        if (record.PackRequested)
        {
            record.PackageDirectory = evidence.Directory("packages").FullPath;
            await Dotnet("pack", project.FullPath, "--configuration", buildConfiguration, "--no-build", "--no-restore",
                    "--output", record.PackageDirectory,
                    "-flp:LogFile=" + evidence.File("pack.log").FullPath + ";Verbosity=normal")
                .RunAsync();
            record.CompletedStages.Add("pack");
            context.WriteLine($"Local packages: {record.PackageDirectory}");
        }
        else
        {
            context.WriteLine("Packaging skipped; use --pack to create a local package.");
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        record.Succeeded = true;
    }
    finally
    {
        record.FinishedAtUtc = DateTimeOffset.UtcNow;
        // Process awaits settle their owned work before this evidence write, even after cancellation/failure.
        await evidence.File("run.json")
            .WriteAllTextAsync(JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                CancellationToken.None);
    }
});

enum BuildConfiguration
{
    Debug,
    Release
}

sealed class WorkflowRecord
{
    public string Repository { get; init; } = "";
    public string Configuration { get; init; } = "";
    public bool FullTests { get; init; }
    public bool PackRequested { get; init; }
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public List<string> CompletedStages { get; } = [];
    public string? PackageId { get; set; }
    public string? Version { get; set; }
    public string? TargetFramework { get; set; }
    public string? PackageDirectory { get; set; }
}

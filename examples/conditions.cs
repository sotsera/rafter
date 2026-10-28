#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Build, optionally test, and optionally pack this repository.");
var root = command.Option<string>("root", "Repository root.").Default(".");
var skipTests = command.Option<bool>("skip-tests", "Skip the test target.").Default(false);
var createPackages = command.Option<bool>("pack", "Create local development packages.").Default(false);
command.RootDirectory(root);

var build = command.Target("build", "Build the repository in Release configuration.")
    .Run(async context =>
    {
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Build("Rafter.slnx")
            .Configuration("Release")
            .DisableBuildServers()
            .MaxCpuCount(1)
            .RunAsync();
    });

var test = command.Target("test", "Run the ordinary repository tests.")
    .DependsOn(build)
    .When("Tests were not disabled with --skip-tests.", context =>
    {
        return !context.GetValue(skipTests);
    })
    .Run(async context =>
    {
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Arguments("test", "--solution", "Rafter.slnx")
            .Arguments("--configuration", "Release", "--no-build")
            .Arguments("--minimum-expected-tests", "1", "--max-parallel-test-modules", "1")
            .RunAsync();
    });

var pack = command.Target("pack", "Create local development packages.")
    .DependsOn(test)
    .When("Packaging was requested with --pack.", context =>
    {
        return context.GetValue(createPackages);
    })
    .Run(async context =>
    {
        var packages = context.Directory("artifacts", "packages").EnsureExists();
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Pack("src/Sotsera.Rafter/Sotsera.Rafter.csproj")
            .Configuration("Release")
            .NoBuild()
            .Output(packages.FullPath)
            .RunAsync();
    });

var verify = command.Target("verify", "Build, test when enabled, and pack when requested.")
    .DependsOn(test, pack);

return await command.RunAsync(args, verify);

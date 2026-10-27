#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Build a project with a process deadline and wait for owned work to settle.");

var project = command.Option<string>("project", "The project to build.")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value))
    .Required();

var timeout = command.Option<TimeSpan>("timeout", "Child execution deadline; shutdown may take longer.")
    .FromEnvironment("RAFTER_PROCESS_TIMEOUT")
    .Default(TimeSpan.FromSeconds(10))
    .Validate("Must be greater than zero.", value => value > TimeSpan.Zero);

return await command.RunAsync(args, async context =>
{
    await context.Executable("dotnet")
        .Arguments("build", context.GetValue(project))
        .Timeout(context.GetValue(timeout))
        .RunAsync();

    context.WriteLine("Build completed before its process deadline.");
});

#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Inspect a repository's status and recent commits.");
var root = command.Option<string>("root", "Repository root.").Default(".");
var count = command.Option<int>("count", "Maximum number of recent commits.").Default(5)
    .Validate("Must be greater than zero.", value => value > 0);
command.RootDirectory(root);

return await command.RunAsync(args, async context =>
{
    var git = context.Git().Timeout(TimeSpan.FromMinutes(1));
    var status = await git.Status().Short().Branch().RunAndCaptureAsync(32 * 1024);
    context.WriteLine(status.StandardOutput.Text.TrimEnd());
    var log = await git.Log().MaxCount(context.GetValue(count)).Oneline().RunAndCaptureAsync(16 * 1024);
    context.WriteLine(log.StandardOutput.Text.TrimEnd());
});

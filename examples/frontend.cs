#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Optionally install a frontend's locked dependencies, then run a package script.");
var root = command.Option<string>("root", "Frontend package root.").Default(".");
var script = command.Option<string>("script", "Package script to run.").Default("build")
    .Validate("Must not be blank or begin with '-'.", value => !string.IsNullOrWhiteSpace(value) && !value.StartsWith('-'));
var install = command.Option<bool>("install", "Run npm ci before the package script.").Default(false);
command.RootDirectory(root);

return await command.RunAsync(args, async context =>
{
    if (!context.File("package.json").Exists())
    {
        throw new InvalidOperationException("The selected root must contain package.json.");
    }
    var npm = context.Npm().Timeout(TimeSpan.FromMinutes(5));
    if (context.GetValue(install))
    {
        await npm.Ci().NoFund().RunAsync();
    }
    await npm.RunScript(context.GetValue(script)).RunAsync();
});

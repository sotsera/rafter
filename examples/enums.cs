#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Select a build configuration from command-line, environment, or default inputs.");

var configuration = command.Option<BuildConfiguration>("configuration", "Build configuration.")
    .FromEnvironment("RAFTER_CONFIGURATION")
    .Default(BuildConfiguration.Release);

return await command.RunAsync(args, context =>
{
    var selectedConfiguration = context.GetValue(configuration);
    context.WriteLine($"Selected configuration: {selectedConfiguration}.");
});

enum BuildConfiguration
{
    Debug,
    Release
}

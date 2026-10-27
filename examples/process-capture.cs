#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using System.Reflection;
using System.Text.Json;

if (args is ["--capture-child", var childScenario])
{
    if (childScenario == "Failure")
    {
        Console.Error.WriteLine("The child could not produce a greeting.");
        return 7;
    }

    if (childScenario == "Overflow")
    {
        Console.Write(new string('x', 2048));
        return 0;
    }

    Console.WriteLine("""{"name":"Ada","message":"Hello, Ada!"}""");
    Console.Error.WriteLine("Child diagnostic.");
    return 0;
}

var command = Rafter.Command("Capture a child's JSON within explicit bounds, with optional live echo.");

var scenario = command.Option<CaptureScenario>("scenario", "Choose JSON output, a rejected exit, or capture overflow.")
    .Default(CaptureScenario.Success);

var limit = command.Option<int>("limit", "Maximum UTF-16 code units retained per stream.")
    .Default(1024)
    .Validate("Must be greater than zero.", value => value > 0);

var echo = command.Option<bool>("echo", "Also display the child's output live.")
    .Default(false);

var showOrder = command.Option<bool>("show-order", "Display captured chunks in Rafter's observed order.")
    .Default(false);

return await command.RunAsync(args, async context =>
{
    var result = await context.Executable("dotnet")
        .Arguments(Assembly.GetExecutingAssembly().Location, "--capture-child", context.GetValue(scenario).ToString())
        .Timeout(TimeSpan.FromSeconds(10))
        .RunAndCaptureAsync(maxCharactersPerStream: context.GetValue(limit), echo: context.GetValue(echo));

    using var document = JsonDocument.Parse(result.StandardOutput.Text);
    var name = document.RootElement.GetProperty("name").GetString();
    var message = document.RootElement.GetProperty("message").GetString();
    context.WriteLine($"Captured greeting for {name}: {message}");

    if (context.GetValue(showOrder))
    {
        var chunks = result.StandardOutput.Chunks.Select(chunk => (Stream: "stdout", Chunk: chunk))
            .Concat(result.StandardError.Chunks.Select(chunk => (Stream: "stderr", Chunk: chunk)))
            .OrderBy(entry => entry.Chunk.Order);

        foreach (var entry in chunks)
        {
            var text = JsonSerializer.Serialize(entry.Chunk.Text);
            context.WriteLine($"{entry.Chunk.Order} {entry.Stream}: {text}");
        }
    }
});

enum CaptureScenario
{
    Success,
    Failure,
    Overflow
}

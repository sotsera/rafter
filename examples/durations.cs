#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using System.Globalization;

// Input examples: --delay 1h30m --offset=-2m10s
var command = Rafter.Command("Inspect duration inputs without starting timers or imposing deadlines.");

var delay = command.Option<TimeSpan>("delay", "Delay to display.")
    .FromEnvironment("RAFTER_DELAY")
    .Default(TimeSpan.FromSeconds(30))
    .Validate("Must be zero or greater.", value => value >= TimeSpan.Zero);

var offset = command.Option<TimeSpan>("offset", "Signed offset to display.");

return await command.RunAsync(args, context =>
{
    var delayValue = context.GetValue(delay);
    var offsetValue = context.GetValue(offset);
    var offsetText = offsetValue?.ToString("c", CultureInfo.InvariantCulture) ?? "not supplied";

    context.WriteLine($"Delay: {delayValue.ToString("c", CultureInfo.InvariantCulture)}");
    context.WriteLine($"Offset: {offsetText}");
});

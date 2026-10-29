#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using System.Net.Http;
using System.Net.Http.Headers;

var command = Rafter.Command("Preview a request using disposable example tokens and redacted output.");

var token = command.Option<string>("token", "Example bearer token; use synthetic values for this demonstration.")
    .Aliases("t")
    .FromEnvironment("RAFTER_TOKEN")
    .Sensitive()
    .Validate("Use at least eight characters.", value => value.Length >= 8)
    .Default("demo-token-only");

var secondaryToken = command.Option<string>("secondary-token", "An optional second example token.")
    .FromEnvironment("RAFTER_SECONDARY_TOKEN")
    .Sensitive()
    .Validate("Use at least eight characters when supplied.", value => value.Length >= 8);

var fail = command.Option<bool>("fail", "Throw an example exception containing the raw authorization header.")
    .Default(false);

var preview = command.Target("preview", "Prepare the request and show its redacted headers.")
    .Run(context =>
    {
        // GetValue returns the original string for the API that actually needs it.
        var rawToken = context.GetValue(token);
        var rawSecondaryToken = context.GetValue(secondaryToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/api/check");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);

        if (rawSecondaryToken is not null)
        {
            request.Headers.Add("X-Secondary-Token", rawSecondaryToken);
        }

        // Rafter masks registered values at this output boundary.
        context.WriteLine($"Authorization: {request.Headers.Authorization}");

        if (rawSecondaryToken is not null)
        {
            context.WriteLine($"X-Secondary-Token: {rawSecondaryToken}");
        }

        if (context.GetValue(fail))
        {
            // The exception object stays unchanged; Rafter masks its rendered details.
            throw new InvalidOperationException($"Example request failed for {request.Headers.Authorization}.");
        }

        context.WriteLine("Request prepared; no network request was sent.");
    });

return await command.RunAsync(args, preview);

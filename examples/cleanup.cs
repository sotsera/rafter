#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Share a temporary workspace and release resources at their owning scope.");
var skipVerification = command.Option<bool>("skip-verification", "Skip verification.").Default(false);
var fail = command.Option<bool>("fail", "Fail inside verification.").Default(false);
var failCleanup = command.Option<bool>("fail-cleanup", "Fail after releasing verification resources.").Default(false);

// A typed handle, with a fresh state object for each invocation.
var resources = command.State(() => new WorkflowResources());

command.Cleanup("Release the shared workspace after all entered work settles.", context =>
{
    var state = context.GetState(resources);
    if (state.Workspace is { } workspace && workspace.Exists())
    {
        System.IO.Directory.Delete(workspace.FullPath, recursive: true);
        state.Workspace = null;
        context.WriteLine("Shared workspace released.");
    }
});

var prepare = command.Target("prepare", "Prepare shared input and target-local scratch data.")
    .Run(async context =>
    {
        var state = context.GetState(resources);

        // Record ownership before I/O so cleanup can handle partial preparation.
        state.Workspace = context.Directory("artifacts", "work", "cleanup-" + Guid.NewGuid().ToString("N"));
        var workspace = state.Workspace.EnsureExists();

        state.PreparationFile = workspace.File("prepare.tmp");
        await state.PreparationFile.WriteAllTextAsync("Preparation scratch data.", context.CancellationToken);
        await workspace.File("input.txt").WriteAllTextAsync("Shared workflow input.", context.CancellationToken);
        context.WriteLine("Shared input prepared.");
    })
    .Cleanup("Release preparation scratch data before the next target.", context =>
    {
        var state = context.GetState(resources);
        if (state.PreparationFile is { } file)
        {
            System.IO.File.Delete(file.FullPath);
            state.PreparationFile = null;
            context.WriteLine("Preparation resources released.");
        }
    });

var verify = command.Target("verify", "Read the shared input and optionally demonstrate failure.")
    .DependsOn(prepare)
    .When("Verification was not disabled with --skip-verification.", context =>
    {
        return !context.GetValue(skipVerification);
    })
    .Run(async context =>
    {
        var state = context.GetState(resources);
        var workspace = state.Workspace
            ?? throw new InvalidOperationException("The prepare target did not supply a workspace.");

        state.VerificationFile = workspace.File("verify.tmp");
        state.VerificationStream = System.IO.File.Create(state.VerificationFile.FullPath);
        await state.VerificationStream.WriteAsync("Verification scratch data."u8.ToArray(), context.CancellationToken);
        var input = await System.IO.File.ReadAllTextAsync(workspace.File("input.txt").FullPath,
            context.CancellationToken);
        context.WriteLine("Verified: " + input);

        if (context.GetValue(fail))
        {
            throw new InvalidOperationException("Requested verification failure.");
        }
    })
    .Cleanup("Release verification scratch data, including after a failed body.", async context =>
    {
        var state = context.GetState(resources);
        if (state.VerificationStream is { } stream)
        {
            await stream.DisposeAsync();
            state.VerificationStream = null;
        }
        if (state.VerificationFile is { } file)
        {
            System.IO.File.Delete(file.FullPath);
            state.VerificationFile = null;
            context.WriteLine("Verification resources released.");
        }

        if (context.GetValue(failCleanup))
        {
            throw new IOException("Requested verification cleanup failure.");
        }
    });

var finish = command.Target("finish", "Use the shared input after target-local resources are released.")
    .DependsOn(verify)
    .Run(async context =>
    {
        var state = context.GetState(resources);
        var workspace = state.Workspace
            ?? throw new InvalidOperationException("The prepare target did not supply a workspace.");
        var input = await System.IO.File.ReadAllTextAsync(workspace.File("input.txt").FullPath,
            context.CancellationToken);
        context.WriteLine("Finished with: " + input);
    });

return await command.RunAsync(args, finish);

sealed class WorkflowResources
{
    public DirectoryPath? Workspace { get; set; }
    public FilePath? PreparationFile { get; set; }
    public FilePath? VerificationFile { get; set; }
    public System.IO.FileStream? VerificationStream { get; set; }
}

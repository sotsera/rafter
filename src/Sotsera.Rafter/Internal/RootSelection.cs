namespace Sotsera.Rafter.Internal;

// Immutable declaration snapshot; prepared values, not the mutable option declaration, select each run's root.
internal sealed record RootSelection(int Calls = 0, string? Path = null, ValueOption<string>? Option = null,
    bool FromOption = false)
{
    public void ValidateDeclaration(OptionSnapshot[] options, List<string> diagnostics)
    {
        if (Calls == 0)
        {
            return;
        }
        if (Calls > 1)
        {
            diagnostics.Add("The command root may only be configured once.");
        }
        if (FromOption)
        {
            if (Option is null || !options.Any(option => option.Declaration == Option.Declaration))
            {
                diagnostics.Add("The root option must be a non-null required/defaulted string belonging to this command.");
            }
        }
        else if (!RootPaths.HasValidText(Path))
        {
            diagnostics.Add("The command root must be nonblank and contain no NUL characters.");
        }
    }

    public bool TryResolve(string invocationDirectory, OptionSnapshot[] options, InputValues values,
        out string rootDirectory, out string? diagnostic)
    {
        rootDirectory = invocationDirectory;
        diagnostic = null;
        if (Calls == 0)
        {
            return true;
        }
        var path = FromOption ? values.GetRequired(Option!) : Path!;
        try
        {
            rootDirectory = System.IO.Path.TrimEndingDirectorySeparator(RootPaths.Resolve(path, invocationDirectory));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            var name = FromOption
                ? values.DiagnosticName(options.Single(option => option.Declaration == Option!.Declaration))
                : "Command root";
            diagnostic = name + " requires a valid, unambiguous filesystem path.";
            return false;
        }
    }
}

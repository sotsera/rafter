using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>A typed key for reference state initialized separately for each invocation. It owns no resource itself.</summary>
/// <typeparam name="T">The invocation's state object type.</typeparam>
public sealed class State<T> where T : class
{
    internal StateDeclaration Declaration { get; }

    internal State(StateDeclaration declaration) => Declaration = declaration;
}

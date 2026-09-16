namespace RazorWings.Compiler.Models;

/// <summary>
/// Represents the reactive dependency graph for a component.
/// Maps how state changes trigger DOM updates.
/// </summary>
public class ReactionGraph
{
    /// <summary>
    /// Maps state variable names to the DOM selectors that render them.
    /// </summary>
    public Dictionary<string, HashSet<string>> StateToSelectors { get; set; } = new();

    /// <summary>
    /// Maps event handler names to the state variables they mutate.
    /// </summary>
    public Dictionary<string, HashSet<string>> HandlerToMutations { get; set; } = new();

    /// <summary>
    /// Maps state variable names to the event handlers that mutate them.
    /// </summary>
    public Dictionary<string, HashSet<string>> StateToHandlers { get; set; } = new();

    /// <summary>
    /// Event handler flow definitions (which handlers call which other handlers).
    /// </summary>
    public Dictionary<string, HashSet<string>> HandlerDependencies { get; set; } = new();

    /// <summary>Maps computed state members to the state members they read.</summary>
    public Dictionary<string, HashSet<string>> ComputedDependencies { get; set; } = new();

    /// <summary>Expressions that require structural DOM reconciliation.</summary>
    public Dictionary<string, string> StructuralDependencies { get; set; } = new();

    /// <summary>
    /// Topologically sorted list of state variables for update order.
    /// </summary>
    public List<string> StateUpdateOrder { get; set; } = new();

    /// <summary>
    /// Indicates if the graph was successfully built.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Build errors or warnings.
    /// </summary>
    public List<string> Messages { get; set; } = new();
}

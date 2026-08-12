namespace RazorWings.Framework;

/// <summary>
/// Defines the contract for fine-grained state reactivity tracking.
/// Components implementing this interface can track which DOM nodes
/// depend on specific state variables.
/// </summary>
public interface IStateReactivity
{
    /// <summary>
    /// Gets the reactive state variables for this component.
    /// </summary>
    IReadOnlyDictionary<string, object?> GetState();

    /// <summary>
    /// Gets the DOM selector paths that depend on a specific state variable.
    /// </summary>
    /// <param name="variableName">The name of the state variable.</param>
    /// <returns>Collection of DOM selector strings that render this variable.</returns>
    IEnumerable<string> GetDependentSelectors(string variableName);

    /// <summary>
    /// Triggers a UI update for the given state variable.
    /// Called by generated JavaScript after a state change.
    /// </summary>
    /// <param name="variableName">The name of the changed state variable.</param>
    void UpdateUI(string variableName);
}

namespace RazorWings.Core;

/// <summary>
/// Defines the contract for server-side rendering of a Razor component.
/// Generated .g.cs files will implement this interface.
/// </summary>
public interface IComponentRenderer
{
    /// <summary>
    /// Renders the component to an HTML string using current state values.
    /// Called during initial server-side rendering.
    /// </summary>
    /// <returns>HTML markup representing the component.</returns>
    string Render();
}

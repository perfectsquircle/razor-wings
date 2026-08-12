namespace RazorWings.Compiler.Models;

/// <summary>
/// Represents the complete analysis of a Razor component.
/// </summary>
public class ComponentModel
{
    /// <summary>
    /// The component name (derived from filename or @code block).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The namespace for generated classes.
    /// </summary>
    public string Namespace { get; set; } = "RazorWings.Components";

    /// <summary>
    /// Full path to the .razor source file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// The full Razor markup HTML.
    /// </summary>
    public string MarkupContent { get; set; } = string.Empty;

    /// <summary>
    /// The full @code block content (C# syntax tree).
    /// </summary>
    public string CodeBlockContent { get; set; } = string.Empty;

    /// <summary>
    /// State variables declared in the component.
    /// </summary>
    public List<StateVariable> StateVariables { get; set; } = new();

    /// <summary>
    /// Event handler methods declared in the component.
    /// </summary>
    public List<EventHandler> EventHandlers { get; set; } = new();

    /// <summary>
    /// Markup bindings (data and event bindings).
    /// </summary>
    public List<MarkupBinding> Bindings { get; set; } = new();

    /// <summary>
    /// Maps state variable names to the DOM selectors that depend on them.
    /// </summary>
    public Dictionary<string, HashSet<string>> StateToSelectorsMap { get; set; } = new();

    /// <summary>
    /// Indicates whether parsing completed successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Parsing or analysis errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

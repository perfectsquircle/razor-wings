namespace RazorWings.Compiler.Models;

/// <summary>
/// Represents a state variable (field or property) in a Razor component.
/// </summary>
public class StateVariable
{
    /// <summary>
    /// The name of the state variable (e.g., "count").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The C# type of the state variable (e.g., "int", "string", "bool").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The initial value as a C# string (e.g., "0", "\"\"", "true").
    /// </summary>
    public string? InitialValue { get; set; }

    /// <summary>
    /// Whether this variable is private, public, protected, etc.
    /// </summary>
    public string AccessModifier { get; set; } = "private";

    /// <summary>
    /// Line number in the source Razor file where this variable is declared.
    /// </summary>
    public int LineNumber { get; set; }
}

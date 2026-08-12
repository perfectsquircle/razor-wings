namespace RazorWings.Compiler.Models;

/// <summary>
/// Represents an event handler method in a Razor component.
/// </summary>
public class EventHandler
{
    /// <summary>
    /// The name of the method (e.g., "Increment").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The return type (e.g., "void", "Task", "int").
    /// </summary>
    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// Parameters of the method.
    /// </summary>
    public List<MethodParameter> Parameters { get; set; } = new();

    /// <summary>
    /// The full method body as a string.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Access modifier (private, public, protected, etc.).
    /// </summary>
    public string AccessModifier { get; set; } = "private";

    /// <summary>
    /// Line number in the source Razor file where this method is declared.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Names of state variables this handler mutates.
    /// </summary>
    public HashSet<string> MutatedVariables { get; set; } = new();
}

/// <summary>
/// Represents a parameter in a method.
/// </summary>
public class MethodParameter
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type.
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

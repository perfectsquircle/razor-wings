namespace RazorWings.Compiler.Models;

/// <summary>
/// Represents a data or event binding in Razor markup.
/// </summary>
public class MarkupBinding
{
    /// <summary>
    /// The type of binding (DataBinding, EventBinding, etc.).
    /// </summary>
    public BindingType Type { get; set; }

    /// <summary>
    /// The expression or variable name being bound (e.g., "count", "Increment").
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// The HTML attribute name (e.g., "onclick", "textContent").
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// CSS selector or path to the DOM element.
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// Line number in the markup section.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Indicates whether this is a callback or direct binding.
    /// </summary>
    public bool IsCallback { get; set; }
}

/// <summary>
/// Types of bindings in Razor markup.
/// </summary>
public enum BindingType
{
    /// <summary>
    /// Data binding (e.g., @count).
    /// </summary>
    DataBinding,

    /// <summary>
    /// Event binding (e.g., @onclick="Increment").
    /// </summary>
    EventBinding,

    /// <summary>
    /// Two-way binding (deferred to Phase 2).
    /// </summary>
    TwoWayBinding
}

namespace RazorWings.Core;

/// <summary>
/// Marks a class as a Razor component that will be processed by the
/// RazorWings compilation pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RazorComponentAttribute : Attribute
{
    /// <summary>
    /// The name of the Razor component. If not specified, the class name is used.
    /// </summary>
    public string? ComponentName { get; set; }
}

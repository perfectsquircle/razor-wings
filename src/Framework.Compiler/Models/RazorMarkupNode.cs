namespace RazorWings.Compiler.Models;

/// <summary>Base type for the structured markup tree produced by the Razor parser.</summary>
public abstract class RazorMarkupNode
{
    public List<RazorMarkupNode> Children { get; } = new();
    public int LineNumber { get; set; }
}

public sealed class RazorTextNode : RazorMarkupNode
{
    public string Content { get; set; } = string.Empty;
}

public class RazorElementNode : RazorMarkupNode
{
    public string TagName { get; set; } = string.Empty;
    public bool IsComponent { get; set; }
    public bool IsSelfClosing { get; set; }
    public List<RazorAttribute> Attributes { get; } = new();
}

public sealed class RazorComponentNode : RazorElementNode
{
    public RazorComponentNode() => IsComponent = true;
}

public sealed class RazorIfNode : RazorMarkupNode
{
    public string Condition { get; set; } = string.Empty;
    public List<RazorMarkupNode> ElseChildren { get; } = new();
}

public sealed class RazorForEachNode : RazorMarkupNode
{
    public string IterationVariable { get; set; } = string.Empty;
    public string CollectionExpression { get; set; } = string.Empty;
}

public sealed class RazorAttribute
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsExpression { get; set; }
    public bool IsEvent => Name.StartsWith("on", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Information about a [Parameter] property declared by a component.</summary>
public sealed class ComponentParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AccessModifier { get; set; } = "public";
    public string? DefaultValue { get; set; }
    public int LineNumber { get; set; }
}

/// <summary>Information about an EventCallback parameter declared by a component.</summary>
public sealed class ComponentEventCallback
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? CallbackArgumentType { get; set; }
    public string AccessModifier { get; set; } = "public";
    public int LineNumber { get; set; }
}

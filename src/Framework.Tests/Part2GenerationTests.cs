using RazorWings.Compiler.Analysis;
using RazorWings.Compiler.CodeGen;
using RazorWings.Compiler.Parsing;
using Xunit;

namespace RazorWings.Tests;

public sealed class Part2GenerationTests
{
    [Fact]
    public void ParserBuildsConditionalAndLoopNodes()
    {
        const string source = """
            @code {
                private bool isVisible = true;
                private List<string> items = new() { "A", "B" };
            }
            @if (isVisible)
            {
                <p>@items.Count</p>
            }
            @else
            {
                <span>Hidden</span>
            }
            <ul>
                @foreach (var item in items)
                {
                    <li>@item</li>
                }
            }
            """;

        var component = new RazorParsingUtility().ParseRazorFile("List.razor", source);

        Assert.True(component.IsValid, string.Join(Environment.NewLine, component.Errors));
        Assert.Contains(component.MarkupNodes, node => node is RazorWings.Compiler.Models.RazorIfNode);
        Assert.Contains(component.MarkupNodes, node => node is RazorWings.Compiler.Models.RazorElementNode);
        var conditional = Assert.IsType<RazorWings.Compiler.Models.RazorIfNode>(
            component.MarkupNodes.First(node => node is RazorWings.Compiler.Models.RazorIfNode));
        Assert.Contains(conditional.ElseChildren,
            node => node is RazorWings.Compiler.Models.RazorElementNode element && element.TagName == "span");
        Assert.Contains(component.MarkupNodes.SelectMany(node => node.Children),
            node => node is RazorWings.Compiler.Models.RazorForEachNode);
    }

    [Fact]
    public void GraphTracksComputedAndStructuralDependencies()
    {
        const string source = """
            @code {
                private string firstName = "Ada";
                private string lastName = "Lovelace";
                private string FullName => $"{firstName} {lastName}";
                private bool visible = true;
            }
            @if (visible) { <p>@FullName</p> }
            """;

        var component = new RazorParsingUtility().ParseRazorFile("Profile.razor", source);
        var graph = new ReactionGraphBuilder().BuildGraph(component);

        Assert.True(graph.IsValid);
        Assert.Contains("FullName", graph.ComputedDependencies.Keys);
        Assert.Contains("firstName", graph.ComputedDependencies["FullName"]);
        Assert.Contains("lastName", graph.ComputedDependencies["FullName"]);
        Assert.NotEmpty(graph.StructuralDependencies);
    }

    [Fact]
    public void GeneratorsEmitStructuralRuntimeAndPropsContract()
    {
        const string source = """
            @code {
                [Parameter] public int Count { get; set; }
                [Parameter] public EventCallback OnIncrement { get; set; }
                private bool visible = true;
                private List<string> items = new() { "A" };
            }
            <button onclick="@OnIncrement">Increment</button>
            @if (visible) { <p>Count: @Count</p> }
            @foreach (var item in items) { <span>@item</span> }
            """;

        var component = new RazorParsingUtility().ParseRazorFile("Child.razor", source);
        var graph = new ReactionGraphBuilder().BuildGraph(component);
        var javascript = new JavaScriptEmitter().GenerateJavaScriptCode(component, graph);
        var ssr = new SsrGenerator().GenerateSsrCode(component, graph);

        Assert.Contains("__updateIf", javascript);
        Assert.Contains("__updateForEach", javascript);
        Assert.Contains("updateProps", javascript);
        Assert.Contains("foreach (var item in items)", ssr);
        Assert.Contains("if (visible)", ssr);
    }
}

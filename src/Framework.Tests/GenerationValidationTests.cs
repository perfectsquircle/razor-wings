using Xunit;
using RazorWings.Compiler.Parsing;
using RazorWings.Compiler.Analysis;
using RazorWings.Compiler.CodeGen;

namespace RazorWings.Tests;

public class GenerationValidationTests
{
    private const string CounterRazorContent = @"
@code {
    private int count = 0;

    private void Increment()
    {
        count++;
    }
}

<div class=""counter"">
    <button onclick=""@Increment"">Increment</button>
    <p>Current count: @count</p>
</div>
";

    [Fact]
    public void GenerateSsr_ProducesValidCSharpCode()
    {
        // Arrange
        var parser = new RazorParsingUtility();
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);
        var generator = new SsrGenerator();

        // Act
        var ssrCode = generator.GenerateSsrCode(component);

        // Assert
        Assert.NotEmpty(ssrCode);
        Assert.Contains("CounterRenderer", ssrCode);
        Assert.Contains("Render", ssrCode);
        Assert.Contains("public static string", ssrCode);
        Assert.Contains("count", ssrCode.ToLower());
    }

    [Fact]
    public void GenerateJavaScript_ProducesValidJsCode()
    {
        // Arrange
        var parser = new RazorParsingUtility();
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);
        var builder = new ReactionGraphBuilder();
        var graph = builder.BuildGraph(component);
        var emitter = new JavaScriptEmitter();

        // Act
        var jsCode = emitter.GenerateJavaScriptCode(component, graph);

        // Assert
        Assert.NotEmpty(jsCode);
        Assert.Contains("export function mountCounter", jsCode);
        Assert.Contains("function update", jsCode);
        Assert.Contains("count", jsCode);
        Assert.True(jsCode.Length < 2048, "JS code should be < 2KB"); // Should be < 2KB
    }

    [Fact]
    public void EndToEnd_PipelineGeneratesValidOutput()
    {
        // Arrange
        var parser = new RazorParsingUtility();
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);
        var builder = new ReactionGraphBuilder();
        var graph = builder.BuildGraph(component);
        var ssrGen = new SsrGenerator();
        var jsEmitter = new JavaScriptEmitter();

        // Act
        var ssrCode = ssrGen.GenerateSsrCode(component);
        var jsCode = jsEmitter.GenerateJavaScriptCode(component, graph);

        // Assert - Component metadata
        Assert.True(component.IsValid);
        Assert.Equal("Counter", component.Name);
        
        // Assert - State variables
        Assert.Single(component.StateVariables);
        Assert.Equal("count", component.StateVariables[0].Name);
        Assert.Equal("int", component.StateVariables[0].Type);
        
        // Assert - Event handlers
        Assert.Single(component.EventHandlers);
        Assert.Equal("Increment", component.EventHandlers[0].Name);
        Assert.Contains("count", component.EventHandlers[0].MutatedVariables);
        
        // Assert - Reaction graph
        Assert.True(graph.IsValid);
        Assert.NotEmpty(graph.HandlerToMutations);
        Assert.NotEmpty(graph.StateToHandlers);
        
        // Assert - SSR generation
        Assert.NotEmpty(ssrCode);
        Assert.Contains("class CounterRenderer", ssrCode);
        
        // Assert - JS generation
        Assert.NotEmpty(jsCode);
        Assert.Contains("export", jsCode);
        Assert.True(jsCode.Length < 2048, "JS code should be < 2KB");
    }
}

using Xunit;
using RazorWings.Compiler.Parsing;
using RazorWings.Compiler.Analysis;

namespace RazorWings.Tests;

public class ParsingTests
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
    public void ParseRazorFile_ParsesComponentSuccessfully()
    {
        // Arrange
        var parser = new RazorParsingUtility();

        // Act
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);

        // Assert
        Assert.True(component.IsValid);
        Assert.Equal("Counter", component.Name);
    }

    [Fact]
    public void ParseRazorFile_ExtractsStateVariables()
    {
        // Arrange
        var parser = new RazorParsingUtility();

        // Act
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);

        // Assert
        Assert.Single(component.StateVariables);
        Assert.Equal("count", component.StateVariables[0].Name);
        Assert.Equal("int", component.StateVariables[0].Type);
        Assert.Equal("0", component.StateVariables[0].InitialValue);
    }

    [Fact]
    public void ParseRazorFile_ExtractsEventHandlers()
    {
        // Arrange
        var parser = new RazorParsingUtility();

        // Act
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);

        // Assert
        Assert.Single(component.EventHandlers);
        Assert.Equal("Increment", component.EventHandlers[0].Name);
        Assert.Equal("void", component.EventHandlers[0].ReturnType);
        Assert.Contains("count", component.EventHandlers[0].MutatedVariables);
    }

    [Fact]
    public void ParseRazorFile_ExtractsMarkupBindings()
    {
        // Arrange
        var parser = new RazorParsingUtility();

        // Act
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);

        // Assert
        Assert.NotEmpty(component.Bindings);
        var dataBindings = component.Bindings.Where(b => b.Type == RazorWings.Compiler.Models.BindingType.DataBinding);
        Assert.NotEmpty(dataBindings);
    }

    [Fact]
    public void BuildGraph_BuildsReactionGraphSuccessfully()
    {
        // Arrange
        var parser = new RazorParsingUtility();
        var component = parser.ParseRazorFile("Counter.razor", CounterRazorContent);
        var builder = new ReactionGraphBuilder();

        // Act
        var graph = builder.BuildGraph(component);

        // Assert
        Assert.True(graph.IsValid);
        Assert.NotEmpty(graph.HandlerToMutations);
        Assert.NotEmpty(graph.StateToHandlers);
    }
}

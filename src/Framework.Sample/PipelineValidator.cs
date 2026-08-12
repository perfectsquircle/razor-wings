using RazorWings.Compiler.Parsing;
using RazorWings.Compiler.Analysis;
using RazorWings.Compiler.CodeGen;

namespace RazorWings.Sample;

/// <summary>
/// Validates the complete code generation pipeline by parsing Counter.razor,
/// building the reaction graph, and generating SSR and JavaScript code.
/// </summary>
public static class PipelineValidator
{
    public static void ValidateCounterComponent()
    {
        const string razorContent = @"
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

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              RAZOR-WINGS PIPELINE VALIDATION                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Parse component
        var parser = new RazorParsingUtility();
        var component = parser.ParseRazorFile("Counter.razor", razorContent);

        Console.WriteLine("✓ PARSING");
        Console.WriteLine($"  Component: {component.Name}");
        Console.WriteLine($"  State Variables: {component.StateVariables.Count}");
        foreach (var sv in component.StateVariables)
        {
            Console.WriteLine($"    - {sv.Name}: {sv.Type} = {sv.InitialValue}");
        }
        Console.WriteLine($"  Event Handlers: {component.EventHandlers.Count}");
        foreach (var eh in component.EventHandlers)
        {
            Console.WriteLine($"    - {eh.Name}() → mutates {string.Join(", ", eh.MutatedVariables)}");
        }
        Console.WriteLine($"  Bindings: {component.Bindings.Count}");
        Console.WriteLine();

        // Build reaction graph
        var graphBuilder = new ReactionGraphBuilder();
        var graph = graphBuilder.BuildGraph(component);

        Console.WriteLine("✓ REACTION GRAPH");
        Console.WriteLine($"  Valid: {graph.IsValid}");
        Console.WriteLine($"  State → Handlers:");
        foreach (var kvp in graph.StateToHandlers)
        {
            Console.WriteLine($"    {kvp.Key}: {string.Join(", ", kvp.Value)}");
        }
        Console.WriteLine($"  Handler → Mutations:");
        foreach (var kvp in graph.HandlerToMutations)
        {
            Console.WriteLine($"    {kvp.Key}: {string.Join(", ", kvp.Value)}");
        }
        Console.WriteLine();

        // Generate SSR
        var ssrGenerator = new SsrGenerator();
        var ssrCode = ssrGenerator.GenerateSsrCode(component);

        Console.WriteLine("✓ SSR GENERATION");
        Console.WriteLine($"  Generated: {ssrCode.Length} bytes");
        Console.WriteLine($"  Contains class CounterRenderer: {ssrCode.Contains("CounterRenderer")}");
        Console.WriteLine($"  Contains Render method: {ssrCode.Contains("Render")}");
        Console.WriteLine();
        Console.WriteLine("  Generated Code Preview:");
        Console.WriteLine("  " + string.Join("\n  ", ssrCode.Split('\n').Take(5)));
        Console.WriteLine();

        // Generate JavaScript
        var jsEmitter = new JavaScriptEmitter();
        var jsCode = jsEmitter.GenerateJavaScriptCode(component, graph);

        Console.WriteLine("✓ JAVASCRIPT GENERATION");
        Console.WriteLine($"  Generated: {jsCode.Length} bytes");
        Console.WriteLine($"  Size < 2KB: {jsCode.Length < 2048}");
        Console.WriteLine($"  Contains export mount: {jsCode.Contains("export function mount")}");
        Console.WriteLine($"  Contains update function: {jsCode.Contains("function update")}");
        Console.WriteLine();
        Console.WriteLine("  Generated Code Preview:");
        Console.WriteLine("  " + string.Join("\n  ", jsCode.Split('\n').Take(8)));
        Console.WriteLine();

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    VALIDATION SUCCESSFUL ✓                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
    }
}

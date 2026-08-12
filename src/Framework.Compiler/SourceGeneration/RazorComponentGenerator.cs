namespace RazorWings.Compiler.SourceGeneration;

using Microsoft.CodeAnalysis;
using RazorWings.Compiler.Analysis;
using RazorWings.Compiler.CodeGen;
using RazorWings.Compiler.Models;
using RazorWings.Compiler.Parsing;

/// <summary>
/// Roslyn incremental source generator for Razor components.
/// Processes .razor files and generates .g.cs (SSR) and .g.js (client) outputs.
///
/// Note: This is a placeholder for Phase 1. In production, this would use:
/// - context.AdditionalFilesProvider to discover .razor files
/// - IncrementalGenerator pipeline for caching and perf
/// - Custom MSBuild targets to handle .js output
/// </summary>
[Generator]
public class RazorComponentGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental generator pipeline.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 1 PoC: Generate a helper class that shows the pipeline works
        context.RegisterSourceOutput(
            context.CompilationProvider,
            (ctx, compilation) => GenerateHelperCode(ctx, compilation)
        );
    }

    /// <summary>
    /// Generates helper code to demonstrate the pipeline.
    /// </summary>
    private void GenerateHelperCode(SourceProductionContext context, Compilation compilation)
    {
        var helperCode = """
namespace RazorWings.Compiler;

/// <summary>
/// Helper class for the Razor-Wings component generation pipeline.
/// </summary>
public static class ComponentGenerationHelper
{
    /// <summary>
    /// Example of how to use the generation pipeline for a component.
    /// </summary>
    public static string GenerateExampleComponent()
    {
        // In production, this would:
        // 1. Discover .razor files via AdditionalFilesProvider
        // 2. Parse using RazorParsingUtility
        // 3. Analyze using ReactionGraphBuilder
        // 4. Generate SSR code via SsrGenerator
        // 5. Generate JS code via JavaScriptEmitter
        
        return "Component generation pipeline ready for production use";
    }
}
""";

        context.AddSource("ComponentGenerationHelper.g.cs", helperCode);
    }
}



namespace RazorWings.Compiler.SourceGeneration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RazorWings.Compiler.Analysis;
using RazorWings.Compiler.CodeGen;
using RazorWings.Compiler.Parsing;

/// <summary>
/// Generates server-side C# and client-side JavaScript for .razor components.
/// </summary>
[Generator]
public sealed class RazorComponentGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor ParseFailure = new(
        "RW001",
        "Razor component generation failed",
        "Could not generate component '{0}': {1}",
        "RazorWings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor JavaScriptWriteFailure = new(
        "RW002",
        "Generated JavaScript could not be written",
        "Could not write generated JavaScript for component '{0}' to '{1}': {2}",
        "RazorWings",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var razorFiles = context.AdditionalTextsProvider
            .Where(static file => Path.GetExtension(file.Path)
                .Equals(".razor", StringComparison.OrdinalIgnoreCase));

        var inputs = razorFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) =>
            {
                var text = pair.Left.GetText(cancellationToken)?.ToString() ?? string.Empty;
                pair.Right.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDirectory);
                return new GenerationInput(pair.Left.Path, text, projectDirectory);
            });

        context.RegisterSourceOutput(inputs, static (productionContext, input) =>
            GenerateComponent(productionContext, input));
    }

    private static void GenerateComponent(SourceProductionContext context, GenerationInput input)
    {
        var componentName = Path.GetFileNameWithoutExtension(input.Path);

        try
        {
            var parser = new RazorParsingUtility();
            var component = parser.ParseRazorFile(input.Path, input.Content);

            if (!component.IsValid)
            {
                ReportFailure(context, input.Path, componentName,
                    string.Join("; ", component.Errors));
                return;
            }

            var graph = new ReactionGraphBuilder().BuildGraph(component);
            if (!graph.IsValid)
            {
                ReportFailure(context, input.Path, componentName,
                    "The component reaction graph is invalid.");
                return;
            }

            var ssrCode = new SsrGenerator().GenerateSsrCode(component, graph);
            context.AddSource($"{componentName}.g.cs", ssrCode);

            var javascript = new JavaScriptEmitter().GenerateJavaScriptCode(component, graph);
            WriteJavaScript(context, componentName, input.ProjectDirectory, javascript);
        }
        catch (Exception exception)
        {
            ReportFailure(context, input.Path, componentName, exception.Message);
        }
    }

    private static void WriteJavaScript(
        SourceProductionContext context,
        string componentName,
        string? projectDirectory,
        string javascript)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                JavaScriptWriteFailure,
                Location.None,
                componentName,
                "wwwroot/generated",
                "The build property 'ProjectDir' was not supplied."));
            return;
        }

        var outputDirectory = Path.Combine(projectDirectory, "wwwroot", "generated");
        var outputPath = Path.Combine(outputDirectory, $"{componentName}.g.js");

        try
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputPath, javascript);
        }
        catch (Exception exception)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                JavaScriptWriteFailure,
                Location.None,
                componentName,
                outputPath,
                exception.Message));
        }
    }

    private static void ReportFailure(
        SourceProductionContext context,
        string path,
        string componentName,
        string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            ParseFailure,
            Location.None,
            componentName,
            message));
    }

    private sealed record GenerationInput(
        string Path,
        string Content,
        string? ProjectDirectory);
}

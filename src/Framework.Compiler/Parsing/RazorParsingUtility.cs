namespace RazorWings.Compiler.Parsing;

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RazorWings.Compiler.Models;

/// <summary>
/// Utility for parsing .razor files and extracting component metadata.
/// </summary>
public class RazorParsingUtility
{
    /// <summary>
    /// Parses a .razor file and extracts component model.
    /// </summary>
    /// <param name="razorFilePath">Path to the .razor file.</param>
    /// <param name="razorContent">Content of the .razor file.</param>
    /// <returns>Parsed ComponentModel.</returns>
    public ComponentModel ParseRazorFile(string razorFilePath, string razorContent)
    {
        var componentModel = new ComponentModel
        {
            SourcePath = razorFilePath,
            Name = Path.GetFileNameWithoutExtension(razorFilePath)
        };

        try
        {
            // Split into @code block and markup
            var (codeBlock, markup) = SplitRazorContent(razorContent);

            componentModel.CodeBlockContent = codeBlock;
            componentModel.MarkupContent = markup;

            // Extract state variables from @code block
            if (!string.IsNullOrEmpty(codeBlock))
            {
                ExtractStateVariables(codeBlock, componentModel);
                ExtractEventHandlers(codeBlock, componentModel);
            }

            // Extract bindings from markup
            if (!string.IsNullOrEmpty(markup))
            {
                ExtractMarkupBindings(markup, componentModel);
            }

            // Build state-to-selectors mapping
            BuildStateToSelectorsMap(componentModel);

            componentModel.IsValid = true;
        }
        catch (Exception ex)
        {
            componentModel.Errors.Add($"Parse error: {ex.Message}");
            componentModel.IsValid = false;
        }

        return componentModel;
    }

    /// <summary>
    /// Splits .razor content into @code block and markup sections.
    /// </summary>
    private (string CodeBlock, string Markup) SplitRazorContent(string razorContent)
    {
        const string codeBlockPattern = @"@code\s*\{((?:[^{}]|(?<c>\{)|(?<-c>\}))*)\}";
        var match = Regex.Match(razorContent, codeBlockPattern, RegexOptions.Singleline);

        if (match.Success)
        {
            var codeBlock = match.Groups[1].Value;
            var markup = Regex.Replace(razorContent, codeBlockPattern, "", RegexOptions.Singleline).Trim();
            return (codeBlock, markup);
        }

        return ("", razorContent);
    }

    /// <summary>
    /// Extracts state variables (fields/properties) from the @code block.
    /// </summary>
    private void ExtractStateVariables(string codeBlock, ComponentModel model)
    {
        try
        {
            // Wrap code block in a temporary class for Roslyn parsing
            var wrappedCode = $"class _Temp {{ {codeBlock} }}";
            var tree = CSharpSyntaxTree.ParseText(wrappedCode);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null) return;

            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

            int lineNum = 1;

            // Process field declarations
            foreach (var fieldDecl in fieldDeclarations)
            {
                var typeStr = fieldDecl.Declaration.Type.ToString();
                var accessMod = GetAccessModifier(fieldDecl.Modifiers);

                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    var stateVar = new StateVariable
                    {
                        Name = variable.Identifier.Text,
                        Type = typeStr,
                        InitialValue = variable.Initializer?.Value.ToString(),
                        AccessModifier = accessMod,
                        LineNumber = lineNum
                    };
                    model.StateVariables.Add(stateVar);
                }
                lineNum++;
            }

            // Process property declarations
            foreach (var propDecl in propertyDeclarations)
            {
                var typeStr = propDecl.Type.ToString();
                var accessMod = GetAccessModifier(propDecl.Modifiers);

                var stateVar = new StateVariable
                {
                    Name = propDecl.Identifier.Text,
                    Type = typeStr,
                    InitialValue = propDecl.Initializer?.Value.ToString(),
                    AccessModifier = accessMod,
                    LineNumber = lineNum
                };
                model.StateVariables.Add(stateVar);
                lineNum++;
            }
        }
        catch (Exception ex)
        {
            model.Errors.Add($"Error extracting state variables: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts event handler methods from the @code block.
    /// </summary>
    private void ExtractEventHandlers(string codeBlock, ComponentModel model)
    {
        try
        {
            // Wrap code block in a temporary class for Roslyn parsing
            var wrappedCode = $"class _Temp {{ {codeBlock} }}";
            var tree = CSharpSyntaxTree.ParseText(wrappedCode);
            var root = tree.GetRoot() as CompilationUnitSyntax;

            if (root == null) return;

            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var methodDecl in methodDeclarations)
            {
                var handler = new EventHandler
                {
                    Name = methodDecl.Identifier.Text,
                    ReturnType = methodDecl.ReturnType.ToString(),
                    AccessModifier = GetAccessModifier(methodDecl.Modifiers),
                    Body = methodDecl.Body?.ToString() ?? "",
                    LineNumber = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                };

                // Extract parameters
                foreach (var param in methodDecl.ParameterList.Parameters)
                {
                    handler.Parameters.Add(new MethodParameter
                    {
                        Name = param.Identifier.Text,
                        Type = param.Type?.ToString() ?? "object"
                    });
                }

                // Analyze body for mutated state variables
                AnalyzeMethodBodyForMutations(methodDecl.Body, handler, model);

                model.EventHandlers.Add(handler);
            }
        }
        catch (Exception ex)
        {
            model.Errors.Add($"Error extracting event handlers: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes method body to identify mutated state variables.
    /// </summary>
    private void AnalyzeMethodBodyForMutations(BlockSyntax? body, EventHandler handler, ComponentModel model)
    {
        if (body == null) return;

        var postfixUnaryOps = body.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>();
        var prefixUnaryOps = body.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>();
        var assignments = body.DescendantNodes().OfType<AssignmentExpressionSyntax>();

        // Handle count++, count--
        foreach (var postfixOp in postfixUnaryOps)
        {
            if (postfixOp.Operand is IdentifierNameSyntax id)
            {
                var varName = id.Identifier.Text;
                if (model.StateVariables.Any(s => s.Name == varName) && !handler.MutatedVariables.Contains(varName))
                {
                    handler.MutatedVariables.Add(varName);
                }
            }
        }

        foreach (var prefixOp in prefixUnaryOps)
        {
            if (prefixOp.Operand is IdentifierNameSyntax id)
            {
                var varName = id.Identifier.Text;
                if (model.StateVariables.Any(s => s.Name == varName) && !handler.MutatedVariables.Contains(varName))
                {
                    handler.MutatedVariables.Add(varName);
                }
            }
        }

        // Handle count = value, count += 1, etc.
        foreach (var assignment in assignments)
        {
            if (assignment.Left is IdentifierNameSyntax id)
            {
                var varName = id.Identifier.Text;
                if (model.StateVariables.Any(s => s.Name == varName) && !handler.MutatedVariables.Contains(varName))
                {
                    handler.MutatedVariables.Add(varName);
                }
            }
        }
    }

    /// <summary>
    /// Extracts markup bindings (data and event bindings) from the HTML markup.
    /// </summary>
    private void ExtractMarkupBindings(string markup, ComponentModel model)
    {
        // Extract @variable data bindings (e.g., @count)
        var dataBindingPattern = @"@(\w+)";
        var dataBindings = Regex.Matches(markup, dataBindingPattern);

        foreach (Match match in dataBindings)
        {
            var varName = match.Groups[1].Value;
            if (model.StateVariables.Any(s => s.Name == varName))
            {
                var binding = new MarkupBinding
                {
                    Type = BindingType.DataBinding,
                    Expression = varName,
                    AttributeName = "textContent",
                    LineNumber = GetLineNumber(markup, match.Index)
                };
                model.Bindings.Add(binding);
            }
        }

        // Extract @onclick="Increment" event bindings
        var eventPattern = @"@(onclick|onchange|onsubmit|onkeyup)\s*=\s*[""']([^""']+)[""']";
        var eventBindings = Regex.Matches(markup, eventPattern);

        foreach (Match match in eventBindings)
        {
            var eventName = match.Groups[1].Value;
            var handlerName = match.Groups[2].Value.Trim();

            var binding = new MarkupBinding
            {
                Type = BindingType.EventBinding,
                Expression = handlerName,
                AttributeName = eventName,
                IsCallback = true,
                LineNumber = GetLineNumber(markup, match.Index)
            };
            model.Bindings.Add(binding);
        }
    }

    /// <summary>
    /// Builds a mapping from state variables to the DOM selectors that render them.
    /// </summary>
    private void BuildStateToSelectorsMap(ComponentModel model)
    {
        foreach (var variable in model.StateVariables)
        {
            var selectors = new HashSet<string>();

            // Find all bindings that use this variable
            foreach (var binding in model.Bindings)
            {
                if (binding.Type == BindingType.DataBinding && binding.Expression == variable.Name)
                {
                    // Infer selector from markup context
                    var selector = $"[data-bind-{variable.Name}]";
                    selectors.Add(selector);
                }
            }

            if (selectors.Count > 0)
            {
                model.StateToSelectorsMap[variable.Name] = selectors;
            }
        }
    }

    /// <summary>
    /// Gets the access modifier string (private, public, protected, internal).
    /// </summary>
    private string GetAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            return "public";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return "protected";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
            return "internal";

        return "private";
    }

    /// <summary>
    /// Calculates line number from string index.
    /// </summary>
    private int GetLineNumber(string content, int index)
    {
        return content.Substring(0, index).Count(c => c == '\n') + 1;
    }
}

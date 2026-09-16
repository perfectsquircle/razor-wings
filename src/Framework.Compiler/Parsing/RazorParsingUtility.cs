namespace RazorWings.Compiler.Parsing;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RazorWings.Compiler.Models;

/// <summary>Parses the supported Razor subset and extracts component metadata.</summary>
public class RazorParsingUtility
{
    public ComponentModel ParseRazorFile(string razorFilePath, string razorContent)
    {
        var model = new ComponentModel {
            SourcePath = razorFilePath,
            Name = Path.GetFileNameWithoutExtension(razorFilePath)
        };

        try
        {
            var (code, markup) = SplitRazorContent(razorContent);
            model.CodeBlockContent = code;
            model.MarkupContent = markup;

            if (!string.IsNullOrWhiteSpace(code))
            {
                var root = ParseCode(code);
                ExtractStateVariables(root, model);
                ExtractEventHandlers(root, model);
                ExtractParameters(root, model);
            }

            if (!string.IsNullOrWhiteSpace(markup))
            {
                model.MarkupNodes = new MarkupTreeParser(markup).Parse();
                ExtractMarkupBindings(markup, model);
            }

            BuildStateToSelectorsMap(model);
            model.IsValid = true;
        }
        catch (Exception ex)
        {
            model.Errors.Add($"Parse error: {ex.Message}");
            model.IsValid = false;
        }
        return model;
    }

    private static (string CodeBlock, string Markup) SplitRazorContent(string content)
    {
        var start = content.IndexOf("@code", StringComparison.Ordinal);
        if (start < 0) return ("", content);
        var open = content.IndexOf('{', start);
        if (open < 0) return ("", content);
        var close = FindMatchingBrace(content, open);
        if (close < 0) return ("", content);
        return (content[(open + 1)..close], (content[..start] + content[(close + 1)..]).Trim());
    }

    private static int FindMatchingBrace(string text, int open)
    {
        var depth = 0;
        char quote = '\0';
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static CompilationUnitSyntax ParseCode(string code) =>
        CSharpSyntaxTree.ParseText($"class _Temp {{ {code} }}").GetCompilationUnitRoot();

    private static void ExtractStateVariables(CompilationUnitSyntax root, ComponentModel model)
    {
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            foreach (var variable in field.Declaration.Variables)
                model.StateVariables.Add(new StateVariable {
                    Name = variable.Identifier.Text, Type = field.Declaration.Type.ToString(),
                    InitialValue = variable.Initializer?.Value.ToString(),
                    AccessModifier = GetAccessModifier(field.Modifiers),
                    LineNumber = Line(field)
                });

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            model.StateVariables.Add(new StateVariable {
                Name = property.Identifier.Text, Type = property.Type.ToString(),
                InitialValue = property.Initializer?.Value.ToString(),
                AccessModifier = GetAccessModifier(property.Modifiers),
                IsComputed = property.ExpressionBody is not null ||
                    (property.AccessorList?.Accessors.Any(a => a.Kind() == SyntaxKind.GetAccessorDeclaration &&
                        a.Body is not null) ?? false),
                ComputedExpression = property.ExpressionBody?.Expression.ToString() ??
                    property.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration)?
                        .Body?.ToString(),
                LineNumber = Line(property)
            });
    }

    private static void ExtractParameters(CompilationUnitSyntax root, ComponentModel model)
    {
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (!property.AttributeLists.SelectMany(x => x.Attributes)
                .Any(a => a.Name.ToString() is "Parameter" or "ParameterAttribute"))
                continue;

            var type = property.Type.ToString();
            var line = Line(property);
            if (type.StartsWith("EventCallback", StringComparison.Ordinal))
            {
                model.EventCallbacks.Add(new ComponentEventCallback {
                    Name = property.Identifier.Text, Type = type,
                    CallbackArgumentType = ExtractGenericArgument(type),
                    AccessModifier = GetAccessModifier(property.Modifiers), LineNumber = line
                });
            }
            else
            {
                model.Parameters.Add(new ComponentParameter {
                    Name = property.Identifier.Text, Type = type,
                    DefaultValue = property.Initializer?.Value.ToString(),
                    AccessModifier = GetAccessModifier(property.Modifiers), LineNumber = line
                });
            }
        }
    }

    private static string? ExtractGenericArgument(string type)
    {
        var start = type.IndexOf('<');
        return start >= 0 && type.EndsWith('>') ? type[(start + 1)..^1].Trim() : null;
    }

    private static void ExtractEventHandlers(CompilationUnitSyntax root, ComponentModel model)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var handler = new EventHandler {
                Name = method.Identifier.Text, ReturnType = method.ReturnType.ToString(),
                AccessModifier = GetAccessModifier(method.Modifiers),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "",
                LineNumber = Line(method)
            };
            foreach (var parameter in method.ParameterList.Parameters)
                handler.Parameters.Add(new MethodParameter {
                    Name = parameter.Identifier.Text, Type = parameter.Type?.ToString() ?? "object"
                });
            AnalyzeMethodBodyForMutations(method.Body, handler, model);
            model.EventHandlers.Add(handler);
        }
    }

    private static void AnalyzeMethodBodyForMutations(BlockSyntax? body, EventHandler handler, ComponentModel model)
    {
        if (body == null) return;
        foreach (var expression in body.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var name = expression switch {
                AssignmentExpressionSyntax a when a.Left is IdentifierNameSyntax id => id.Identifier.Text,
                PostfixUnaryExpressionSyntax p when p.Operand is IdentifierNameSyntax id => id.Identifier.Text,
                PrefixUnaryExpressionSyntax p when p.Operand is IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            };
            if (name != null && model.StateVariables.Any(s => s.Name == name))
                handler.MutatedVariables.Add(name);
        }
    }

    private static void ExtractMarkupBindings(string markup, ComponentModel model)
    {
        foreach (Match match in Regex.Matches(markup, @"@(?<expr>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*|\([^)]*\))*)"))
        {
            var expression = match.Groups["expr"].Value;
            if (expression is "if" or "else" or "foreach" or "code" ||
                !model.StateVariables.Any(s => expression == s.Name || expression.StartsWith(s.Name + ".", StringComparison.Ordinal)))
                continue;
            model.Bindings.Add(new MarkupBinding {
                Type = BindingType.DataBinding, Expression = expression, AttributeName = "textContent",
                LineNumber = GetLineNumber(markup, match.Index)
            });
        }

        var eventPattern = @"(?<name>@?on[a-zA-Z]+)\s*=\s*[""'](?<value>[^""']*)[""']";
        foreach (Match match in Regex.Matches(markup, eventPattern))
        {
            var name = match.Groups["name"].Value.TrimStart('@');
            var value = match.Groups["value"].Value.Trim().TrimStart('@');
            model.Bindings.Add(new MarkupBinding {
                Type = BindingType.EventBinding, Expression = value, AttributeName = name,
                IsCallback = true, LineNumber = GetLineNumber(markup, match.Index)
            });
        }
    }

    private static void BuildStateToSelectorsMap(ComponentModel model)
    {
        foreach (var variable in model.StateVariables)
        {
            var selectors = model.Bindings.Where(b => b.Type == BindingType.DataBinding &&
                    (b.Expression == variable.Name || b.Expression.StartsWith(variable.Name + ".", StringComparison.Ordinal)))
                .Select(b => InferElementSelector(model.MarkupContent, variable.Name))
                .ToHashSet();
            if (selectors.Count > 0) model.StateToSelectorsMap[variable.Name] = selectors;
        }
    }

    private static string InferElementSelector(string markup, string variable)
    {
        var match = Regex.Match(markup, $@"<(?<tag>[A-Za-z][\w-]*)\b[^>]*>[^<]*@{Regex.Escape(variable)}\b",
            RegexOptions.Singleline);
        return match.Success ? match.Groups["tag"].Value : $"[data-bind-{variable}]";
    }

    private static string GetAccessModifier(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) ? "public" :
        modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)) ? "protected" :
        modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)) ? "internal" : "private";

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    private static int GetLineNumber(string content, int index) => content[..index].Count(c => c == '\n') + 1;

    private sealed class MarkupTreeParser
    {
        private readonly string _text;
        private int _position;
        public MarkupTreeParser(string text) => _text = text;
        public List<RazorMarkupNode> Parse() => ParseNodes(null, false);

        private List<RazorMarkupNode> ParseNodes(string? closingTag, bool stopAtBrace)
        {
            var nodes = new List<RazorMarkupNode>();
            while (_position < _text.Length)
            {
                if (stopAtBrace && _position < _text.Length && _text[_position] == '}') { _position++; break; }
                if (closingTag != null && StartsWith("</" + closingTag)) { ConsumeTag(); break; }
                if (_position >= _text.Length) break;
                if (_text[_position] == '<') { nodes.Add(ParseElement()); continue; }
                if (_text[_position] == '@' && TryParseDirective(out var directive)) { nodes.Add(directive); continue; }
                nodes.Add(ParseText());
            }
            return nodes;
        }

        private RazorMarkupNode ParseElement()
        {
            var start = _position;
            var end = FindTagEnd(_position);
            if (end < 0) return ParseText();
            var raw = _text[(_position + 1)..end].Trim();
            _position = end + 1;
            if (raw.StartsWith("/")) return new RazorTextNode { Content = "<" + raw + ">" };
            var selfClosing = raw.EndsWith("/");
            if (selfClosing) raw = raw[..^1].TrimEnd();
            var match = Regex.Match(raw, @"^(?<tag>[A-Za-z][\w:.-]*)");
            if (!match.Success) return new RazorTextNode { Content = _text[start.._position] };
            var tag = match.Groups["tag"].Value;
            var node = char.IsUpper(tag[0]) ? new RazorComponentNode() : new RazorElementNode();
            node.TagName = tag; node.IsSelfClosing = selfClosing; node.LineNumber = LineAt(start);
            ParseAttributes(raw[match.Length..], node.Attributes);
            if (!selfClosing && !IsVoidElement(tag))
                node.Children.AddRange(ParseNodes(tag, false));
            return node;
        }

        private bool TryParseDirective(out RazorMarkupNode node)
        {
            node = null!;
            if (StartsWith("@if"))
            {
                var (header, body) = ParseBlock("@if");
                var result = new RazorIfNode { Condition = header, LineNumber = LineAt(_position) };
                result.Children.AddRange(new MarkupTreeParser(body).Parse());
                var save = _position; SkipWhitespaceOnly();
                if (StartsWith("@else"))
                {
                    var (_, elseBody) = ParseBlock("@else");
                    result.ElseChildren.AddRange(new MarkupTreeParser(elseBody).Parse());
                }
                else _position = save;
                node = result; return true;
            }
            if (StartsWith("@foreach"))
            {
                var (header, body) = ParseBlock("@foreach");
                var parts = Regex.Match(header, @"^(?:var\s+)?(?<item>\w+)\s+in\s+(?<collection>.+)$");
                var result = new RazorForEachNode { LineNumber = LineAt(_position) };
                if (parts.Success) { result.IterationVariable = parts.Groups["item"].Value; result.CollectionExpression = parts.Groups["collection"].Value.Trim(); }
                result.Children.AddRange(new MarkupTreeParser(body).Parse());
                node = result; return true;
            }
            return false;
        }

        private (string Header, string Body) ParseBlock(string keyword)
        {
            _position += keyword.Length;
            var open = _text.IndexOf('{', _position);
            if (open < 0) return (_text[_position..].Trim(), "");
            var header = _text[_position..open].Trim().Trim('(', ')');
            var close = FindMatchingBrace(_text, open);
            if (close < 0) close = _text.Length - 1;
            var body = _text[(open + 1)..close];
            _position = Math.Min(close + 1, _text.Length);
            return (header, body);
        }

        private RazorTextNode ParseText()
        {
            var start = _position;
            if (_position < _text.Length && _text[_position] == '@')
            {
                _position++;
                while (_position < _text.Length &&
                       (char.IsLetterOrDigit(_text[_position]) || _text[_position] is '_' or '.' or '(' or ')' or ',' or ' ' or '='))
                {
                    if (_text[_position] == '(')
                    {
                        var depth = 1;
                        _position++;
                        while (_position < _text.Length && depth > 0)
                        {
                            if (_text[_position] == '(') depth++;
                            else if (_text[_position] == ')') depth--;
                            _position++;
                        }
                        continue;
                    }
                    _position++;
                }
                return new RazorTextNode { Content = _text[start.._position], LineNumber = LineAt(start) };
            }
            while (_position < _text.Length && _text[_position] != '<' && _text[_position] != '@') _position++;
            return new RazorTextNode { Content = _text[start.._position], LineNumber = LineAt(start) };
        }

        private void ParseAttributes(string text, List<RazorAttribute> attributes)
        {
            foreach (Match match in Regex.Matches(text, @"(?<name>[-:@\w.]+)(?:\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s]+)))?"))
            {
                var value = match.Groups["double"].Success ? match.Groups["double"].Value :
                    match.Groups["single"].Success ? match.Groups["single"].Value :
                    match.Groups["bare"].Success ? match.Groups["bare"].Value : null;
                value = value?.TrimStart('@');
                attributes.Add(new RazorAttribute {
                    Name = match.Groups["name"].Value, Value = value,
                    IsExpression = value != null && (match.Groups["double"].Value.StartsWith("@") ||
                        match.Groups["single"].Value.StartsWith("@") || match.Groups["bare"].Value.StartsWith("@"))
                });
            }
        }

        private int FindTagEnd(int start)
        {
            char quote = '\0';
            for (var i = start; i < _text.Length; i++)
            {
                if (quote != '\0') { if (_text[i] == quote) quote = '\0'; continue; }
                if (_text[i] is '"' or '\'') quote = _text[i];
                else if (_text[i] == '>') return i;
            }
            return -1;
        }
        private void ConsumeTag() { var end = FindTagEnd(_position); _position = end < 0 ? _text.Length : end + 1; }
        private bool StartsWith(string value) => _text.AsSpan(_position).StartsWith(value, StringComparison.Ordinal);
        private void SkipWhitespaceOnly() { while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) _position++; }
        private int LineAt(int index) => _text[..Math.Min(index, _text.Length)].Count(c => c == '\n') + 1;
        private static bool IsVoidElement(string tag) => tag is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
    }
}

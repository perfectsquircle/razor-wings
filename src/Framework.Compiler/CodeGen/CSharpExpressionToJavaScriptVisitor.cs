namespace RazorWings.Compiler.CodeGen;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Translates the expression subset used by generated client code from C# to JavaScript.
/// </summary>
public sealed class CSharpExpressionToJavaScriptVisitor : CSharpSyntaxVisitor<string>
{
    public static string Translate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var syntax = SyntaxFactory.ParseExpression(expression);
        if (syntax.ContainsDiagnostics)
        {
            throw new ExpressionTranslationException(
                $"Unable to parse C# expression '{expression}': {string.Join("; ", syntax.GetDiagnostics().Select(d => d.ToString()))}");
        }

        return new CSharpExpressionToJavaScriptVisitor().Translate(syntax);
    }

    public string Translate(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Visit(expression) ?? throw Unsupported(expression);
    }

    public override string DefaultVisit(SyntaxNode node) => throw Unsupported(node);

    public override string VisitIdentifierName(IdentifierNameSyntax node) => node.Identifier.ValueText;

    public override string VisitGenericName(GenericNameSyntax node) =>
        throw Unsupported(node, "Generic expressions are not supported.");

    public override string VisitLiteralExpression(LiteralExpressionSyntax node) =>
        node.Kind() switch
        {
            SyntaxKind.NullLiteralExpression => "null",
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.StringLiteralExpression => JsonSerializer.Serialize((string?)node.Token.Value ?? node.Token.ValueText),
            SyntaxKind.CharacterLiteralExpression => JsonSerializer.Serialize(node.Token.ValueText),
            SyntaxKind.NumericLiteralExpression => FormatNumber(node.Token),
            _ => throw Unsupported(node)
        };

    public override string VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) =>
        $"({Translate(node.Expression)})";

    public override string VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        var operand = Translate(node.Operand);
        return node.OperatorToken.Kind() switch
        {
            SyntaxKind.PlusToken => $"+{operand}",
            SyntaxKind.MinusToken => $"-{operand}",
            SyntaxKind.ExclamationToken => $"!{operand}",
            SyntaxKind.TildeToken => $"~{operand}",
            SyntaxKind.PlusPlusToken => $"++{operand}",
            SyntaxKind.MinusMinusToken => $"--{operand}",
            _ => throw Unsupported(node)
        };
    }

    public override string VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        var operand = Translate(node.Operand);
        return node.OperatorToken.Kind() switch
        {
            SyntaxKind.PlusPlusToken => $"{operand}++",
            SyntaxKind.MinusMinusToken => $"{operand}--",
            _ => throw Unsupported(node)
        };
    }

    public override string VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var op = node.OperatorToken.Kind() switch
        {
            SyntaxKind.EqualsEqualsToken => "===",
            SyntaxKind.ExclamationEqualsToken => "!==",
            SyntaxKind.AmpersandAmpersandToken => "&&",
            SyntaxKind.BarBarToken => "||",
            SyntaxKind.QuestionQuestionToken => "??",
            SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken or
                SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken or
                SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.AsteriskToken or
                SyntaxKind.SlashToken or SyntaxKind.PercentToken or SyntaxKind.AmpersandToken or
                SyntaxKind.BarToken or SyntaxKind.CaretToken or SyntaxKind.LessThanLessThanToken or
                SyntaxKind.GreaterThanGreaterThanToken => node.OperatorToken.Text,
            _ => throw Unsupported(node)
        };

        return $"({Translate(node.Left)} {op} {Translate(node.Right)})";
    }

    public override string VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        var op = node.OperatorToken.Kind() switch
        {
            SyntaxKind.EqualsToken or SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or
                SyntaxKind.AsteriskEqualsToken or SyntaxKind.SlashEqualsToken or SyntaxKind.PercentEqualsToken or
                SyntaxKind.AmpersandEqualsToken or SyntaxKind.BarEqualsToken or SyntaxKind.CaretEqualsToken or
                SyntaxKind.LessThanLessThanEqualsToken or SyntaxKind.GreaterThanGreaterThanEqualsToken =>
                node.OperatorToken.Text,
            SyntaxKind.QuestionQuestionEqualsToken => "??=",
            _ => throw Unsupported(node)
        };

        return $"{Translate(node.Left)} {op} {Translate(node.Right)}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionSyntax node) =>
        $"({Translate(node.Condition)} ? {Translate(node.WhenTrue)} : {Translate(node.WhenFalse)})";

    public override string VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var receiver = Translate(node.Expression);
        var member = node.Name.Identifier.ValueText;
        return $"{receiver}.{MapMember(receiver, member)}";
    }

    public override string VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var arguments = string.Join(", ", node.ArgumentList.Arguments.Select(a => TranslateArgument(a)));

        if (node.Expression is MemberAccessExpressionSyntax member)
        {
            var receiver = Translate(member.Expression);
            var method = MapMethod(receiver, member.Name.Identifier.ValueText);
            if ((receiver == "String" || receiver == "string") &&
                member.Name.Identifier.ValueText is "IsNullOrEmpty" or "IsNullOrWhiteSpace")
            {
                if (node.ArgumentList.Arguments.Count != 1)
                    throw Unsupported(node, $"{member.Name.Identifier.ValueText} requires one argument.");
                var value = TranslateArgument(node.ArgumentList.Arguments[0]);
                return member.Name.Identifier.ValueText == "IsNullOrEmpty"
                    ? $"({value} == null || {value} === \"\")"
                    : $"({value} == null || {value}.trim() === \"\")";
            }

            return $"{receiver}.{method}({arguments})";
        }

        return $"{Translate(node.Expression)}({arguments})";
    }

    public override string VisitElementAccessExpression(ElementAccessExpressionSyntax node) =>
        $"{Translate(node.Expression)}[{string.Join(", ", node.ArgumentList.Arguments.Select(TranslateArgument))}]";

    public override string VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var result = new StringBuilder("`");
        foreach (var content in node.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    result.Append(text.TextToken.ValueText.Replace("`", "\\`", StringComparison.Ordinal));
                    break;
                case InterpolationSyntax interpolation when interpolation.AlignmentClause is null &&
                    interpolation.FormatClause is null:
                    result.Append("${").Append(Translate(interpolation.Expression)).Append('}');
                    break;
                case InterpolationSyntax:
                    throw Unsupported(content, "Interpolated string alignment and format clauses are not supported.");
                default:
                    throw Unsupported(content);
            }
        }

        return result.Append('`').ToString();
    }

    public override string VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) =>
        $"({node.Parameter.Identifier.ValueText}) => {TranslateLambdaBody(node.Body)}";

    public override string VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) =>
        $"({string.Join(", ", node.ParameterList.Parameters.Select(p => p.Identifier.ValueText))}) => {TranslateLambdaBody(node.Body)}";

    private string TranslateLambdaBody(CSharpSyntaxNode body)
    {
        if (body is ExpressionSyntax expression)
            return Translate(expression);

        if (body is BlockSyntax block && block.Statements.Count == 1 &&
            block.Statements[0] is ReturnStatementSyntax { Expression: not null } returnStatement)
            return Translate(returnStatement.Expression);

        throw Unsupported(body, "Only expression-bodied lambdas (or a single return statement) are supported.");
    }

    private static string TranslateArgument(ArgumentSyntax argument)
    {
        if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != 0)
            throw new ExpressionTranslationException($"Named and ref/out arguments are not supported: '{argument}'.");
        return new CSharpExpressionToJavaScriptVisitor().Translate(argument.Expression);
    }

    private static string MapMember(string receiver, string member) =>
        member switch
        {
            "Length" when receiver != "Math" => "length",
            "Count" when receiver != "Math" => "length",
            _ => member
        };

    private static string MapMethod(string receiver, string method) =>
        receiver == "Math"
            ? method switch
            {
                "Abs" => "abs",
                "Ceiling" => "ceil",
                "Floor" => "floor",
                "Round" => "round",
                "Max" => "max",
                "Min" => "min",
                _ => method
            }
            : method switch
        {
            "ToString" => "toString",
            "ToLower" or "ToLowerInvariant" => "toLowerCase",
            "ToUpper" or "ToUpperInvariant" => "toUpperCase",
            "Trim" => "trim",
            "TrimStart" => "trimStart",
            "TrimEnd" => "trimEnd",
            "Contains" => "includes",
            "StartsWith" => "startsWith",
            "EndsWith" => "endsWith",
            "Replace" => "replaceAll",
            "Substring" => "substring",
            "IndexOf" => "indexOf",
            "Add" => "push",
            _ => method
        };

    private static string FormatNumber(SyntaxToken token)
    {
        var text = token.Text.TrimEnd('u', 'U', 'l', 'L', 'f', 'F', 'd', 'D', 'm', 'M');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? text
            : throw new ExpressionTranslationException($"Unsupported numeric literal '{token.Text}'.");
    }

    private static ExpressionTranslationException Unsupported(SyntaxNode node, string? detail = null) =>
        new($"Unsupported C# expression syntax '{node.Kind()}': {detail ?? node.ToString()}");
}

/// <summary>Reports an expression that cannot be safely emitted as JavaScript.</summary>
public sealed class ExpressionTranslationException : InvalidOperationException
{
    public ExpressionTranslationException(string message) : base(message) { }
}

/// <summary>Short alias for consumers that prefer the JavaScript-oriented name.</summary>
public sealed class JavaScriptExpressionVisitor
{
    public static string Translate(string expression) => CSharpExpressionToJavaScriptVisitor.Translate(expression);

    public string Visit(string expression) => Translate(expression);
}

/// <summary>Compatibility alias for callers using the C#-to-JavaScript naming convention.</summary>
public static class CSharpToJavaScriptExpressionVisitor
{
    public static string Translate(string expression) => CSharpExpressionToJavaScriptVisitor.Translate(expression);
}

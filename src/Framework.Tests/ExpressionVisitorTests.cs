using RazorWings.Compiler.CodeGen;
using Xunit;

namespace RazorWings.Tests;

public sealed class ExpressionVisitorTests
{
    [Theory]
    [InlineData("count == 1", "(count === 1)")]
    [InlineData("name != null && enabled", "((name !== null) && enabled)")]
    [InlineData("count++", "count++")]
    [InlineData("count += 2", "count += 2")]
    [InlineData("items.Count", "items.length")]
    [InlineData("name.ToLowerInvariant()", "name.toLowerCase()")]
    [InlineData("Math.Abs(delta)", "Math.abs(delta)")]
    [InlineData("isReady ? \"yes\" : \"no\"", "(isReady ? \"yes\" : \"no\")")]
    public void TranslatesSupportedExpressions(string source, string expected)
    {
        Assert.Equal(expected, CSharpExpressionToJavaScriptVisitor.Translate(source));
    }

    [Fact]
    public void TranslatesInterpolationAndLambda()
    {
        Assert.Equal("`Hello ${name}!`", CSharpExpressionToJavaScriptVisitor.Translate("$\"Hello {name}!\""));
        Assert.Equal("(value) => (value * 2)", CSharpExpressionToJavaScriptVisitor.Translate("value => value * 2"));
    }

    [Fact]
    public void RejectsUnsupportedSyntaxExplicitly()
    {
        var exception = Assert.Throws<ExpressionTranslationException>(
            () => CSharpExpressionToJavaScriptVisitor.Translate("new Widget()"));

        Assert.Contains("Unsupported", exception.Message);
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Radish.WindowsRazor.Program;

namespace Radish.WindowsRazor;

internal static class Tokens
{
    internal static string[] Values(string source, bool normalizeLineEndings = false, bool removeMarker = false)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        Require(!tree.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "Generated C# has syntax errors");
        return new LiteralConcatenation().Visit(tree.GetRoot())!.DescendantTokens().Select(token =>
        {
            var value = token.ValueText;
            if (token.IsKind(SyntaxKind.StringLiteralToken))
            {
                if (normalizeLineEndings) value = value.Replace("\r\n", "\n", StringComparison.Ordinal);
                if (removeMarker) value = value.Replace(MarkerAttribute, "", StringComparison.Ordinal);
            }
            return token.RawKind + ":" + value;
        }).ToArray();
    }
    private sealed class LiteralConcatenation : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitBinaryExpression(Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax node)
        {
            var rewritten = (Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;
            if (rewritten.IsKind(SyntaxKind.AddExpression) && rewritten.Left is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax left && rewritten.Right is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax right && left.IsKind(SyntaxKind.StringLiteralExpression) && right.IsKind(SyntaxKind.StringLiteralExpression))
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(left.Token.ValueText + right.Token.ValueText));
            return rewritten;
        }
    }
    internal static void Equal(string first, string second, bool normalizeLineEndings = false, bool removeMarker = false) =>
        Require(Values(first, normalizeLineEndings).SequenceEqual(Values(second, normalizeLineEndings, removeMarker)), "Unknown generated C# token/value difference");
    internal static void Compare(string baseline, string lf, string crlf, string marked, string report)
    {
        var results = new List<object>();
        foreach (var pair in Sources.Pairs)
        {
            var relative = pair["output"]!.GetValue<string>();
            var original = File.ReadAllText(Sources.At(baseline, relative));
            var plain = File.ReadAllText(Sources.At(lf, relative));
            var windows = File.ReadAllText(Sources.At(crlf, relative));
            var diagnostic = File.ReadAllText(Sources.At(marked, relative));
            var isMarker = Path.GetFileName(relative) == "_ErrorAlert.cshtml.cs";
            Equal(original, windows);
            Equal(plain, windows, normalizeLineEndings: true);
            Equal(windows, diagnostic, removeMarker: isMarker);
            Require(Values(diagnostic).Any(value => value.Contains(Marker, StringComparison.Ordinal)) == isMarker, "Template marker missing or escaped its one output");
            results.Add(new { path = relative, originalMatchesCrlf = true, lfDifferenceOnlyLineEndings = true, markedDifferenceOnlyApprovedAttribute = true });
        }
        Save(report, results);
    }
}

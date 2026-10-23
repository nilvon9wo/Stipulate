using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Testing;

/// <summary>Asserts that diagnostics are exactly the ones marked up in the source.</summary>
public static class DiagnosticAssert
{
    private const string LineSeparator = "\n  ";

    public static void MatchesMarkup(DiagnosticMarkup markup, ImmutableArray<Diagnostic> diagnostics)
    {
        string[] expected = [.. markup.Expected.Select(diagnostic => Describe(markup, diagnostic)).Order()];
        string[] actual = [.. diagnostics.Select(diagnostic => Describe(markup, diagnostic)).Order()];
        string details = string.Join(LineSeparator, diagnostics.Select(DescribeInFull));
        Assert.True(
            expected.SequenceEqual(actual),
            $"Expected:{LineSeparator}{string.Join(LineSeparator, expected)}\nActual:{LineSeparator}{details}"
        );
    }

    private static string Describe(DiagnosticMarkup markup, ExpectedDiagnostic diagnostic)
        => $"{diagnostic.Id} at '{markup.Source.Substring(diagnostic.Span.Start, diagnostic.Span.Length)}'";

    private static string Describe(DiagnosticMarkup markup, Diagnostic diagnostic)
        => Describe(markup, new ExpectedDiagnostic(diagnostic.Id, diagnostic.Location.SourceSpan));

    private static string DescribeInFull(Diagnostic diagnostic)
        => $"{diagnostic.Id} at {diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.GetMessage()}";
}

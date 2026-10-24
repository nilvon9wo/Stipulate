using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// Every C# example in README.md and docs/ compiles, and the analyzer reports on it exactly the
/// diagnostics its comments show: documentation that no longer matches the analyzer fails here.
/// </summary>
public class DocumentationExampleTest
{
    [Theory]
    [MemberData(nameof(Examples))]
    public async Task Analyze_WhenADocumentedExampleIsCompiled_ReportsExactlyWhatItShows(string page, int line)
    {
        // Arrange
        DocumentationExample example = DocumentationExample.Read(page, line);
        MetadataReference[] references = example.CompileReferences();

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(
            TestCompilation.Usings + example.Code,
            solutionRoot: null,
            references
        );

        // Assert
        Assert.Equal(example.ShownDiagnostics, Describe(diagnostics));
    }

    public static TheoryData<string, int> Examples()
        => [.. DocumentationExample.ReadAll().Select(static example => (example.Page, example.Line))];

    private static ImmutableArray<string> Describe(ImmutableArray<Diagnostic> diagnostics)
        => [.. diagnostics
            .Select(static diagnostic => $"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Id}")
            .Order(StringComparer.Ordinal)];
}

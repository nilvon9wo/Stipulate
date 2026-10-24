using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// Every C# example in README.md, docs/ and the package's README compiles, and the analyzer reports
/// on it exactly the diagnostics its comments show: documentation that no longer matches the
/// analyzer fails here.
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

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task Analyze_WhenADocumentedExampleQuotesAMessage_QuotesItFaithfully(string page, int line)
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
        foreach ((string shown, string quote) in example.Quotes)
        {
            List<string> messages = [.. diagnostics
                .Where(diagnostic => Describe(diagnostic) == shown)
                .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))];
            Assert.True(
                messages.Any(message => DocumentationExample.IsQuoteOf(quote, message)),
                $"{example}: \"{shown}: {quote}\" doesn't quote any {shown} reported: "
                    + string.Join(" | ", messages)
            );
        }
    }

    [Theory]
    [InlineData("The argument 0 is rejected by [Range(1, 10)].")]
    [InlineData("the argument 0 is rejected by [Range(1, 10)].")]
    [InlineData("is rejected by [Range(1, 10)]")]
    [InlineData("The argument 0 ... by [Range(1, 10)]")]
    [InlineData("The   argument 0 is rejected")]
    [InlineData("...")]
    public void IsQuoteOf_WhenTheQuoteIsTheMessageOrExcerptsOfIt_IsTrue(string quote)
    {
        // Arrange
        const string message = "The argument 0 is rejected by [Range(1, 10)].";

        // Act
        bool quotes = DocumentationExample.IsQuoteOf(quote, message);

        // Assert
        Assert.True(quotes);
    }

    [Theory]
    [InlineData("The argument 0 is refused by [Range(1, 10)].")]
    [InlineData("by [Range(1, 10)] ... The argument 0")]
    [InlineData("The argument 0 is rejected by [range(1, 10)].")]
    [InlineData("The argument 0 is rejected by [Range(1, 10)]. Pass another.")]
    public void IsQuoteOf_WhenTheQuoteRewordsReordersOrAddsToTheMessage_IsFalse(string quote)
    {
        // Arrange
        const string message = "The argument 0 is rejected by [Range(1, 10)].";

        // Act
        bool quotes = DocumentationExample.IsQuoteOf(quote, message);

        // Assert
        Assert.False(quotes);
    }

    [Fact]
    public void Quotes_WhenACommentContinuesOnTheNextLines_JoinsThemUntilTheNextDiagnostic()
    {
        // Arrange
        DocumentationExample example = new(
            "page.md",
            1,
            """
            // error ST1001: the first
            //   part, then
            // the second
            // warning ST1002: another
            // warning ST1002
            int x = 0; // error ST3001: trailing
            """,
            []
        );

        // Act
        ImmutableArray<(string Diagnostic, string Quote)> quotes = example.Quotes;

        // Assert
        Assert.Equal(
            [
                ("error ST1001", "the first part, then the second"),
                ("warning ST1002", "another"),
                ("error ST3001", "trailing"),
            ],
            quotes
        );
    }

    public static TheoryData<string, int> Examples()
        => [.. DocumentationExample.ReadAll().Select(static example => (example.Page, example.Line))];

    private static ImmutableArray<string> Describe(ImmutableArray<Diagnostic> diagnostics)
        => [.. diagnostics.Select(Describe).Order(StringComparer.Ordinal)];

    private static string Describe(Diagnostic diagnostic)
        => $"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Id}";
}

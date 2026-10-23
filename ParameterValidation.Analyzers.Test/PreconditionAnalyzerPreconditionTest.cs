using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ParameterValidation.Testing;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// How the built-in DataAnnotations attributes compare as preconditions: an implementation may
/// weaken its contract's, never strengthen them, and must drop one explicitly.
/// </summary>
public class PreconditionAnalyzerPreconditionTest
{
    [Theory]
    [InlineData("MinLength(1)", "MinLength(1)", "int[]")]
    [InlineData("MinLength(1)", "MinLength(0)", "int[]")]
    [InlineData("Range(1, 10)", "Range(0, 10)", "int")]
    [InlineData("Range(1, 10)", "Range(1.0, 10.0)", "int")]
    [InlineData("AllowedValues(1, 2)", "AllowedValues(1, 2, 3)", "int")]
    [InlineData("DeniedValues(1, 2)", "DeniedValues(1)", "int")]
    [InlineData("AllowedValues(1, 2)", "DeniedValues(3)", "int")]
    [InlineData("Length(1, 10)", "MinLength(1), MaxLength(20)", "string")]
    [InlineData("StringLength(10)", "StringLength(20)", "string")]
    [InlineData("Required", "Required(AllowEmptyStrings = true)", "string")]
    [InlineData("", "MaxLength", "string")]
    [InlineData("Range(0, 10)", "Range(double.NegativeInfinity, 10)", "double")]
    public async Task Analyze_WhenTheImplementationIsNoStronger_ReportsNothing(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(1)", "{|PV1001:MinLength(2)|}", "int[]")]
    [InlineData("Range(1, 10)", "{|PV1001:Range(2, 10)|}", "int")]
    [InlineData("Range(1, 10)", "{|PV1001:Range(1, 10, MinimumIsExclusive = true)|}", "int")]
    [InlineData("Range(1, 10)", "{|PV1001:Range(1, 10, MaximumIsExclusive = true)|}", "int")]
    [InlineData("AllowedValues(1, 2)", "{|PV1001:AllowedValues(1)|}", "int")]
    [InlineData("DeniedValues(1, 2)", "{|PV1001:DeniedValues(1, 2, 3)|}", "int")]
    [InlineData("AllowedValues(1, 2)", "{|PV1001:DeniedValues(2)|}", "int")]
    [InlineData("Length(1, 10)", "MinLength(1), {|PV1001:MaxLength(5)|}", "string")]
    [InlineData("Required(AllowEmptyStrings = true)", "{|PV1001:Required|}", "string")]
    [InlineData("", "{|PV1001:Required|}", "string")]
    [InlineData("", "{|PV1001:StringLength(10)|}", "string")]
    [InlineData("", "{|PV1001:AllowedValues(1)|}", "int")]
    [InlineData("", "{|PV1001:DeniedValues(1)|}", "int")]
    [InlineData("DeniedValues(1)", "{|PV1001:AllowedValues(2)|}", "int")]
    [InlineData("MinLength(1), Length(2, 10)", "{|PV1001:MinLength(3)|}, MaxLength(10)", "string")]
    [InlineData("Length(2, 10), MinLength(1)", "{|PV1001:MinLength(3)|}, MaxLength(10)", "string")]
    public async Task Analyze_WhenTheImplementationIsStronger_ReportsPV1001OnItsAttribute(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(1)", "", "int[]")]
    [InlineData("Required", "", "string")]
    [InlineData("Range(1, 10)", "", "int")]
    [InlineData("Length(1, 10)", "MinLength(1)", "string")]
    [InlineData("AllowedValues(1, 2)", "", "int")]
    public async Task Analyze_WhenTheImplementationDropsABound_ReportsPV1002OnTheParameter(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        string parameter = $"{{|PV1002:{ContractSource.Parameter}|}}";
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, implementation, type, parameter));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("Length(1, 10)", "MinLength(1), MaxLength", "string")]
    [InlineData("Length(1, 10)", "MinLength(0), MaxLength(10)", "string")]
    [InlineData("Required", "", "string?")]
    [InlineData("Required", "", "int?")]
    [InlineData("AllowedValues(1, 2)", "DeniedValues(0)", "int")]
    public async Task Analyze_WhenTheImplementationDropsEachBoundExplicitly_ReportsNothing(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenABuiltInAttributesArgumentIsNotOfTheRightKind_ReportsPV2002()
    {
        // Arrange
        string source = ContractSource.ForParameter("AllowedValues(1)", "{|PV2002:AllowedValues(null)|}", "int");
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenABuiltInAttributeIsNotDescribed_ReportsPV2002()
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter("", "{|PV2002:EmailAddress|}", "string"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenABuiltInAttributeThatIsNotDescribedIsRestated_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter("EmailAddress", "EmailAddress", "string"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers.Test;

/// <summary>
/// What an implementation hands back (a getter's value, an <c>out</c> or <c>ref</c> parameter)
/// must satisfy everything its contract's attributes promise: it may strengthen them, but not
/// weaken or leave them out.
/// </summary>
public class PreconditionAnalyzerGuaranteeTest
{
    private const string Divisible =
        "public sealed class DivisibleAttribute(int divisor) : ValidationAttribute "
        + "{ public int Divisor => divisor; }\n";

    [Theory]
    [InlineData("Range(1, 10)", "Range(1, 10)")]
    [InlineData("Range(1, 10)", "Range(2, 5)")]
    [InlineData("MaxLength", "")]
    [InlineData("MinLength(-1)", "")]
    [InlineData("MinLength(0)", "")]
    public async Task Analyze_WhenAGetterIsNoWeaker_ReportsNothing(string contract, string implementation)
    {
        // Arrange
        string type = contract.Contains("Length", StringComparison.Ordinal)
            ? "int[]"
            : "int";
        DiagnosticMarkup markup = new(ContractSource.ForProperty(contract, "get;", implementation, "get;", type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("Range(1, 10)", "Range(1, 20)", "get;")]
    [InlineData("Range(1, 10)", "Range(0, 10)", "get;")]
    [InlineData("Range(1, 10)", "", "get;")]
    [InlineData("Range(1, 10)", "MinLength(1)", "get;")]
    [InlineData("Range(1, 10)", "Range(0, 10)", "get; set;")]
    public async Task Analyze_WhenAGetterIsWeaker_ReportsPV1003OnTheProperty(
        string contract,
        string implementation,
        string implementationAccessors
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(
            ContractSource.ForProperty(
                contract,
                "get;",
                implementation,
                implementationAccessors,
                "int",
                "{|PV1003:Value|}"
            )
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterDropsAnExclusiveMinimumLengthOfZero_ReportsPV1003()
    {
        // Arrange - unlike MinLength(0), a length above 0 rejects something: an empty array
        const string nonEmpty = """
            [ComparableValidation(
                ValidationDimensions.Length,
                ValidationComparison.LowerBound,
                Value = 0,
                IsExclusive = true)]
            public sealed class NonEmptyAttribute : ValidationAttribute { }

            """;
        DiagnosticMarkup markup = new(
            nonEmpty + ContractSource.ForProperty("NonEmpty", "get;", "", "get;", "int[]", "{|PV1003:Value|}")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterIsNullableAndTheContractRequiresAValue_ReportsPV1003()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            ContractSource.ForProperty("Required", "get;", "", "get;", "string?", "{|PV1003:Value|}")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterDropsAnAttributeThatIsNotDescribed_ReportsPV1003Once()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            "public sealed class EvenAttribute : ValidationAttribute { }\n"
            + ContractSource.ForProperty("Even", "get; set;", "", "get; set;", "int", "{|PV1003:Value|}")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterChangesTheArgumentsOfAnAttributeThatIsNotDescribed_ReportsPV2001Once()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            Divisible
            + ContractSource.ForProperty("Divisible(2)", "get; set;", "{|PV2001:Divisible(4)|}", "get; set;")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterOnlyChangesTheArgumentsOfAnAttributeThatIsNotDescribed_ReportsPV2001()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            Divisible
            + ContractSource.ForProperty("Divisible(2)", "get;", "Divisible(4)", "get;", "int", "{|PV2001:Value|}")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(2)")]
    [InlineData("MinLength(1)")]
    public async Task Analyze_WhenAnOutParameterIsNoWeaker_ReportsNothing(string implementation)
    {
        // Arrange
        DiagnosticMarkup markup = new(OutParameterSource(implementation, "text"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(0)")]
    [InlineData("")]
    public async Task Analyze_WhenAnOutParameterIsWeaker_ReportsPV1003OnTheParameter(string implementation)
    {
        // Arrange
        DiagnosticMarkup markup = new(OutParameterSource(implementation, "{|PV1003:text|}"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(1)", "text")]
    [InlineData("MinLength(0)", "{|PV1003:text|}")]
    [InlineData("{|PV1001:MinLength(2)|}", "text")]
    public async Task Analyze_WhenARefParameterDiffersFromItsContract_ReportsTheDifference(
        string implementation,
        string parameter
    )
    {
        // Arrange
        DiagnosticMarkup markup = new($$"""
            public interface IEditor { void Edit([MinLength(1)] ref string text); }
            public class Editor : IEditor
            {
                public void Edit([{{implementation}}] ref string {{parameter}}) { }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAWeakerGetterIsInherited_ReportsPV1003OnTheClass()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface ISized { [Range(1, 10)] int Size { get; } }
            public class Base
            {
                [Range(1, 20)] public int Size => 3;
            }
            public class {|PV1003:Derived|} : Base, ISized { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAGetterOverrideIsWeaker_ReportsPV1003()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public abstract class Base { [Range(1, 10)] public abstract int Size { get; } }
            public class Derived : Base
            {
                [Range(1, 20)] public override int {|PV1003:Size|} => 3;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    private static string OutParameterSource(string implementation, string parameter)
    {
        string attributes = implementation.Length == 0
            ? string.Empty
            : $"[{implementation}] ";
        return $$"""
            public interface IReader { bool TryRead([MinLength(1)] out string text); }
            public class Reader : IReader
            {
                public bool TryRead({{attributes}}out string {{parameter}}) { text = "ab"; return true; }
            }
            """;
    }
}

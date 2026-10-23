using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers.Test;

/// <summary>
/// Custom validation attributes: comparable once described with <c>[ComparableValidation]</c>, and
/// otherwise reported according to whether the developer can describe them.
/// </summary>
public class PreconditionAnalyzerCustomAttributeTest
{
    private const string Even = """
        public sealed class EvenAttribute(int divisor) : ValidationAttribute
        {
            public int Divisor => divisor;
            public override bool IsValid(object? value) => value is not int number || number % divisor == 0;
        }

        """;

    private const string Described = """
        [ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]
        public sealed class NotEmptyAttribute : ValidationAttribute { }

        [ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Argument = "minimum")]
        public class AtLeastAttribute(int minimum) : ValidationAttribute { public int Minimum => minimum; }

        [ComparableValidation("Parity", ValidationComparison.Flag)]
        public sealed class OddAttribute : ValidationAttribute { }

        [ComparableValidation(
            ValidationDimensions.Value,
            ValidationComparison.UpperBound,
            Argument = "maximum",
            ExclusiveArgument = "IsExclusive")]
        public sealed class AtMostAttribute(int maximum) : ValidationAttribute
        {
            public int Maximum => maximum;
            public bool IsExclusive { get; set; }
        }

        [ComparableValidation(ValidationDimensions.Value, ValidationComparison.AllowedValues, Value = new[] { 1, 2 })]
        public sealed class OneOrTwoAttribute : ValidationAttribute { }

        """;

    private const string Vendor = """
        namespace Vendor;

        public sealed class EvenAttribute(int divisor) : ValidationAttribute
        {
            public int Divisor => divisor;
        }

        public sealed class NonEmptyAttribute : ValidationAttribute { }
        """;

    private const string VendorDescription =
        "[assembly: ComparableValidation(typeof(Vendor.NonEmptyAttribute), ValidationDimensions.Length, "
        + "ValidationComparison.LowerBound, Value = 1)]\n";

    private const string RepositoryRoot = "/repo/";

    [Fact]
    public async Task Analyze_WhenAnAttributeThatIsNotDescribedIsRestated_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(Even + ContractSource.ForParameter("Even(2)", "Even(2)", "int"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeInThisProjectIsNotDescribed_ReportsPV2001()
    {
        // Arrange
        DiagnosticMarkup markup = new(Even + ContractSource.ForParameter("Even(2)", "{|PV2001:Even(4)|}", "int"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("MinLength(2)", "NotEmpty", "int[]")]
    [InlineData("Range(5, 100)", "AtLeast(5), Range(0, 100)", "int")]
    [InlineData("AtLeast(5)", "AtLeast(3)", "int")]
    [InlineData("Odd", "Odd", "int")]
    [InlineData("AtMost(5)", "AtMost(6)", "int")]
    [InlineData("AtMost(5, IsExclusive = true)", "AtMost(5)", "int")]
    [InlineData("OneOrTwo", "AllowedValues(1, 2, 3)", "int")]
    public async Task Analyze_WhenADescribedImplementationIsNoStronger_ReportsNothing(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(Described + ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("NotEmpty", "{|PV1001:MinLength(2)|}", "int[]")]
    [InlineData("AtLeast(5)", "{|PV1001:AtLeast(6)|}", "int")]
    [InlineData("", "{|PV1001:Odd|}", "int")]
    [InlineData("AtMost(5)", "{|PV1001:AtMost(5, IsExclusive = true)|}", "int")]
    [InlineData("AllowedValues(1, 2, 3)", "{|PV1001:OneOrTwo|}", "int")]
    [InlineData("AllowedValues(1, 2, 3), OneOrTwo", "{|PV1001:AllowedValues(1)|}", "int")]
    public async Task Analyze_WhenADescribedImplementationIsStronger_ReportsPV1001(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(Described + ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADescribedAttributeLeavesABoundUnstated_ReportsPV1002()
    {
        // Arrange
        string parameter = $"{{|PV1002:{ContractSource.Parameter}|}}";
        string usage = ContractSource.ForParameter("Range(5, 100)", "AtLeast(5)", "int", parameter);
        DiagnosticMarkup markup = new(Described + usage);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADerivedClassIsNotDescribedItself_DoesNotInheritTheDescription()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            Described
            + "public sealed class AtLeastOrZeroAttribute(int minimum) : AtLeastAttribute(minimum) { }\n"
            + ContractSource.ForParameter("AtLeast(5)", "AtLeast(5), {|PV2001:AtLeastOrZero(5)|}", "int")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("\"one\"")]
    [InlineData("true")]
    [InlineData("new[] { 1 }")]
    [InlineData("'a'")]
    public async Task Analyze_WhenAUsagesArgumentIsNotANumber_ReportsPV2001(string argument)
    {
        // Arrange
        const string declaration = """
            [ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Argument = "minimum")]
            public sealed class AtLeastAttribute(object minimum) : ValidationAttribute
            {
                public object Minimum => minimum;
            }

            """;
        DiagnosticMarkup markup = new(
            declaration + ContractSource.ForParameter("AtLeast(1)", $"{{|PV2001:AtLeast({argument})|}}", "int")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeFromAPackageIsNotDescribed_ReportsPV2002()
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor");
        DiagnosticMarkup markup = new(VendorEvenUsage(DiagnosticIds.NotComparableExternal));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, vendor);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeFromAProjectReferenceIsNotDescribed_ReportsPV2001()
    {
        // Arrange
        MetadataReference vendor = TestCompilation
            .Compile(TestCompilation.Usings + Vendor, "Vendor")
            .ToMetadataReference();
        DiagnosticMarkup markup = new(VendorEvenUsage(DiagnosticIds.NotComparableInSolution));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, vendor);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("/repo/Vendor/bin/Debug/Vendor.dll")]
    [InlineData(@"\repo\Vendor\bin\Debug\Vendor.dll")]
    public async Task Analyze_WhenAnAttributeFromAFileUnderTheSolutionRootIsNotDescribed_ReportsPV2001(string filePath)
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor", filePath);
        DiagnosticMarkup markup = new(VendorEvenUsage(DiagnosticIds.NotComparableInSolution));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, RepositoryRoot, vendor);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData(RepositoryRoot, "/home/me/.nuget/packages/vendor/1.0.0/lib/Vendor.dll")]
    [InlineData(" ", "/repo/Vendor/bin/Debug/Vendor.dll")]
    [InlineData(null, "/repo/Vendor/bin/Debug/Vendor.dll")]
    public async Task Analyze_WhenAnAttributeFromAFileOutsideTheSolutionRootIsNotDescribed_ReportsPV2002(
        string? solutionRoot,
        string filePath
    )
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor", filePath);
        DiagnosticMarkup markup = new(VendorEvenUsage(DiagnosticIds.NotComparableExternal));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, solutionRoot, vendor);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    /// <summary>A usage of the package's <c>Vendor.Even</c>, marked up with <paramref name="id"/>.</summary>
    private static string VendorEvenUsage(string id)
        => ContractSource.ForParameter("Vendor.Even(2)", $"{{|{id}:Vendor.Even(4)|}}", "int");

    [Fact]
    public async Task Analyze_WhenThisAssemblyDescribesAPackagesAttribute_ComparesIt()
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor");
        DiagnosticMarkup markup = new(
            VendorDescription + ContractSource.ForParameter("Vendor.NonEmpty", "{|PV1001:MinLength(2)|}", "int[]")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, vendor);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAReferencedAssemblyDescribesAPackagesAttribute_ComparesIt()
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor");
        MetadataReference descriptions =
            TestCompilation.CompileToImage(VendorDescription, "Descriptions", references: vendor);
        string usage = ContractSource.ForParameter("Vendor.NonEmpty", "{|PV1001:MinLength(2)|}", "int[]");
        DiagnosticMarkup markup = new(usage);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, vendor, descriptions);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADescriptionIsInvalid_LeavesTheAttributeUndescribed()
    {
        // Arrange
        const string declaration = """
            [{|PV2003:ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound)|}]
            public sealed class NoBoundAttribute(int length) : ValidationAttribute { public int Length => length; }

            """;
        DiagnosticMarkup markup = new(
            declaration + ContractSource.ForParameter("NoBound(1)", "{|PV2001:NoBound(0)|}", "int[]")
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers.Test;

/// <summary>Every rule a <c>[ComparableValidation]</c> must follow, and the reason it gives when broken.</summary>
public class DescriptionAnalyzerTest
{
    private const string Subject = """
        public class SubjectAttribute(int minimum) : ValidationAttribute
        {
            public int Minimum => minimum;
            public int Count { get; set; }
            public bool Exclusive { get; set; }
            public int Limit;
            public static int Shared { get; set; }
            public static int SharedLimit;
            public readonly int Fixed;
            public const int Constant = 1;
        }
        """;

    private const string Length = "ValidationDimensions.Length, ValidationComparison.LowerBound, ";
    private const string Allowed = "ValidationDimensions.Value, ValidationComparison.AllowedValues, ";
    private const string Denied = "ValidationDimensions.Value, ValidationComparison.DeniedValues, ";
    private const string NoArgument = "'SubjectAttribute' has no constructor parameter or settable property named ";
    private const string Flag = "a Flag takes no Argument, Value, IsExclusive or ExclusiveArgument";
    private const string Exclusive = "only a LowerBound or an UpperBound can be exclusive";
    private const string NoBoolean = "'SubjectAttribute' has no bool constructor parameter or settable property named ";
    private const string ClassFormTakesNoType =
        "on a class it describes that class, so it takes no attribute type (that form is for "
        + "[assembly: ComparableValidation(typeof(...), ...)])";

    /// <summary>A marker on <c>SubjectAttribute</c>, and why it is invalid.</summary>
    public static TheoryData<string, string> InvalidClassMarkers => new()
    {
        { "null, ValidationComparison.Flag", "it needs a dimension and a comparison" },
        { "\" \", ValidationComparison.Flag", "the dimension is empty" },
        { "\"Shape\", (ValidationComparison)9", "9 isn't a ValidationComparison" },
        { "\"Shape\", (ValidationComparison)(-1)", "-1 isn't a ValidationComparison" },
        { "\"Shape\", ValidationComparison.Flag, Value = 1", Flag },
        { "\"Shape\", ValidationComparison.Flag, Argument = \"minimum\"", Flag },
        { "\"Shape\", ValidationComparison.Flag, IsExclusive = true", Flag },
        { "\"Shape\", ValidationComparison.Flag, ExclusiveArgument = \"Exclusive\"", Flag },
        { "ValidationDimensions.Length, ValidationComparison.LowerBound", "a LowerBound needs an Argument or a Value" },
        { Length + "Value = null", "a LowerBound needs an Argument or a Value" },
        { Length + "Argument = \"nope\"", NoArgument + "'nope'" },
        { Length + "Argument = \"Minimum\"", NoArgument + "'Minimum'" },
        { Length + "Argument = \"Shared\"", NoArgument + "'Shared'" },
        { Length + "Argument = \"SharedLimit\"", NoArgument + "'SharedLimit'" },
        { Length + "Argument = \"Fixed\"", NoArgument + "'Fixed'" },
        { Length + "Argument = \"Constant\"", NoArgument + "'Constant'" },
        { Length + "Argument = \"IsValid\"", NoArgument + "'IsValid'" },
        { Length + "Value = \"one\"", "the Value of a LowerBound must be a number" },
        { Length + "Value = true", "the Value of a LowerBound must be a number" },
        { Allowed + "Value = 1", "the Value of a AllowedValues must be an array" },
        { Denied + "Value = new[] { 1 }, IsExclusive = true", Exclusive },
        { Denied + "Value = new[] { 1 }, ExclusiveArgument = \"Exclusive\"", Exclusive },
        { Length + "Value = 1, ExclusiveArgument = \"Nope\"", NoBoolean + "'Nope'" },
        { Length + "Value = 1, ExclusiveArgument = \"Count\"", NoBoolean + "'Count'" },
        { Length + "Value = 1, ExclusiveArgument = \"Limit\"", NoBoolean + "'Limit'" },
        { "typeof(SubjectAttribute), \"Shape\", ValidationComparison.Flag", ClassFormTakesNoType },
    };

    public static TheoryData<string> ValidClassMarkers =>
    [
        "\"Shape\", ValidationComparison.Flag",
        Length + "Argument = \"minimum\"",
        Length + "Argument = \"Count\"",
        Length + "Argument = \"Limit\"",
        Length + "Argument = \"ErrorMessage\"",
        Length + "Value = 1.5",
        Length + "Value = 1, IsExclusive = true",
        Length + "Argument = \"minimum\", ExclusiveArgument = \"Exclusive\"",
        Allowed + "Value = new[] { 1, 2 }",
        Denied + "Argument = \"minimum\"",
    ];

    [Theory]
    [MemberData(nameof(InvalidClassMarkers))]
    public async Task Analyze_WhenAClassMarkerIsInvalid_ReportsPV2003WithTheReason(string arguments, string reason)
    {
        // Arrange
        DiagnosticMarkup markup = new($"[ComparableValidation({arguments})]\n{Subject}");

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal($"This [ComparableValidation] is invalid because {reason}", diagnostic.GetMessage());
    }

    [Theory]
    [MemberData(nameof(ValidClassMarkers))]
    public async Task Analyze_WhenAClassMarkerIsValid_ReportsNothing(string arguments)
    {
        // Arrange
        DiagnosticMarkup markup = new($"[ComparableValidation({arguments})]\n{Subject}");

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAClassMarkerIsInvalid_ReportsPV2003OnTheMarker()
    {
        // Arrange
        const string marker = "[{|PV2003:ComparableValidation(\"Shape\", ValidationComparison.Flag, Value = 1)|}]\n";
        DiagnosticMarkup markup = new(marker + Subject);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheMarkerIsOnAClassThatIsNotAValidationAttribute_ReportsPV2003()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            [{|PV2003:ComparableValidation("Shape", ValidationComparison.Flag)|}]
            public sealed class ShapeAttribute : System.Attribute { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAssemblyMarkerIsValid_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            "[assembly: ComparableValidation(typeof(SubjectAttribute), \"Shape\", ValidationComparison.Flag)]\n"
            + Subject
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("typeof(string), \"Shape\", ValidationComparison.Flag")]
    [InlineData("\"Shape\", ValidationComparison.Flag")]
    public async Task Analyze_WhenAnAssemblyMarkerIsInvalid_ReportsPV2003OnTheMarker(string arguments)
    {
        // Arrange
        DiagnosticMarkup markup = new($"[assembly: {{|PV2003:ComparableValidation({arguments})|}}]\n{Subject}");

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAssemblyMarkerHasNoType_SaysItNeedsOne()
    {
        // Arrange
        DiagnosticMarkup markup = new("[assembly: ComparableValidation(\"Shape\", ValidationComparison.Flag)]");

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        Assert.Equal(
            "This [ComparableValidation] is invalid because on an assembly it needs the type of the attribute "
            + "it describes",
            Assert.Single(diagnostics).GetMessage()
        );
    }

    [Fact]
    public async Task Analyze_WhenAClassHasSeveralMarkers_ReportsOnlyTheInvalidOne()
    {
        // Arrange
        const string markers = "[ComparableValidation(\"Shape\", ValidationComparison.Flag)]\n"
            + "[{|PV2003:ComparableValidation(\"Shape\", ValidationComparison.Flag, Value = 1)|}]\n";
        DiagnosticMarkup markup = new(markers + Subject);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    /// <remarks>A class of the contracts' name in source shadows theirs (warning CS0436).</remarks>
    [Fact]
    public async Task Analyze_WhenAMarkerHasTooFewArguments_ReportsPV2003()
    {
        // Arrange
        const string shadow = """
            namespace Net.NowhereAtAll.ParameterValidation.Contracts
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class ComparableValidationAttribute(string dimension) : System.Attribute
                {
                    public string Dimension => dimension;
                }
            }

            """;
        DiagnosticMarkup markup = new(shadow + "[{|PV2003:ComparableValidation(\"Shape\")|}]\n" + Subject);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPartialClassHasMarkersInTwoFiles_ReportsOnlyTheInvalidOne()
    {
        // Arrange
        const string valid = "[ComparableValidation(\"Shape\", ValidationComparison.Flag)]\n"
            + "public sealed partial class ShapeAttribute : ValidationAttribute { }";
        const string invalid = "[ComparableValidation(\"Size\", ValidationComparison.Flag, Value = 1)]\n"
            + "public sealed partial class ShapeAttribute { }";
        CSharpCompilation compilation = TestCompilation.Compile(TestCompilation.Usings + valid)
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                TestCompilation.Usings + invalid,
                TestCompilation.ParseOptions,
                cancellationToken: TestContext.Current.CancellationToken
            ));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(compilation);

        // Assert
        Assert.Equal(DiagnosticIds.InvalidDescription, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenAnotherAttributeIsApplied_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new("[System.Obsolete]\n" + Subject);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }
}

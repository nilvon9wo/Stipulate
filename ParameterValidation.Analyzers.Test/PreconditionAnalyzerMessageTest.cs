using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ParameterValidation.Testing;

namespace ParameterValidation.Analyzers.Test;

/// <summary>What each diagnostic says: what is wrong, where, and how to fix it.</summary>
public class PreconditionAnalyzerMessageTest
{
    private const string Vendor = """
        namespace Vendor;

        public sealed class EvenAttribute(int divisor) : ValidationAttribute { public int Divisor => divisor; }
        """;

    public static TheoryData<string, string> Messages => new()
    {
        {
            ContractSource.ForParameter("MinLength(1)", "MinLength(2)", "int[]"),
            "Parameter 'value' of 'Implementation.Accept(int[])' strengthens the precondition of "
            + "'IContract.Accept(int[])': [MinLength(2)] requires Length >= 2, but the contract accepts Length >= 1"
        },
        {
            ContractSource.ForParameter("Range(1, 10)", "Range(1, 10, MaximumIsExclusive = true)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [Range(1, 10, MaximumIsExclusive = true)] requires Value < 10, but the "
            + "contract accepts Value <= 10"
        },
        {
            ContractSource.ForParameter("Range(1, 10, MinimumIsExclusive = true)", "Range(1.5, 10)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [Range(1.5, 10)] requires Value >= 1.5, but the contract accepts Value > 1"
        },
        {
            ContractSource.ForParameter("", "MinLength(1)", "string"),
            "Parameter 'value' of 'Implementation.Accept(string)' strengthens the precondition of "
            + "'IContract.Accept(string)': [MinLength(1)] requires Length >= 1, but the contract has no lower bound "
            + "on Length"
        },
        {
            ContractSource.ForParameter("", "Required", "string"),
            "Parameter 'value' of 'Implementation.Accept(string)' strengthens the precondition of "
            + "'IContract.Accept(string)': [Required] rejects null, which the contract accepts"
        },
        {
            ContractSource.ForParameter("Required(AllowEmptyStrings = true)", "Required", "string"),
            "Parameter 'value' of 'Implementation.Accept(string)' strengthens the precondition of "
            + "'IContract.Accept(string)': [Required] rejects empty and whitespace-only strings, which the "
            + "contract accepts"
        },
        {
            ContractSource.ForParameter("", "AllowedValues(1, 2)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [AllowedValues(1, 2)] accepts only 1, 2, but the contract doesn't limit "
            + "Value to a list"
        },
        {
            ContractSource.ForParameter("AllowedValues(1, 2)", "AllowedValues(1)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [AllowedValues(1)] rejects 2, which the contract accepts"
        },
        {
            ContractSource.ForParameter("MinLength(1)", "", "int[]"),
            "Parameter 'value' of 'Implementation.Accept(int[])' doesn't restate [MinLength(1)] from "
            + "'IContract.Accept(int[])'. Restate it, or state a weaker constraint for each of its bounds to "
            + "accept more on purpose."
        },
        {
            ContractSource.ForProperty("Range(1, 10)", "get;", "Range(1, 20)", "get;"),
            "Property 'Implementation.Value' weakens the guarantee of 'IContract.Value': [Range(1, 10)] requires "
            + "Value <= 10, but the implementation accepts Value <= 20"
        },
        {
            ContractSource.ForProperty("Range(1, 10)", "get;", "", "get;"),
            "Property 'Implementation.Value' weakens the guarantee of 'IContract.Value': it doesn't restate "
            + "[Range(1, 10)]"
        },
        {
            ContractSource.ForParameter("MaxLength", "MaxLength(5)", "string"),
            "Parameter 'value' of 'Implementation.Accept(string)' strengthens the precondition of "
            + "'IContract.Accept(string)': [MaxLength(5)] requires Length <= 5, but the contract accepts "
            + "Length <= infinity"
        },
        {
            ContractSource.ForParameter("Range(1, 10)", "Range(1, 5)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [Range(1, 5)] requires Value <= 5, but the contract accepts Value <= 10"
        },
        {
            """
            public interface IGrid { int this[[Range(0, 9)] int index] { get; } }
            public class Grid : IGrid { public int this[[Range(0, 5)] int index] => index; }
            """,
            "Parameter 'index' of 'Grid.this[int]' strengthens the precondition of 'IGrid.this[int]': "
            + "[Range(0, 5)] requires Value <= 5, but the contract accepts Value <= 9"
        },
        {
            """
            public interface IGrid { [Range(0, 9)] int this[int index] { get; set; } }
            public class Grid : IGrid { [Range(0, 5)] public int this[int index] { get => 0; set { } } }
            """,
            "Indexer 'Grid.this[int]' strengthens the precondition of 'IGrid.this[int]': [Range(0, 5)] requires "
            + "Value <= 5, but the contract accepts Value <= 9"
        },
        {
            """
            public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
            public class Base { public int[] Multiply([MinLength(2)] int[] array) => array; }
            public class Derived : Base, IMultiplier { }
            """,
            "Parameter 'array' of 'Base.Multiply(int[])' (inherited by 'Derived') strengthens the precondition of "
            + "'IMultiplier.Multiply(int[])': [MinLength(2)] requires Length >= 2, but the contract accepts Length >= 1"
        },
        {
            ContractSource.ForParameter(
                "Range(typeof(System.DateTime), \"2020-01-01\", \"2030-12-31\", ParseLimitsInInvariantCulture = true)",
                "Range(typeof(System.DateTime), \"2021-01-01\", \"2030-12-31\", ParseLimitsInInvariantCulture = true)",
                "System.DateTime"
            ),
            "Parameter 'value' of 'Implementation.Accept(DateTime)' strengthens the precondition of "
            + "'IContract.Accept(DateTime)': [Range(typeof(System.DateTime), \"2021-01-01\", \"2030-12-31\", "
            + "ParseLimitsInInvariantCulture = true)] requires Value >= 2021-01-01, but the contract accepts "
            + "Value >= 2020-01-01"
        },
        {
            "[ComparableValidation(\"Parity\", ValidationComparison.Flag)]\n"
            + "public sealed class OddAttribute : ValidationAttribute { }\n"
            + ContractSource.ForParameter("", "Odd", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)' strengthens the precondition of "
            + "'IContract.Accept(int)': [Odd] imposes 'Parity', which the contract doesn't"
        },
        {
            "public sealed class EvenAttribute(int divisor) : ValidationAttribute { public int Divisor => divisor; }\n"
            + ContractSource.ForParameter("Even(2)", "Even(4)", "int"),
            "Parameter 'value' of 'Implementation.Accept(int)': [Even(4)] can't be compared with the attributes of "
            + "'IContract.Accept(int)'. Describe 'EvenAttribute' with [ComparableValidation], or repeat the contract's "
            + "attribute exactly."
        },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task Analyze_WhenAProblemIsReported_ExplainsIt(string source, string message)
    {
        // Arrange
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        Assert.Equal(message, Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeFromAPackageCannotBeCompared_SaysWhereItComesFrom()
    {
        // Arrange
        MetadataReference vendor = TestCompilation.CompileToImage(Vendor, "Vendor");
        DiagnosticMarkup markup = new(ContractSource.ForParameter("Vendor.Even(2)", "Vendor.Even(4)", "int"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, vendor);

        // Assert
        Assert.Equal(
            "Parameter 'value' of 'Implementation.Accept(int)': [Vendor.Even(4)] can't be compared with the "
            + "attributes of 'IContract.Accept(int)', because 'Vendor.EvenAttribute' comes from 'Vendor', outside "
            + "this solution. Repeat the contract's attribute exactly, or describe it with "
            + "[assembly: ComparableValidation(typeof(Vendor.EvenAttribute), ...)].",
            Assert.Single(diagnostics).GetMessage()
        );
    }

    [Fact]
    public async Task Analyze_WhenTheContractComesFromMetadata_ShowsItsAttributeAsCSharp()
    {
        // Arrange
        MetadataReference library = TestCompilation.CompileToImage(
            """
            namespace Library;
            public interface IContract { void Accept([Range(1, 10, MinimumIsExclusive = true)] int value); }
            """,
            "Library"
        );
        DiagnosticMarkup markup = new(
            "public class Implementation : Library.IContract { public void Accept(int value) { } }"
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, library);

        // Assert
        Assert.Contains(
            "doesn't restate [Range(1, 10, MinimumIsExclusive = true)]",
            Assert.Single(diagnostics).GetMessage(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeClassFromMetadataLacksTheSuffix_ShowsItsWholeName()
    {
        // Arrange
        MetadataReference library = TestCompilation.CompileToImage(
            """
            namespace Library;
            public sealed class Even : ValidationAttribute { }
            public interface IContract { void Accept([Even] int value); }
            """,
            "Library"
        );
        DiagnosticMarkup markup = new(
            "public class Implementation : Library.IContract { public void Accept(int value) { } }"
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, library);

        // Assert
        string message = Assert.Single(diagnostics).GetMessage();
        Assert.Contains("doesn't restate [Even]", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WhenAnAttributeWithoutArgumentsComesFromMetadata_ShowsItsBareName()
    {
        // Arrange
        MetadataReference library = TestCompilation.CompileToImage(
            "namespace Library;\npublic interface IContract { void Accept([Required] string value); }",
            "Library"
        );
        DiagnosticMarkup markup = new(
            "public class Implementation : Library.IContract { public void Accept(string value) { } }"
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, library);

        // Assert
        string message = Assert.Single(diagnostics).GetMessage();
        Assert.Contains("doesn't restate [Required]", message, StringComparison.Ordinal);
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ParameterValidation.Testing;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// Properties and indexers: a property's attributes are preconditions of its setter and guarantees
/// of its getter; an indexer's parameters are compared like a method's.
/// </summary>
public class PreconditionAnalyzerPropertyTest
{
    private const string Grid = """
        public interface IGrid
        {
            int this[[Range(0, 9)] int index] { get; }
        }

        """;

    [Theory]
    [InlineData("Range(1, 10)", "get; set;", "Range(1, 10)", "get; set;")]
    [InlineData("Range(1, 10)", "set;", "Range(0, 10)", "set { }")]
    [InlineData("Range(1, 10)", "get;", "Range(1, 5)", "get; set;")]
    public async Task Analyze_WhenAPropertyHonoursItsContract_ReportsNothing(
        string contract,
        string contractAccessors,
        string implementation,
        string implementationAccessors
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(
            ContractSource.ForProperty(contract, contractAccessors, implementation, implementationAccessors)
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("get; set;", "{|PV1001:Range(1, 5)|}", "get; set;")]
    [InlineData("get; init;", "{|PV1001:Range(2, 10)|}", "get; init;")]
    [InlineData("set;", "{|PV1001:Range(1, 5)|}", "set { }")]
    public async Task Analyze_WhenASetterIsStronger_ReportsPV1001(
        string contractAccessors,
        string implementation,
        string implementationAccessors
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(
            ContractSource.ForProperty("Range(1, 10)", contractAccessors, implementation, implementationAccessors)
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenASetOnlyPropertyDropsItsAttribute_ReportsPV1002()
    {
        // Arrange
        string source = ContractSource.ForProperty("Range(1, 10)", "set;", "", "set { }", "int", "{|PV1002:Value|}");
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenANullableSetOnlyPropertyDropsRequired_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForProperty("Required", "set;", "", "set { }", "string?"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenASettablePropertyAddsRequired_ReportsPV1001()
    {
        // Arrange
        string source = ContractSource.ForProperty("", "get; set;", "{|PV1001:Required|}", "get; set;", "string?");
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPropertyIsImplementedExplicitly_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IWidget { [Range(1, 10)] int Size { get; set; } }
            public class Widget : IWidget
            {
                [{|PV1001:Range(1, 5)|}] int IWidget.Size { get; set; }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPropertyOverridesAnAbstractProperty_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public abstract class Base { [Range(1, 10)] public abstract int Size { get; set; } }
            public class Derived : Base
            {
                [{|PV1001:Range(1, 5)|}] public override int Size { get; set; }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPropertyOverrideAlsoImplementsAnInterface_ReportsOnlyOnce()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IWidget { [Range(1, 10)] int Size { get; set; } }
            public class Widget : IWidget
            {
                [Range(1, 10)] public virtual int Size { get; set; }
            }
            public class Derived : Widget
            {
                [{|PV1001:Range(1, 5)|}] public override int Size { get; set; }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPropertyIsInheritedFromAnUnrelatedBase_ReportsOnTheClass()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IWidget { [Range(1, 10)] int Size { get; set; } }
            public class Base
            {
                [Range(1, 5)] public int Size { get; set; }
            }
            public class {|PV1001:Derived|} : Base, IWidget { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAPropertyImplementsNothing_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IWidget { [Range(1, 10)] int Size { get; set; } }
            public class Widget
            {
                [Range(1, 5)] public int Size { get; set; }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("Range(0, 9)", "index")]
    [InlineData("{|PV1001:Range(0, 5)|}", "index")]
    [InlineData("", "{|PV1002:index|}")]
    public async Task Analyze_WhenAnIndexerImplementsAnInterface_ComparesItsParameters(
        string implementation,
        string parameter
    )
    {
        // Arrange
        string attributes = implementation.Length == 0
            ? string.Empty
            : $"[{implementation}] ";
        DiagnosticMarkup markup = new(Grid + $$"""
            public class Grid : IGrid
            {
                public int this[{{attributes}}int {{parameter}}] => index;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenASettableIndexerParameterIsStronger_ReportsOnlyOnce()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IGrid { int this[[Range(0, 9)] int index] { get; set; } }
            public class Grid : IGrid
            {
                public int this[[{|PV1001:Range(0, 5)|}] int index] { get => index; set { } }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnIndexerIsImplementedExplicitly_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new(Grid + """
            public class Grid : IGrid
            {
                int IGrid.this[[{|PV1001:Range(1, 9)|}] int index] => index;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnIndexerOverridesAnAbstractIndexer_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public abstract class Base { public abstract int this[[MinLength(1)] string key] { get; } }
            public class Derived : Base
            {
                public override int this[[{|PV1001:MinLength(2)|}] string key] => 0;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenASettableIndexerValueIsStronger_ReportsPV1001()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IGrid { [Range(0, 100)] int this[int index] { get; set; } }
            public class Grid : IGrid
            {
                [{|PV1001:Range(0, 50)|}] public int this[int index] { get => 0; set { } }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnIndexerIsInheritedFromAnUnrelatedBase_ReportsOnTheClass()
    {
        // Arrange
        const string source = Grid + """
            public class Base
            {
                public int this[[Range(0, 5)] int index] => index;
            }
            public class {|PV1001:Derived|} : Base, IGrid { }
            """;
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }
}

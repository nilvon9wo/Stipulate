using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers.Test;

/// <summary>
/// The analyzer runs live in the IDE, on code that doesn't compile yet: it must neither fail nor
/// report anything that isn't so.
/// </summary>
public class PreconditionAnalyzerIncompleteCodeTest
{
    [Fact]
    public async Task Analyze_WhenAnAttributeDoesNotBind_ReportsNothing()
    {
        // Arrange
        CSharpCompilation compilation = Compile("""
            public interface IContract { void Accept([Unknown] int value); }
            public class Implementation : IContract { public void Accept([Unknown] int value) { } }
            [Unknown]
            public class Other { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(compilation);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnAttributesConstructorDoesNotBind_CannotCompareIt()
    {
        // Arrange
        CSharpCompilation compilation = Compile("""
            public interface IContract { void Accept([MinLength(1)] int[] value); }
            public class Implementation : IContract { public void Accept([MinLength("one")] int[] value) { } }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(compilation);

        // Assert
        Assert.Equal(DiagnosticIds.NotComparableExternal, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenAClassDoesNotYetImplementItsInterface_ReportsNothing()
    {
        // Arrange
        CSharpCompilation compilation = Compile("""
            public interface IContract { void Accept([MinLength(1)] int[] value); }
            public class Implementation : IContract { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(compilation);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(
        "public class Shape { [ComparableValidation(\"Shape\", ValidationComparison.Flag)] public void Draw() { } }")]
    [InlineData("[return: ComparableValidation(\"Shape\", ValidationComparison.Flag)] public class Shape { }")]
    [InlineData("[ComparableValidation(\"Shape\", ValidationComparison.Flag)] public enum Shape { }")]
    public async Task Analyze_WhenAMarkerIsWhereItCannotBe_ReportsNothing(string source)
    {
        // Arrange
        CSharpCompilation compilation = Compile(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(compilation);

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>Compiles source that the compiler complains about.</summary>
    private static CSharpCompilation Compile(string source)
    {
        CSharpCompilation compilation = TestCompilation.Compile(TestCompilation.Usings + source);
        Assert.Contains(
            compilation.GetDiagnostics(),
            static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning
        );
        return compilation;
    }
}

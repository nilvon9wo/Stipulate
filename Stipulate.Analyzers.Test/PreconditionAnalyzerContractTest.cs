using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// Which members are compared with a contract: implementations of interface members, however they
/// implement them, and overrides; and where the comparison is reported.
/// </summary>
public class PreconditionAnalyzerContractTest
{
    private const string Multiplier = """
        public interface IMultiplier
        {
            int[] Multiply([MinLength(1)] int[] array);
        }

        """;

    [Fact]
    public async Task Analyze_WhenAMethodImplementsAnInterfaceImplicitly_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Multiplier : IMultiplier
            {
                public int[] Multiply([{|ST1001:MinLength(2)|}] int[] array) => array;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheParametersAreRenamed_MatchesThemByPosition()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Multiplier : IMultiplier
            {
                public int[] Multiply([{|ST1001:MinLength(2)|}] int[] values) => values;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAMethodImplementsAnInterfaceExplicitly_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Multiplier : IMultiplier
            {
                int[] IMultiplier.Multiply([{|ST1001:MinLength(2)|}] int[] array) => array;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAStaticMethodImplementsAStaticAbstractMember_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IParser { static abstract int Parse([MinLength(1)] string text); }
            public class Parser : IParser
            {
                public static int Parse([{|ST1001:MinLength(2)|}] string text) => 0;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheInterfaceIsGeneric_ComparesTheConstructedMember()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface IStore<T> { void Add([Required] T item); }
            public class Store : IStore<string>
            {
                public void Add(string {|ST1002:item|}) { }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAMethodOverridesAnAbstractMethod_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public abstract class Base { public abstract void Add([MinLength(1)] int[] items); }
            public class Derived : Base
            {
                public override void Add([{|ST1001:MinLength(2)|}] int[] items) { }
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnOverrideAlsoImplementsAnInterface_ReportsOnlyOnce()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Multiplier : IMultiplier
            {
                public virtual int[] Multiply([MinLength(1)] int[] array) => array;
            }
            public class Derived : Multiplier
            {
                public override int[] Multiply([{|ST1001:MinLength(2)|}] int[] array) => array;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheImplementationIsInheritedFromAnUnrelatedBase_ReportsOnTheClass()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Base
            {
                public int[] Multiply([MinLength(2)] int[] array) => array;
            }
            public class {|ST1001:Derived|} : Base, IMultiplier { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheImplementationIsInheritedFromAnImplementingBase_ReportsOnTheBase()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Base : IMultiplier
            {
                public int[] Multiply([{|ST1001:MinLength(2)|}] int[] array) => array;
            }
            public class Derived : Base, IMultiplier { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADefaultInterfaceMemberImplementsAnotherInterface_ComparesItOnlyWhereDeclared()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public interface IDoubler : IMultiplier
            {
                int[] IMultiplier.Multiply(int[] {|ST1002:array|}) => array;
            }
            public class Doubler : IDoubler { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAMethodImplementsNothing_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Multiplier
            {
                public int[] Multiply([MinLength(2)] int[] array) => array;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAStructImplementsAnInterface_ComparesIt()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public struct Multiplier : IMultiplier
            {
                public int[] Multiply([{|ST1001:MinLength(2)|}] int[] array) => array;
            }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnInterfaceHasAnEvent_IgnoresIt()
    {
        // Arrange
        DiagnosticMarkup markup = new("""
            public interface INotifier { event System.EventHandler Changed; }
            public class Notifier : INotifier { public event System.EventHandler? Changed; }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheImplementationIsInheritedFromAPackage_ReportsOnTheClass()
    {
        // Arrange
        MetadataReference library = TestCompilation.CompileToImage(
            "namespace Library;\npublic class Base { public int[] Multiply([MinLength(2)] int[] array) => array; }",
            "Library"
        );
        DiagnosticMarkup markup = new(Multiplier + "public class {|ST1001:Derived|} : Library.Base, IMultiplier { }");

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup, library);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnInheritedImplementationDropsAnAttribute_ReportsST1002OnTheClass()
    {
        // Arrange
        DiagnosticMarkup markup = new(Multiplier + """
            public class Base
            {
                public int[] Multiply(int[] array) => array;
            }
            public class {|ST1002:Derived|} : Base, IMultiplier { }
            """);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }
}

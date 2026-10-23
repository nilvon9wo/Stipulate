using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Net.NowhereAtAll.ParameterValidation.Analyzers;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.CodeFixes.Test;

/// <summary>The code fix that restates a contract's missing attribute, one at a time or all at once.</summary>
public class RestateAttributeCodeFixTest
{
    private const string Usings = "using System.ComponentModel.DataAnnotations;\n\n";

    /// <summary>The analyzer's PV1002, for diagnostics a test builds by hand.</summary>
    private static readonly DiagnosticDescriptor Dropped = new PreconditionAnalyzer().SupportedDiagnostics
        .Single(static descriptor => descriptor.Id == DiagnosticIds.PreconditionDropped);

    private const string Multiplier = Usings + """
        public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
        public class Multiplier : IMultiplier
        {
            public int[] Multiply(int[] array) => array;
        }
        """;

    [Fact]
    public async Task RegisterCodeFixes_WhenAParameterDropsAnAttribute_RestatesIt()
    {
        // Arrange
        string expected = Multiplier.Replace(
            "Multiply(int[] array) =>",
            "Multiply([MinLength(1)] int[] array) =>",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(Multiplier);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenADiagnosticSaysWhatToRestate_TitlesTheFixWithTheAttribute()
    {
        // Arrange
        Document document = CodeFixRunner.CreateDocument(Multiplier);
        ImmutableArray<Diagnostic> fixable = await CodeFixRunner.GetFixableAsync(document);

        // Act
        ImmutableArray<CodeAction> actions = await CodeFixRunner.RegisterAsync(document, fixable);

        // Assert
        Assert.Equal("Restate [MinLength(1)] from the contract", Assert.Single(actions).Title);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheUsingIsMissing_AddsIt()
    {
        // Arrange
        const string source = """
            public interface IScale { void Set([System.ComponentModel.DataAnnotations.Range(1, 10)] int level); }
            public class Scale : IScale
            {
                public void Set(int level) { }
            }
            """;
        const string expected = Usings + """
            public interface IScale { void Set([System.ComponentModel.DataAnnotations.Range(1, 10)] int level); }
            public class Scale : IScale
            {
                public void Set([Range(1, 10)] int level) { }
            }
            """;

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenAGetterDropsAnAttribute_RestatesItOnTheProperty()
    {
        // Arrange
        const string source = Usings + """
            public interface ISized { [Range(1, 10)] int Size { get; } }
            public class Sized : ISized
            {
                public int Size => 3;
            }
            """;
        const string expected = Usings + """
            public interface ISized { [Range(1, 10)] int Size { get; } }
            public class Sized : ISized
            {
                [Range(1, 10)]
                public int Size => 3;
            }
            """;

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheAttributeHasTypesAndNamedArguments_RestatesThemExactly()
    {
        // Arrange
        const string range =
            "Range(typeof(DateTime), \"2020-01-01\", \"2030-12-31\", ParseLimitsInInvariantCulture = true)";
        string source = $$"""
            using System;
            {{Usings}}public interface IDiary { void Write([{{range}}] DateTime when); }
            public class Diary : IDiary
            {
                public void Write(DateTime when) { }
            }
            """;
        string expected = source.Replace(
            "Write(DateTime when) {",
            $"Write([{range}] DateTime when) {{",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheContractIsGeneric_RestatesItsAttribute()
    {
        // Arrange
        const string source = Usings + """
            public interface IStore<T> { void Add([Required] T item); }
            public class Store : IStore<string>
            {
                public void Add(string item) { }
            }
            """;
        string expected = source.Replace("Add(string item)", "Add([Required] string item)", StringComparison.Ordinal);

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheContractIsInAReferencedAssembly_RestatesItsAttribute()
    {
        // Arrange
        MetadataReference library = TestCompilation.CompileToImage(
            "namespace Library;\npublic interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }",
            "Library"
        );
        const string source = """
            public class Multiplier : Library.IMultiplier
            {
                public int[] Multiply(int[] array) => array;
            }
            """;
        const string expected = Usings + """
            public class Multiplier : Library.IMultiplier
            {
                public int[] Multiply([MinLength(1)] int[] array) => array;
            }
            """;

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source, library);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheDeclarationHasAValidationList_JoinsIt()
    {
        // Arrange
        const string source = Usings + """
            public interface INamer { void Name([Required, MinLength(1), MaxLength(10)] string name); }
            public class Namer : INamer
            {
                public void Name([Required] string name) { }
            }
            """;
        string expected = source.Replace(
            "Name([Required] string name) {",
            "Name([Required, MinLength(1), MaxLength(10)] string name) {",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheDeclarationHasOnlyOtherLists_AddsAListOfItsOwn()
    {
        // Arrange
        const string source = "using System.ComponentModel;\n" + Usings + """
            public interface INamer { void Name([Required] string name); }
            public class Namer : INamer
            {
                public void Name([Description("The name")] string name) { }
            }
            """;
        string expected = source.Replace(
            "Name([Description(\"The name\")] string name)",
            "Name([Description(\"The name\")][Required] string name)",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenAnIndexerParameterDropsAnAttribute_RestatesIt()
    {
        // Arrange
        const string source = Usings + """
            public interface IGrid { int this[[Range(0, 9)] int index] { get; } }
            public class Grid : IGrid
            {
                public int this[int index] => index;
            }
            """;
        string expected = source.Replace(
            "public int this[int index]",
            "public int this[[Range(0, 9)] int index]",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheValidationListHasATarget_AddsAListOfItsOwn()
    {
        // Arrange
        const string source = Usings + """
            public interface INamer { void Name([Required, MinLength(1)] string name); }
            public class Namer : INamer
            {
                public void Name([param: Required] string name) { }
            }
            """;
        string expected = source.Replace(
            "Name([param: Required] string name) {",
            "Name([param: Required][MinLength(1)] string name) {",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenAListHoldsAnAttributeThatDoesNotBind_AddsAListOfItsOwn()
    {
        // Arrange
        const string source = Usings + """
            public interface INamer { void Name([Required] string name); }
            public class Namer : INamer
            {
                public void Name([Unknown] string name) { }
            }
            """;
        string expected = source.Replace(
            "Name([Unknown] string name)",
            "Name([Unknown][Required] string name)",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        Assert.Equal(expected.ReplaceLineEndings(), result.Text);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenTheImplementationIsInherited_OffersNoFix()
    {
        // Arrange
        const string source = Usings + """
            public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
            public class Base
            {
                public int[] Multiply(int[] array) => array;
            }
            public class Derived : Base, IMultiplier { }
            """;

        // Act
        FixResult result = await CodeFixRunner.FixEachAsync(source);

        // Assert
        result.AssertFixedTo(source);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenADiagnosticDoesNotSayWhatToRestate_OffersNoFix()
    {
        // Arrange
        Document document = CodeFixRunner.CreateDocument(Multiplier);
        Diagnostic diagnostic = Diagnostic.Create(Dropped, Location.None, "x", "y", "z");

        // Act
        ImmutableArray<CodeAction> actions = await CodeFixRunner.RegisterAsync(document, [diagnostic]);

        // Assert
        Assert.Empty(actions);
    }

    [Theory]
    [InlineData("M:Nowhere.Nothing(System.Int32)", "0", "0")]
    [InlineData("M:IMultiplier.Multiply(System.Int32[])", "1", "0")]
    [InlineData("M:IMultiplier.Multiply(System.Int32[])", "x", "0")]
    [InlineData("M:IMultiplier.Multiply(System.Int32[])", "0", "1")]
    [InlineData("M:IMultiplier.Multiply(System.Int32[])", "0", "x")]
    [InlineData("M:IMultiplier.Multiply(System.Int32[])", "value", "0")]
    [InlineData(null, "0", "0")]
    public async Task RegisterCodeFixes_WhenADiagnosticPointsToNoAttribute_LeavesTheDocumentUnchanged(
        string? contract,
        string target,
        string index
    )
    {
        // Arrange
        Document document = CodeFixRunner.CreateDocument(Multiplier);
        ImmutableArray<Diagnostic> fixable = await CodeFixRunner.GetFixableAsync(document);
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(RestateProperties.Contract, contract)
            .Add(RestateProperties.Target, target)
            .Add(RestateProperties.AttributeIndex, index)
            .Add(RestateProperties.AttributeDisplay, "MinLength(1)");
        Diagnostic diagnostic = Diagnostic.Create(Dropped, fixable[0].Location, properties, "x", "y", "z");
        ImmutableArray<CodeAction> actions = await CodeFixRunner.RegisterAsync(document, [diagnostic]);

        // Act
        Document fixedDocument = await CodeFixRunner.ApplyAsync(document, Assert.Single(actions));

        // Assert
        Assert.Same(document, fixedDocument);
    }

    [Fact]
    public async Task RegisterCodeFixes_WhenADiagnosticIsNotOnADeclaration_LeavesTheDocumentUnchanged()
    {
        // Arrange
        Document document = CodeFixRunner.CreateDocument(Multiplier);
        ImmutableArray<Diagnostic> fixable = await CodeFixRunner.GetFixableAsync(document);
        Diagnostic diagnostic = Diagnostic.Create(
            Dropped,
            Location.Create(fixable[0].Location.SourceTree!, new TextSpan(0, 0)),
            fixable[0].Properties,
            "x",
            "y",
            "z"
        );
        ImmutableArray<CodeAction> actions = await CodeFixRunner.RegisterAsync(document, [diagnostic]);

        // Act
        Document fixedDocument = await CodeFixRunner.ApplyAsync(document, Assert.Single(actions));

        // Assert
        Assert.Same(document, fixedDocument);
    }

    [Fact]
    public async Task FixAll_WhenSeveralAttributesAreMissing_RestatesThemInTheContractsOrder()
    {
        // Arrange
        const string source = Usings + """
            public interface INamer { void Name([Required, MinLength(1)] string first, [MaxLength(10)] string last); }
            public class Namer : INamer
            {
                public void Name(string first, string last) { }
            }
            """;
        string expected = source.Replace(
            "Name(string first, string last) {",
            "Name([Required, MinLength(1)] string first, [MaxLength(10)] string last) {",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixAllAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public async Task FixAll_WhenAPropertyHasAValidationList_JoinsIt()
    {
        // Arrange
        const string source = Usings + """
            public interface INamed { [Required, MinLength(1)] string Name { get; } }
            public class Named : INamed
            {
                [Required]
                public string Name => "x";
            }
            """;
        string expected = source.Replace(
            "    [Required]\n    public",
            "    [Required, MinLength(1)]\n    public",
            StringComparison.Ordinal
        );

        // Act
        FixResult result = await CodeFixRunner.FixAllAsync(source);

        // Assert
        result.AssertFixedTo(expected);
    }

    [Fact]
    public void GetFixAllProvider_WhenAsked_FixesWholeDocuments()
    {
        // Arrange
        RestateAttributeCodeFix fix = new();

        // Act
        IEnumerable<FixAllScope> scopes = fix.GetFixAllProvider().GetSupportedFixAllScopes();

        // Assert
        Assert.Contains(FixAllScope.Document, scopes);
    }
}

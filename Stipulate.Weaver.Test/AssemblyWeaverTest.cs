using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>Weaving one assembly on disk: which methods, how often, and what happens to its symbols.</summary>
public class AssemblyWeaverTest
{
    private const string TwoChecked = """
        public static class Target
        {
            public static void First([Required] string? text) { }

            public static void Second([Range(1, 10)] int number) { }

            public static void Unchecked(string? text) { }
        }
        """;

    /// <summary>A custom validation attribute in an assembly of its own, as a package would supply it.</summary>
    private const string Library = """
        public sealed class PositiveAttribute : ValidationAttribute
        {
            public override bool IsValid(object? value) => (int)value! > 0;
        }
        """;

    private const string UsesLibrary = """
        public static class Target
        {
            public static void Check([Positive] int number) { }
        }
        """;

    [Fact]
    public void Weave_WhenMethodsHaveChecks_ReturnsHowManyItWove()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);

        // Act
        int? wovenCount = directory.Weave();

        // Assert
        Assert.Equal(2, wovenCount);
    }

    [Fact]
    public void Weave_WhenAMethodHasNoBody_SkipsIt()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile("""
            public interface IShape { void Draw([Required] string? name); }
            public abstract class Shape { public abstract void Draw([Required] string? name); }
            """);

        // Act
        int? wovenCount = directory.Weave();

        // Assert
        Assert.Equal(0, wovenCount);
    }

    [Fact]
    public void Weave_WhenTheAssemblyIsAlreadyWoven_ReturnsNull()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);
        _ = directory.Weave();

        // Act
        int? wovenCount = directory.Weave();

        // Assert
        Assert.Null(wovenCount);
    }

    [Fact]
    public void Weave_WhenTheAssemblyIsAlreadyWoven_LeavesItsFilesUnchanged()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);
        _ = directory.Weave();
        byte[] assembly = File.ReadAllBytes(directory.AssemblyPath);
        byte[] symbols = File.ReadAllBytes(directory.SymbolsPath);

        // Act
        _ = directory.Weave();

        // Assert
        Assert.Equal(assembly, File.ReadAllBytes(directory.AssemblyPath));
        Assert.Equal(symbols, File.ReadAllBytes(directory.SymbolsPath));
    }

    [Fact]
    public void Weave_WhenNothingNeedsChecking_StillMarksTheAssemblyAsWoven()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile("public static class Target { public static void Run(string? text) { } }");
        int? firstCount = directory.Weave();

        // Act
        int? secondCount = directory.Weave();

        // Assert
        Assert.Equal(0, firstCount);
        Assert.Null(secondCount);
    }

    [Fact]
    public void Weave_WhenTheAssemblyHasSymbols_RewritesThemToMatch()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);

        // Act
        _ = directory.Weave();

        // Assert - reading symbols that don't match the assembly throws
        using ModuleDefinition module = ModuleDefinition.ReadModule(
            directory.AssemblyPath,
            new ReaderParameters { ReadSymbols = true, InMemory = true }
        );
        Assert.True(module.HasSymbols);
    }

    [Fact]
    public void Weave_WhenTheAssemblyHasNoSymbols_WritesNone()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);
        File.Delete(directory.SymbolsPath);

        // Act
        int? wovenCount = directory.Weave();

        // Assert
        Assert.Equal(2, wovenCount);
        Assert.False(File.Exists(directory.SymbolsPath));
    }

    [Fact]
    public void Weave_WhenTheSymbolsDoNotMatch_WarnsAndLeavesThemUnchanged()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);
        directory.WriteMismatchedSymbols("public static class Other { }");
        byte[] symbols = File.ReadAllBytes(directory.SymbolsPath);
        using StringWriter output = new();

        // Act
        int? wovenCount = directory.Weave(output);

        // Assert
        Assert.Equal(2, wovenCount);
        Assert.Equal(symbols, File.ReadAllBytes(directory.SymbolsPath));
        Assert.Equal(
            $"{directory.SymbolsPath} : warning ST0006: The symbols don't match the assembly, so they were left "
            + "unchanged; debugging the woven code may not work.",
            output.ToString().TrimEnd()
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Weave_WhenItIsDone_HoldsNoFileOpen(bool areSymbolsMismatched)
    {
        // Arrange - Windows can't replace or delete a file while anything holds it open
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(TwoChecked);
        if (areSymbolsMismatched)
        {
            directory.WriteMismatchedSymbols("public static class Other { }");
        }

        // Act
        _ = directory.Weave();

        // Assert - opening a file for exclusive use fails while it is still held, on Linux as well
        using FileStream assembly = OpenExclusively(directory.AssemblyPath);
        using FileStream symbols = OpenExclusively(directory.SymbolsPath);
        Assert.True(assembly.CanWrite && symbols.CanWrite);
    }

    [Fact]
    public void Weave_WhenAnAttributeComesFromAReferencedAssembly_ChecksIt()
    {
        // Arrange - the library is found through the references file, in a directory of its own
        using WeavingDirectory directory = WeavingDirectory.Create();
        string libraryPath = WriteLibrary(directory, Library);
        directory.Compile(UsesLibrary, MetadataReference.CreateFromFile(libraryPath));
        _ = directory.Weave();
        Assembly woven = directory.Load(libraryPath);

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(woven, "Target", "Check", 0)
        );

        // Assert
        Assert.Equal("number", exception.ParamName);
    }

    [Fact]
    public void Weave_WhenAnAttributesAssemblyCannotBeFound_ThrowsWeavingException()
    {
        // Arrange - the library exists only in memory, so its line in the references file is blank
        using WeavingDirectory directory = WeavingDirectory.Create();
        MetadataReference library = TestCompilation.CompileToImage(Library, "Library");
        directory.Compile(UsesLibrary, library);

        // Act
        WeavingException exception = Assert.Throws<WeavingException>(() => directory.Weave());

        // Assert
        Assert.Equal(WeavingErrorCodes.WeavingFailed, exception.Code);
        Assert.Equal(
            "Could not resolve type 'PositiveAttribute': its assembly "
            + "'Library, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' is not among the references.",
            exception.Message
        );
    }

    [Fact]
    public void Weave_WhenAnAttributesAssemblyNoLongerDeclaresIt_ThrowsWeavingException()
    {
        // Arrange - compiled against one build of the library, woven against a later one
        using WeavingDirectory directory = WeavingDirectory.Create();
        string libraryPath = WriteLibrary(directory, Library);
        byte[] image = File.ReadAllBytes(libraryPath);
        MetadataReference library = MetadataReference.CreateFromImage(image, filePath: libraryPath);
        directory.Compile(UsesLibrary, library);
        _ = WriteLibrary(directory, "public sealed class Unrelated { }");

        // Act
        WeavingException exception = Assert.Throws<WeavingException>(() => directory.Weave());

        // Assert
        Assert.Equal(WeavingErrorCodes.WeavingFailed, exception.Code);
        Assert.Equal("Could not resolve type 'PositiveAttribute' from 'Library'.", exception.Message);
    }

    [Fact]
    public void Weave_WhenTheAssemblyIsMissing_ThrowsWithoutRetrying()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        AssemblyWeaver weaver = new(TextWriter.Null, new FileRetry(2, TimeSpan.Zero, TextWriter.Null, Unexpected));
        string assemblyPath = directory.AssemblyPath;

        // Act
        Exception exception = Record.Exception(() => weaver.Weave(assemblyPath, []))!;

        // Assert
        _ = Assert.IsType<FileNotFoundException>(exception);
    }

    /// <returns>Where the library was written, in a directory of its own.</returns>
    private static string WriteLibrary(WeavingDirectory directory, string source)
    {
        string libraryDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "lib")).FullName;
        string libraryPath = Path.Combine(libraryDirectory, "Library.dll");
        CSharpCompilation compilation = TestCompilation.CompileCleanly(TestCompilation.Usings + source, "Library");
        File.WriteAllBytes(libraryPath, [.. TestCompilation.Emit(compilation)]);
        return libraryPath;
    }

    private static FileStream OpenExclusively(string path)
        => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    private static void Unexpected(TimeSpan delay) => Assert.Fail($"Paused for {delay}, so it retried.");
}

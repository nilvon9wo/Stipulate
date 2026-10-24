using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// A temporary directory holding what MSBuild hands the weaver: a freshly compiled assembly, its
/// symbols and the file listing its references.
/// </summary>
internal sealed class WeavingDirectory : IDisposable
{
    private const string DirectoryPrefix = "Stipulate.Weaver.Test.";
    private const string AssemblyFileName = TestCompilation.AssemblyName + ".dll";
    private const string SymbolsExtension = ".pdb";
    private const string ReferencesFileName = "references.txt";

    private WeavingDirectory(string path) => this.Path = path;

    public string Path { get; }

    public string AssemblyPath => System.IO.Path.Combine(this.Path, AssemblyFileName);

    public string SymbolsPath => System.IO.Path.ChangeExtension(this.AssemblyPath, SymbolsExtension);

    public string ReferencesPath => System.IO.Path.Combine(this.Path, ReferencesFileName);

    public static WeavingDirectory Create() => new(Directory.CreateTempSubdirectory(DirectoryPrefix).FullName);

    /// <summary>Compiles, weaves and loads <paramref name="source"/>, leaving nothing on disk.</summary>
    /// <param name="source">The assembly's source, after <see cref="TestCompilation.Usings"/>.</param>
    public static Assembly WeaveAndLoad([StringSyntax("C#")] string source)
    {
        using WeavingDirectory directory = Create();
        directory.Compile(source);
        _ = directory.Weave();
        return directory.Load();
    }

    /// <summary>
    /// Compiles the assembly and its symbols into this directory and lists its references, the way
    /// the build does before it runs the weaver.
    /// </summary>
    /// <param name="source">The assembly's source, after <see cref="TestCompilation.Usings"/>.</param>
    /// <param name="references">
    /// Assemblies the source references, beyond the platform's; one with no file is listed as a blank line.
    /// </param>
    public void Compile([StringSyntax("C#")] string source, params MetadataReference[] references)
        => this.Compile(source, hasChecks: true, references);

    /// <summary>
    /// Compiles the assembly and its symbols into this directory and lists its references, the way
    /// the build does before it runs the weaver.
    /// </summary>
    /// <param name="source">The assembly's source, after <see cref="TestCompilation.Usings"/>.</param>
    /// <param name="hasChecks">
    /// Whether to compile in the run-time check, as Stipulate.targets does for every project it weaves.
    /// </param>
    /// <param name="references">
    /// Assemblies the source references, beyond the platform's; one with no file is listed as a blank line.
    /// </param>
    public void Compile([StringSyntax("C#")] string source, bool hasChecks, params MetadataReference[] references)
    {
        CSharpCompilation written = TestCompilation.CompileCleanly(
            TestCompilation.Usings + source,
            references: references
        );
        CSharpCompilation compilation = hasChecks
            ? written.AddSyntaxTrees(TestCompilation.ArgumentChecks)
            : written;
        this.Emit(compilation, this.SymbolsPath);
        IEnumerable<string> referencePaths = TestCompilation.References
            .Concat(references)
            .OfType<PortableExecutableReference>()
            .Select(static reference => reference.FilePath ?? string.Empty);
        File.WriteAllLines(this.ReferencesPath, referencePaths);
    }

    /// <summary>Compiles other source and writes only its symbols, which then don't match the assembly.</summary>
    public void WriteMismatchedSymbols([StringSyntax("C#")] string source)
    {
        CSharpCompilation compilation = TestCompilation.CompileCleanly(TestCompilation.Usings + source);
        string assemblyPath = System.IO.Path.Combine(this.Path, "Other.dll");
        Emit(compilation, assemblyPath, this.SymbolsPath);
    }

    /// <summary>Runs the weaver, as the build does, on the compiled assembly.</summary>
    /// <param name="output">Where the weaver's warnings go; nowhere if <c>null</c>.</param>
    public int? Weave(TextWriter? output = null)
    {
        TextWriter writer = output ?? TextWriter.Null;
        AssemblyWeaver weaver = new(writer, FileRetry.Create(writer));
        return weaver.Weave(this.AssemblyPath, File.ReadAllLines(this.ReferencesPath));
    }

    /// <summary>Loads the assembly from memory, so the file stays free to delete.</summary>
    /// <param name="dependencyPaths">Assemblies it references beyond the platform's, loaded alongside it.</param>
    public Assembly Load(params string[] dependencyPaths)
    {
        AssemblyLoadContext context = new(this.Path, isCollectible: true);
        foreach (string dependencyPath in dependencyPaths)
        {
            _ = LoadFromMemory(context, dependencyPath);
        }

        return LoadFromMemory(context, this.AssemblyPath);
    }

    public void Dispose() => Directory.Delete(this.Path, recursive: true);

    private static Assembly LoadFromMemory(AssemblyLoadContext context, string path)
    {
        using MemoryStream image = new(File.ReadAllBytes(path));
        return context.LoadFromStream(image);
    }

    private void Emit(Compilation compilation, string symbolsPath)
        => Emit(compilation, this.AssemblyPath, symbolsPath);

    private static void Emit(Compilation compilation, string assemblyPath, string symbolsPath)
    {
        using FileStream assembly = File.Create(assemblyPath);
        using FileStream symbols = File.Create(symbolsPath);
        EmitOptions options = new(debugInformationFormat: DebugInformationFormat.PortablePdb);
        EmitResult result = compilation.Emit(
            assembly,
            symbols,
            options: options,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }
}

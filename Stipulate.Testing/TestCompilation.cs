using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Net.NowhereAtAll.Stipulate.Contracts;

namespace Net.NowhereAtAll.Stipulate.Testing;

/// <summary>Compiles C# source in memory, against the running .NET and Stipulate.Contracts.</summary>
public static class TestCompilation
{
    public const string AssemblyName = "Test";

    /// <summary>The usings every analyzer test source starts with.</summary>
    public const string Usings = """
        using System.ComponentModel.DataAnnotations;
        using Net.NowhereAtAll.Stipulate.Contracts;

        """;

    private const string TrustedPlatformAssemblies = "TRUSTED_PLATFORM_ASSEMBLIES";
    private const string ToolAssemblyPrefix = "Net.NowhereAtAll.Stipulate.";

    /// <summary>
    /// The running .NET's framework assemblies plus Stipulate.Contracts. The trusted
    /// platform assemblies only sometimes include the test's own dependencies (it depends on the
    /// test host), so Contracts is added explicitly, and every other Stipulate assembly
    /// left out.
    /// </summary>
    private static readonly Lazy<ImmutableArray<MetadataReference>> PlatformReferences = new(LoadPlatformReferences);

    public static ImmutableArray<MetadataReference> References => PlatformReferences.Value;

    /// <summary>
    /// The run-time check, Stipulate.ArgumentChecks.cs, that Stipulate.targets compiles into every
    /// project that enforces the attributes: the woven methods and the interceptors call it.
    /// </summary>
    public static string ArgumentChecksSource { get; } = ReadArgumentChecks();

    /// <summary><see cref="ArgumentChecksSource"/>, parsed as the test sources are.</summary>
    public static SyntaxTree ArgumentChecks => ParseArgumentChecks(ParseOptions);

    /// <summary>
    /// <see cref="ArgumentChecksSource"/>, parsed with the options of the compilation it joins, which
    /// all its trees must share; with an encoding, so that its symbols can be emitted.
    /// </summary>
    public static SyntaxTree ParseArgumentChecks(CSharpParseOptions options)
        => CSharpSyntaxTree.ParseText(
            SourceText.From(ArgumentChecksSource, Encoding.UTF8),
            options,
            path: "Stipulate.ArgumentChecks.cs",
            cancellationToken: CancellationToken
        );

    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Latest);

    public static CSharpCompilationOptions CompilationOptions { get; } =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

    public static CSharpCompilation Compile(
        [StringSyntax("C#")] string source,
        string assemblyName = AssemblyName,
        params MetadataReference[] references
    )
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, ParseOptions, cancellationToken: CancellationToken);
        return CSharpCompilation.Create(assemblyName, [tree], [.. References, .. references], CompilationOptions);
    }

    /// <summary>Compiles source that must compile without errors.</summary>
    public static CSharpCompilation CompileCleanly(
        [StringSyntax("C#")] string source,
        string assemblyName = AssemblyName,
        params MetadataReference[] references
    )
    {
        CSharpCompilation compilation = Compile(source, assemblyName, references);
        AssertCompiles(compilation);
        return compilation;
    }

    /// <summary>Compiles another assembly, e.g. a package's, for the test source to reference.</summary>
    /// <param name="source">The assembly's source, after <see cref="Usings"/>.</param>
    /// <param name="assemblyName">The assembly's name.</param>
    /// <param name="references">Assemblies the source references, beyond the platform's.</param>
    /// <param name="filePath">Where the reference claims to have been loaded from; <c>null</c> for nowhere.</param>
    public static MetadataReference CompileToImage(
        [StringSyntax("C#")] string source,
        string assemblyName,
        string? filePath = null,
        params MetadataReference[] references
    )
    {
        ImmutableArray<byte> image = Emit(Compile(Usings + source, assemblyName, references));
        return MetadataReference.CreateFromImage(image, filePath: filePath);
    }

    public static ImmutableArray<byte> Emit(Compilation compilation)
    {
        using MemoryStream stream = new();
        EmitResult result = compilation.Emit(stream, cancellationToken: CancellationToken);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return [.. stream.ToArray()];
    }

    public static void AssertCompiles(Compilation compilation)
    {
        Diagnostic[] errors = [.. compilation.GetDiagnostics(CancellationToken).Where(IsError)];
        string listing = string.Join("\n", errors.AsEnumerable());
        Assert.True(errors.Length == 0, $"The test source doesn't compile:\n{listing}");
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static bool IsError(Diagnostic diagnostic) => diagnostic.Severity == DiagnosticSeverity.Error;

    private static bool IsToolAssembly(string path)
        => Path.GetFileName(path).StartsWith(ToolAssemblyPrefix, StringComparison.OrdinalIgnoreCase);

    private static string ReadArgumentChecks()
    {
        using Stream stream = typeof(TestCompilation).Assembly
            .GetManifestResourceStream("Stipulate.ArgumentChecks.cs")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static ImmutableArray<MetadataReference> LoadPlatformReferences()
    {
        string platform = (string)AppContext.GetData(TrustedPlatformAssemblies)!;
        string contracts = typeof(ComparableValidationAttribute).Assembly.Location;
        return [.. platform
            .Split(Path.PathSeparator)
            .Where(static path => !IsToolAssembly(path))
            .Append(contracts)
            .Select(static path => MetadataReference.CreateFromFile(path))];
    }
}

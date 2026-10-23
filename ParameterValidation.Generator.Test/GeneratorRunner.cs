using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Generator.Test;

/// <summary>
/// Runs <see cref="ParameterValidationGenerator"/> over C# source the way the build does in
/// Interceptors mode: with interceptors enabled for the namespace the generator writes into.
/// </summary>
internal static class GeneratorRunner
{
    private const string InterceptorsFeature = "InterceptorsNamespaces";
    private const string GeneratedNamespace = "Net.NowhereAtAll.ParameterValidation.Generated";

    private static readonly CSharpParseOptions ParseOptions = TestCompilation.ParseOptions
        .WithFeatures([new KeyValuePair<string, string>(InterceptorsFeature, GeneratedNamespace)]);

    /// <param name="source">The source, after <see cref="TestCompilation.Usings"/>.</param>
    public static CSharpCompilation Compile([StringSyntax("C#")] string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            TestCompilation.Usings + source,
            ParseOptions,
            path: "/src/Test.cs",
            cancellationToken: CancellationToken
        );
        return CSharpCompilation.Create(
            TestCompilation.AssemblyName,
            [tree],
            TestCompilation.References,
            TestCompilation.CompilationOptions
        );
    }

    public static GeneratorDriver CreateDriver()
        => CSharpGeneratorDriver.Create(
            [new ParameterValidationGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    /// <summary>Runs the generator over <paramref name="source"/>.</summary>
    /// <returns>The source the generator added, or <c>null</c> if it added none.</returns>
    public static string? Generate([StringSyntax("C#")] string source)
    {
        GeneratorDriverRunResult result = CreateDriver()
            .RunGenerators(Compile(source), CancellationToken)
            .GetRunResult();
        GeneratedSourceResult generated = Assert.Single(result.Results).GeneratedSources.SingleOrDefault();
        return generated.SourceText?.ToString();
    }

    /// <summary>Generates, compiles and loads <paramref name="source"/>, asserting it compiles cleanly.</summary>
    public static Assembly GenerateAndLoad([StringSyntax("C#")] string source)
    {
        _ = CreateDriver().RunGeneratorsAndUpdateCompilation(
            Compile(source),
            out Compilation output,
            out ImmutableArray<Diagnostic> diagnostics,
            CancellationToken
        );
        Assert.Empty(diagnostics);
        TestCompilation.AssertCompiles(output);
        using MemoryStream image = new([.. TestCompilation.Emit(output)]);
        return new AssemblyLoadContext(nameof(GeneratorRunner), isCollectible: true).LoadFromStream(image);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}

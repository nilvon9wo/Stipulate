using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ParameterValidation.Analyzers;

namespace ParameterValidation.Testing;

/// <summary>Runs <see cref="PreconditionAnalyzer"/> over C# source in memory.</summary>
public static class AnalyzerRunner
{
    public const string SolutionRootProperty = "build_property.ParameterValidationSolutionRoot";

    public static Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticMarkup markup,
        params MetadataReference[] references
    )
        => RunAsync(markup.Source, solutionRoot: null, references);

    public static Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticMarkup markup,
        string? solutionRoot,
        params MetadataReference[] references
    )
        => RunAsync(markup.Source, solutionRoot, references);

    /// <param name="source">Source that must compile, including its usings.</param>
    /// <param name="solutionRoot">The <c>ParameterValidationSolutionRoot</c> build property, if set.</param>
    /// <param name="references">Assemblies the source references, beyond the platform's.</param>
    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        [StringSyntax("C#")] string source,
        string? solutionRoot,
        params MetadataReference[] references
    )
    {
        CSharpCompilation compilation = TestCompilation.CompileCleanly(source, references: references);
        return await RunAsync(compilation, solutionRoot).ConfigureAwait(false);
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(Compilation compilation, string? solutionRoot = null)
    {
        AnalyzerOptions options = new([], new GlobalOptionsProvider(solutionRoot));
        return await compilation
            .WithAnalyzers([new PreconditionAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Analyzer options with only the solution root build property set, if at all.</summary>
file sealed class GlobalOptionsProvider(string? solutionRoot) : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty = new SolutionRootOptions(null);

    public override AnalyzerConfigOptions GlobalOptions { get; } = new SolutionRootOptions(solutionRoot);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
}

file sealed class SolutionRootOptions(string? solutionRoot) : AnalyzerConfigOptions
{
    private readonly string? _solutionRoot = solutionRoot;

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        value = key == AnalyzerRunner.SolutionRootProperty
            ? this._solutionRoot
            : null;
        return value is not null;
    }
}

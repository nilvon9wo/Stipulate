using System;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// Tells whether a type is declared in this solution, so that the developer could add a
/// <c>[ComparableValidation]</c> to it.
/// </summary>
internal sealed class SolutionMembership
{
    private const char Separator = '/';
    private const char WindowsSeparator = '\\';

    private readonly Compilation _compilation;
    private readonly string? _solutionRoot;

    /// <param name="compilation">The compilation being analyzed.</param>
    /// <param name="solutionRoot">
    /// Referenced assemblies whose files are under this directory count as part of the solution.
    /// </param>
    public SolutionMembership(Compilation compilation, string? solutionRoot)
    {
        this._compilation = compilation;
        string? trimmed = solutionRoot?.Trim();
        this._solutionRoot = string.IsNullOrEmpty(trimmed)
            ? null
            : NormalizeDirectory(trimmed!);
    }

    public bool Contains(INamedTypeSymbol type)
    {
        IAssemblySymbol assembly = type.ContainingAssembly;
        return SymbolEqualityComparer.Default.Equals(assembly, this._compilation.Assembly)
            || this.IsInSolution(this._compilation.GetMetadataReference(assembly));
    }

    /// <remarks>The IDE references other projects in the solution as compilations, the command line as files.</remarks>
    private bool IsInSolution(MetadataReference? reference) => reference switch
    {
        CompilationReference => true,
        PortableExecutableReference { FilePath: { } path } => this.IsUnderRoot(path),
        _ => false,
    };

    private bool IsUnderRoot(string path)
        => this._solutionRoot is not null
            && NormalizeDirectory(path).StartsWith(this._solutionRoot, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirectory(string path)
        => path.Replace(WindowsSeparator, Separator).TrimEnd(Separator) + Separator;
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// The <c>[assembly: ComparableValidation(typeof(...), ...)]</c> descriptions in a compilation and
/// the assemblies it references, by the type they describe.
/// </summary>
internal sealed class AssemblyDescriptions(Compilation compilation)
{
    private static readonly IEqualityComparer<ITypeSymbol> TypeComparer = SymbolEqualityComparer.Default;

    private readonly ImmutableDictionary<ITypeSymbol, ImmutableArray<AttributeData>> _markersByType =
        compilation.SourceModule.ReferencedAssemblySymbols
            .Prepend(compilation.Assembly)
            .Where(ReferencesContracts)
            .SelectMany(static assembly => assembly.GetAttributes())
            .Where(ComparableValidationMarker.IsMarker)
            .SelectMany(Describe)
            .GroupBy(static described => described.Type, TypeComparer)
            .ToImmutableDictionary(
                static group => group.Key,
                static group => group.Select(static described => described.Marker).ToImmutableArray(),
                TypeComparer
            );

    /// <returns>The markers describing <paramref name="type"/>, or <c>null</c> if there are none.</returns>
    public ImmutableArray<AttributeData>? Find(ITypeSymbol type)
        => this._markersByType.TryGetValue(type, out ImmutableArray<AttributeData> markers)
            ? markers
            : null;

    /// <returns>
    /// The type the marker describes, paired with the marker; none if it isn't an assembly-form marker.
    /// </returns>
    private static IEnumerable<(ITypeSymbol Type, AttributeData Marker)> Describe(AttributeData marker)
        => ComparableValidationMarker.FindDescribedType(marker) is { } type
            ? [(type.OriginalDefinition, marker)]
            : [];

    /// <summary>Only an assembly that references the contracts can contain a description.</summary>
    private static bool ReferencesContracts(IAssemblySymbol assembly)
        => assembly.Modules
            .SelectMany(static module => module.ReferencedAssemblies)
            .Any(static identity => identity.Name == ComparableValidationMarker.ContractsAssemblyName);
}

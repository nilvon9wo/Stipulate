using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>
/// Works out what a validation attribute usage constrains, from (in order of precedence) a
/// <c>[ComparableValidation]</c> on its class, the built-in descriptions of the DataAnnotations
/// attributes, or an <c>[assembly: ComparableValidation(typeof(...))]</c> in this compilation or
/// one it references. One instance serves one compilation.
/// </summary>
/// <param name="solutionRoot">See <see cref="SolutionMembership"/>.</param>
internal sealed class ValidationAttributeDescriber(Compilation compilation, string? solutionRoot)
{
    private readonly SolutionMembership _solution = new(compilation, solutionRoot);
    private readonly AssemblyDescriptions _assemblyDescriptions = new(compilation);
    private readonly ConcurrentDictionary<ITypeSymbol, ImmutableArray<Descriptor>?> _descriptorsByClass =
        new(SymbolEqualityComparer.Default);

    public ImmutableArray<DescribedAttribute> GetValidationAttributes(
        ISymbol parameterOrProperty,
        CancellationToken cancellationToken
    )
        => [.. parameterOrProperty.GetAttributes()
            .Where(static attribute => ValidationAttributeTypes.IsValidationAttribute(attribute.AttributeClass))
            .Select(attribute => this.Describe(attribute, cancellationToken))];

    /// <summary>
    /// Whether <paramref name="type"/> is declared in this solution; see <see cref="SolutionMembership"/>.
    /// </summary>
    public bool IsInSolution(INamedTypeSymbol type) => this._solution.Contains(type);

    private DescribedAttribute Describe(AttributeData attribute, CancellationToken cancellationToken)
    {
        ITypeSymbol attributeClass = attribute.AttributeClass!.OriginalDefinition;
        ImmutableArray<Descriptor>? descriptors =
            this._descriptorsByClass.GetOrAdd(attributeClass, this.ReadDescriptors);
        ImmutableArray<Constraint>? constraints = descriptors is { } known
            ? ConstraintEvaluator.Evaluate(known, attribute)
            : null;
        return new DescribedAttribute(
            attribute,
            AttributeIdentity.GetKey(attribute),
            AttributeIdentity.GetDisplay(attribute, cancellationToken),
            constraints
        );
    }

    /// <returns>The descriptors, or <c>null</c> if the attribute class isn't (validly) described.</returns>
    private ImmutableArray<Descriptor>? ReadDescriptors(ITypeSymbol attributeClass)
    {
        List<AttributeData> own = [.. attributeClass.GetAttributes().Where(IsClassFormMarker)];
        return own.Count > 0
            ? ReadAll(own, attributeClass)
            : this.ReadInherentDescriptors(attributeClass);
    }

    /// <summary>The built-in description, or else one from an assembly.</summary>
    private ImmutableArray<Descriptor>? ReadInherentDescriptors(ITypeSymbol attributeClass)
        => BuiltInDescriptors.Find(attributeClass.ToDisplayString()) ?? this.ReadAssemblyDescriptors(attributeClass);

    private ImmutableArray<Descriptor>? ReadAssemblyDescriptors(ITypeSymbol attributeClass)
        => this._assemblyDescriptions.Find(attributeClass) is { } markers
            ? ReadAll(markers, attributeClass)
            : null;

    private static bool IsClassFormMarker(AttributeData attribute)
        => ComparableValidationMarker.IsMarker(attribute) && ComparableValidationMarker.IsClassForm(attribute);

    /// <remarks>
    /// One invalid description makes the whole class undescribed: half a description would understate it.
    /// </remarks>
    private static ImmutableArray<Descriptor>? ReadAll(IEnumerable<AttributeData> markers, ITypeSymbol attributeClass)
    {
        List<DescriptorReading> readings = [.. markers.Select(marker => DescriptorReader.Read(marker, attributeClass))];
        bool isValid = readings.All(static reading => reading.Descriptor is not null);
        return isValid
            ? [.. readings.Select(static reading => reading.Descriptor!)]
            : null;
    }
}

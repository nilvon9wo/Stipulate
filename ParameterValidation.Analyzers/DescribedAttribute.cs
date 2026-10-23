using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>A validation attribute on a parameter, with what it constrains.</summary>
/// <param name="Key">Equal for two usages of the same attribute class with the same arguments.</param>
/// <param name="Constraints">The constraints, or <c>null</c> if the usage can't be compared.</param>
internal sealed record DescribedAttribute(
    AttributeData Data,
    string Key,
    string Display,
    ImmutableArray<Constraint>? Constraints
)
{
    /// <summary>The attribute's class; every validation attribute's resolves.</summary>
    public INamedTypeSymbol AttributeClass => this.Data.AttributeClass!;

    public bool IsSameClassAs(DescribedAttribute other)
        => SymbolEqualityComparer.Default.Equals(
            this.AttributeClass.OriginalDefinition,
            other.AttributeClass.OriginalDefinition
        );
}

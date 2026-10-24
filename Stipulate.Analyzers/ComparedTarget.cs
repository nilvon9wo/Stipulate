using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>One parameter, or a property's value, of an implementation, and its counterpart in the contract.</summary>
/// <param name="Contract">The contract member.</param>
/// <param name="ContractTarget">The contract's parameter, or its property.</param>
/// <param name="Target">The implementation's parameter, or its property.</param>
/// <param name="IsNullable">Whether the implementation's type is annotated as nullable.</param>
/// <param name="Subject">The implementation's parameter or property, as the messages name it.</param>
/// <param name="InheritingType">
/// The class that uses the implementation, inherited from its base class, to implement the contract;
/// <c>null</c> if the implementation is being analyzed itself.
/// </param>
/// <param name="HasPreconditions">Whether values flow in: a parameter that isn't <c>out</c>, or a setter.</param>
/// <param name="HasGuarantees">Whether values flow out: an <c>out</c> or <c>ref</c> parameter, or a getter.</param>
internal sealed record ComparedTarget(
    ISymbol Contract,
    ISymbol ContractTarget,
    ISymbol Target,
    bool IsNullable,
    string Subject,
    string ContractName,
    INamedTypeSymbol? InheritingType,
    bool HasPreconditions,
    bool HasGuarantees
)
{
    public bool HasBoth => this.HasPreconditions && this.HasGuarantees;
}

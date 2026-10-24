using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>Pairs each parameter of an implementation, and a property's value, with the contract's.</summary>
internal static class ComparedTargetFactory
{
    private static readonly SymbolDisplayFormat DisplayFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <param name="contract">The contract member: a method or a property.</param>
    /// <param name="implementation">The member that implements or overrides it, of the same kind.</param>
    /// <param name="inheritingType">See <see cref="ComparedTarget.InheritingType"/>.</param>
    public static IEnumerable<ComparedTarget> Create(
        ISymbol contract,
        ISymbol implementation,
        INamedTypeSymbol? inheritingType
    )
    {
        MemberPair pair = new(contract, implementation, inheritingType);
        IEnumerable<ComparedTarget> parameters = GetParameters(contract)
            .Zip(GetParameters(implementation), pair.CompareParameter);
        IEnumerable<ComparedTarget> properties = contract is IPropertySymbol contractProperty
            ? [pair.CompareProperty(contractProperty, (IPropertySymbol)implementation)]
            : [];
        return parameters.Concat(properties);
    }

    public static string Display(ISymbol symbol) => symbol.ToDisplayString(DisplayFormat);

    public static bool IsNullable(ITypeSymbol type, NullableAnnotation annotation)
        => annotation == NullableAnnotation.Annotated
            || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static ImmutableArray<IParameterSymbol> GetParameters(ISymbol member)
        => member is IMethodSymbol method
            ? method.Parameters
            : ((IPropertySymbol)member).Parameters;
}

/// <summary>A contract member and its implementation, as the messages name them.</summary>
file sealed class MemberPair
{
    private readonly ISymbol _contract;
    private readonly INamedTypeSymbol? _inheritingType;
    private readonly string _contractName;
    private readonly string _implementationName;

    public MemberPair(ISymbol contract, ISymbol implementation, INamedTypeSymbol? inheritingType)
    {
        this._contract = contract;
        this._inheritingType = inheritingType;
        this._contractName = ComparedTargetFactory.Display(contract);
        string inherited = inheritingType is null
            ? string.Empty
            : $" (inherited by '{ComparedTargetFactory.Display(inheritingType)}')";
        this._implementationName = $"'{ComparedTargetFactory.Display(implementation)}'{inherited}";
    }

    public ComparedTarget CompareParameter(IParameterSymbol contractParameter, IParameterSymbol parameter)
        => new(
            this._contract,
            contractParameter,
            parameter,
            ComparedTargetFactory.IsNullable(parameter.Type, parameter.NullableAnnotation),
            $"Parameter '{parameter.Name}' of {this._implementationName}",
            this._contractName,
            this._inheritingType,
            HasPreconditions: parameter.RefKind != RefKind.Out,
            HasGuarantees: parameter.RefKind is RefKind.Out or RefKind.Ref
        );

    /// <summary>
    /// A property's attributes describe its value: what its setter accepts, and what its getter
    /// hands back.
    /// </summary>
    public ComparedTarget CompareProperty(IPropertySymbol contractProperty, IPropertySymbol property)
    {
        string kind = property.IsIndexer
            ? "Indexer"
            : "Property";
        return new ComparedTarget(
            this._contract,
            contractProperty,
            property,
            ComparedTargetFactory.IsNullable(property.Type, property.NullableAnnotation),
            $"{kind} {this._implementationName}",
            this._contractName,
            this._inheritingType,
            HasPreconditions: contractProperty.SetMethod is not null && property.SetMethod is not null,
            HasGuarantees: contractProperty.GetMethod is not null && property.GetMethod is not null
        );
    }
}

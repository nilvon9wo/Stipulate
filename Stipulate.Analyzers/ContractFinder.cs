using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// Finds the contracts a member must honour: the base-class member it overrides, and the interface
/// members it implements, explicitly or implicitly.
/// </summary>
internal static class ContractFinder
{
    /// <summary>
    /// Whether <paramref name="member"/> has anything validation attributes could constrain:
    /// parameters, or a property's value. Accessors are checked as part of their property.
    /// </summary>
    public static bool IsChecked(ISymbol member) => member switch
    {
        IMethodSymbol method => !method.Parameters.IsEmpty && IsOrdinary(method),
        IPropertySymbol => true,
        _ => false,
    };

    /// <summary>The members whose preconditions and guarantees <paramref name="member"/> must honour.</summary>
    /// <param name="member">A member <see cref="IsChecked"/> accepts: a method or a property.</param>
    public static HashSet<ISymbol> FindContracts(ISymbol member)
    {
        ImmutableArray<ISymbol> explicitImplementations = FindExplicitImplementations(member);
        IEnumerable<ISymbol> implicitImplementations = explicitImplementations.IsEmpty
            ? FindImplicitlyImplemented(member)
            : [];
        IEnumerable<ISymbol> contracts = FindOverridden(member)
            .Concat(explicitImplementations)
            .Concat(implicitImplementations);
        return new HashSet<ISymbol>(contracts, SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// The interface members that <paramref name="type"/> implements with a member it inherits from a
    /// base class that doesn't itself implement the interface. Nothing else checks them: the base
    /// class's member knows nothing of the interface.
    /// </summary>
    public static IEnumerable<ContractImplementation> FindInheritedImplementations(INamedTypeSymbol type)
        => IsClassOrStruct(type)
            ? type.AllInterfaces
                .SelectMany(static @interface => @interface.GetMembers())
                .Where(IsChecked)
                .Select(contract => FindImplementation(contract, type))
                .Where(pair => IsInheritedFromUnawareBase(pair, type))
            : [];

    private static ContractImplementation FindImplementation(ISymbol contract, INamedTypeSymbol type)
        => new(contract, type.FindImplementationForInterfaceMember(contract));

    private static bool IsOrdinary(IMethodSymbol method)
        => method.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation;

    private static bool IsClassOrStruct(INamedTypeSymbol type) => type.TypeKind is TypeKind.Class or TypeKind.Struct;

    private static IEnumerable<ISymbol> FindOverridden(ISymbol member)
    {
        ISymbol? overridden = member is IMethodSymbol method
            ? method.OverriddenMethod
            : ((IPropertySymbol)member).OverriddenProperty;
        return overridden is null
            ? []
            : [overridden];
    }

    private static ImmutableArray<ISymbol> FindExplicitImplementations(ISymbol member)
        => member is IMethodSymbol method
            ? ImmutableArray<ISymbol>.CastUp(method.ExplicitInterfaceImplementations)
            : ImmutableArray<ISymbol>.CastUp(((IPropertySymbol)member).ExplicitInterfaceImplementations);

    private static IEnumerable<ISymbol> FindImplicitlyImplemented(ISymbol member)
    {
        INamedTypeSymbol type = member.ContainingType;
        return IsClassOrStruct(type)
            ? type.AllInterfaces
                .SelectMany(@interface => @interface.GetMembers(member.Name))
                .Where(candidate => candidate.Kind == member.Kind && IsImplementedBy(candidate, member))
            : [];
    }

    /// <remarks>
    /// An override is checked against the member it overrides, which in turn was checked against the
    /// interface; checking both would report everything twice. A class or struct always has a base type.
    /// </remarks>
    private static bool IsImplementedBy(ISymbol contract, ISymbol member)
    {
        INamedTypeSymbol type = member.ContainingType;
        ISymbol? baseImplementation = type.BaseType!.FindImplementationForInterfaceMember(contract);
        bool isImplementedByBase = member.IsOverride && baseImplementation is not null;
        ISymbol? implementation = type.FindImplementationForInterfaceMember(contract);
        bool isImplementation = SymbolEqualityComparer.Default.Equals(implementation, member);
        return !isImplementedByBase && isImplementation;
    }

    private static bool IsInheritedFromUnawareBase(ContractImplementation pair, INamedTypeSymbol type)
        => pair.Implementation is { } implementation
            && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type)
            && implementation.ContainingType.TypeKind != TypeKind.Interface
            && type.BaseType!.FindImplementationForInterfaceMember(pair.Contract) is null;
}

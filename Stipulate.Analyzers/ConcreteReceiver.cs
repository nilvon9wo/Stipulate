using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// The object a call to an interface's or a virtual method is made on, where its type is certain,
/// and the method of that type the call runs: in <c>IScale scale = new Scale(); scale.Set(0);</c>,
/// <c>Scale</c> and <c>Scale.Set</c>. A woven check is the implementation's own, so whether such a
/// call throws, when it is woven, depends on the implementation's attributes, not the contract's.
/// </summary>
/// <param name="Type">The object's type.</param>
/// <param name="Method">The method of that type that the call runs.</param>
internal sealed record ConcreteReceiver(INamedTypeSymbol Type, IMethodSymbol Method)
{
    /// <returns>
    /// The receiver, if the call is to an interface's or a virtual method, on an object created
    /// there or held by a local that is only ever set to one; otherwise <c>null</c>. The method may be
    /// the contract's own, when the type doesn't override it.
    /// </returns>
    public static ConcreteReceiver? Find(IInvocationOperation invocation, CancellationToken cancellationToken)
    {
        IMethodSymbol target = invocation.TargetMethod;
        INamedTypeSymbol? type = target.IsVirtual || target.IsAbstract || target.IsOverride
            ? FindCreatedType(invocation.Instance, cancellationToken)
            : null;
        IMethodSymbol? method = type is null
            ? null
            : FindImplementation(type, target);
        return method is null
            ? null
            : new ConcreteReceiver(type!, method);
    }

    private static INamedTypeSymbol? FindCreatedType(IOperation? instance, CancellationToken cancellationToken)
        => Unwrap(instance) switch
        {
            IObjectCreationOperation creation => creation.Type as INamedTypeSymbol,
            ILocalReferenceOperation { Local: var local } reference =>
                FindOnlyCreation(local, reference, cancellationToken),
            _ => null,
        };

    /// <summary>The type of the object a local is initialized with, if nothing else is ever assigned to it.</summary>
    private static INamedTypeSymbol? FindOnlyCreation(
        ILocalSymbol local,
        ILocalReferenceOperation reference,
        CancellationToken cancellationToken
    )
    {
        EqualsValueClauseSyntax? initializer = local.DeclaringSyntaxReferences
            .Select(declaration => declaration.GetSyntax(cancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .Select(static declarator => declarator.Initializer)
            .FirstOrDefault();
        IOperation? value = initializer is null
            ? null
            : ((IVariableInitializerOperation)reference.SemanticModel!.GetOperation(initializer, cancellationToken)!)
                .Value;
        return local.IsRef || IsWrittenAgain(local, reference)
            ? null
            : FindCreatedType(value, cancellationToken);
    }

    /// <summary>Whether the body the reference is in assigns the local, or passes it by reference.</summary>
    private static bool IsWrittenAgain(ILocalSymbol local, IOperation reference)
    {
        IOperation root = reference;
        while (root.Parent is { } parent)
        {
            root = parent;
        }

        return root.Descendants()
            .OfType<ILocalReferenceOperation>()
            .Where(other => SymbolEqualityComparer.Default.Equals(other.Local, local))
            .Any(static other => IsWrite(other));
    }

    private static bool IsWrite(ILocalReferenceOperation reference) => reference.Parent switch
    {
        IAssignmentOperation assignment => assignment.Target == reference,
        IArgumentOperation argument => IsPassedByReference(argument.Syntax),
        ITupleOperation => true,
        _ => false,
    };

    /// <summary>
    /// Whether the argument is written <c>ref</c>, <c>out</c> or <c>in</c>; an extension method's
    /// receiver, also an argument, is written without an <c>ArgumentSyntax</c>.
    /// </summary>
    private static bool IsPassedByReference(SyntaxNode argument)
        => argument is ArgumentSyntax syntax && !syntax.RefKindKeyword.IsKind(SyntaxKind.None);

    /// <summary>
    /// Looks through the conversions that keep the same object: to a base type or an interface, or boxing.
    /// </summary>
    private static IOperation? Unwrap(IOperation? operation)
        => operation is IConversionOperation conversion && IsSameObject(conversion.GetConversion())
            ? Unwrap(conversion.Operand)
            : operation;

    private static bool IsSameObject(Conversion conversion)
        => conversion.IsReference || conversion.IsBoxing || conversion.IsIdentity;

    /// <summary>The method of <paramref name="type"/>, or of its base types, that the call runs.</summary>
    private static IMethodSymbol? FindImplementation(INamedTypeSymbol type, IMethodSymbol target)
        => target.ContainingType.TypeKind == TypeKind.Interface
            ? type.FindImplementationForInterfaceMember(target) as IMethodSymbol
            : TypeHierarchy.SelfAndBaseTypes(type)
                .SelectMany(candidate => candidate.GetMembers(target.Name).OfType<IMethodSymbol>())
                .FirstOrDefault(candidate => Overrides(candidate, target));

    private static bool Overrides(IMethodSymbol candidate, IMethodSymbol target)
    {
        for (IMethodSymbol? method = candidate; method is not null; method = method.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, target.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }
}

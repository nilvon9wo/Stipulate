using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ParameterValidation.Analyzers;

/// <summary>
/// Compares the validation attributes on one parameter or property of an implementation with those
/// on its contract, as preconditions: the implementation may weaken them, but not strengthen them,
/// and must say so explicitly when it drops one.
/// </summary>
/// <remarks>
/// A value has to satisfy all of a parameter's attributes, so the contract's constraints on a
/// dimension combine into the strongest of them, and each of the implementation's must be no
/// stronger than that. The contract's undescribed attributes are ignored, which can only produce
/// extra reports, never miss one.
/// </remarks>
internal static class PreconditionComparer
{
    private const string ContractSide = "the contract";

    /// <summary>Whether a nullable parameter makes a statement about: it explicitly drops <c>[Required]</c>.</summary>
    private static readonly ImmutableArray<Statement> NullableStatements =
    [
        new(Dimensions.NotNull, ConstraintKind.Flag),
        new(Dimensions.NotBlank, ConstraintKind.Flag),
    ];

    /// <param name="contract">The contract's attributes.</param>
    /// <param name="implementation">The implementation's attributes.</param>
    /// <param name="implementationAcceptsNull">Whether the implementation's parameter is annotated as nullable.</param>
    public static IEnumerable<Problem> Compare(
        AttributeSet contract,
        AttributeSet implementation,
        bool implementationAcceptsNull
    )
    {
        IEnumerable<Problem> strengthened = FindStrengthened(contract, implementation);
        IEnumerable<Problem> dropped = FindDropped(contract, implementation, implementationAcceptsNull);
        return strengthened.Concat(dropped);
    }

    private static IEnumerable<Problem> FindStrengthened(AttributeSet contract, AttributeSet implementation)
    {
        Comparand comparand = contract.AsComparand(ContractSide);
        return implementation.Attributes
            .Where(attribute => !contract.Restates(attribute))
            .Select(attribute => CompareAttribute(attribute, comparand))
            .OfType<Problem>();
    }

    private static Problem? CompareAttribute(DescribedAttribute attribute, Comparand contract)
        => attribute.Constraints is { } constraints
            ? FindStrengthening(attribute, constraints, contract)
            : new Problem(ProblemKind.NotComparable, attribute);

    private static Problem? FindStrengthening(
        DescribedAttribute attribute,
        ImmutableArray<Constraint> constraints,
        Comparand contract
    )
    {
        string? strengthening = Constraint.FindFirstStrengthening(constraints, contract);
        return strengthening is null
            ? null
            : new Problem(ProblemKind.Strengthened, attribute, $"[{attribute.Display}] {strengthening}");
    }

    private static IEnumerable<Problem> FindDropped(
        AttributeSet contract,
        AttributeSet implementation,
        bool implementationAcceptsNull
    )
    {
        HashSet<Statement> statements = GetStatements(implementation, implementationAcceptsNull);
        return contract.Attributes
            .Where(attribute => !IsAddressed(attribute, implementation, statements))
            .Select(static attribute => new Problem(ProblemKind.Dropped, attribute));
    }

    private static HashSet<Statement> GetStatements(AttributeSet implementation, bool implementationAcceptsNull)
    {
        IEnumerable<Statement> constrained = implementation.Constraints
            .Select(static constraint => constraint.Statement);
        ImmutableArray<Statement> nullable = implementationAcceptsNull
            ? NullableStatements
            : [];
        return [.. constrained, .. nullable];
    }

    /// <summary>
    /// Whether the implementation restates the contract's attribute exactly; or states the same class
    /// with other arguments, reported already if it can't be compared; or makes a deliberate
    /// statement about each thing it constrains, such as <c>[MinLength(0)]</c> for a lower bound on
    /// length and <c>[MaxLength]</c> for an upper one.
    /// </summary>
    private static bool IsAddressed(
        DescribedAttribute attribute,
        AttributeSet implementation,
        HashSet<Statement> statements
    )
        => implementation.Restates(attribute)
            || implementation.HasClassOf(attribute)
            || IsStatedAbout(attribute, statements);

    private static bool IsStatedAbout(DescribedAttribute attribute, HashSet<Statement> statements)
    {
        ImmutableArray<Constraint> constraints = attribute.Constraints ?? [];
        List<Constraint> meaningful = [.. constraints.Where(static constraint => !constraint.IsVacuous)];
        return meaningful.Count > 0 && meaningful.All(constraint => statements.Contains(constraint.Statement));
    }
}

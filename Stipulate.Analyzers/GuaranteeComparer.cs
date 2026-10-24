using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// Compares the validation attributes on something an implementation hands back (a property's
/// value, an <c>out</c> or <c>ref</c> parameter) with those on its contract, as guarantees: the
/// implementation may strengthen them, but not weaken or leave them out.
/// </summary>
/// <remarks>
/// The implementation's constraints on a dimension combine into the strongest of them, and each of
/// the contract's must be no stronger than that. The implementation's undescribed attributes are
/// ignored, which can only produce extra reports, never miss one.
/// </remarks>
internal static class GuaranteeComparer
{
    private const string ImplementationSide = "the implementation";

    /// <param name="contract">The contract's attributes.</param>
    /// <param name="implementation">The implementation's attributes.</param>
    /// <param name="implementationIsNullable">
    /// Whether the implementation's type is annotated as nullable, which says it may hand back null.
    /// </param>
    public static IEnumerable<Problem> Compare(
        AttributeSet contract,
        AttributeSet implementation,
        bool implementationIsNullable
    )
    {
        Comparand comparand = implementation.AsComparand(ImplementationSide);
        GuaranteeSide side = new(implementation, implementationIsNullable, comparand);
        return contract.Attributes
            .Where(attribute => !implementation.Restates(attribute))
            .Select(side.Compare)
            .OfType<Problem>();
    }
}

/// <summary>The implementation's side of a comparison of guarantees.</summary>
file sealed class GuaranteeSide
{
    private readonly AttributeSet _implementation;
    private readonly Comparand _comparand;
    private readonly HashSet<string> _dimensions;

    public GuaranteeSide(AttributeSet implementation, bool isNullable, Comparand comparand)
    {
        this._implementation = implementation;
        this._comparand = comparand;
        IEnumerable<string> constrained = implementation.Constraints.Select(static constraint => constraint.Dimension);
        ImmutableArray<string> nullable = isNullable
            ? [Dimensions.NotNull]
            : [];
        this._dimensions = [.. constrained, .. nullable];
    }

    public Problem? Compare(DescribedAttribute attribute)
        => attribute.Constraints is { } constraints
            ? this.CompareConstraints(attribute, constraints)
            : this.CompareUndescribed(attribute);

    /// <summary>The same class with other arguments can't be compared; any other class has been dropped.</summary>
    private Problem CompareUndescribed(DescribedAttribute attribute)
    {
        ProblemKind kind = this._implementation.HasClassOf(attribute)
            ? ProblemKind.NotComparable
            : ProblemKind.Dropped;
        return new Problem(kind, attribute, IsGuarantee: true);
    }

    private Problem? CompareConstraints(DescribedAttribute attribute, ImmutableArray<Constraint> constraints)
    {
        List<Constraint> promises = [.. constraints.Where(static constraint => !constraint.IsVacuous)];
        return promises.Count == 0
            ? null
            : this.ComparePromises(attribute, promises);
    }

    /// <summary>A promise on a dimension the implementation says nothing about has been dropped.</summary>
    private Problem? ComparePromises(DescribedAttribute attribute, List<Constraint> promises)
    {
        bool isKept = promises.Any(promise => this._dimensions.Contains(promise.Dimension));
        return isKept
            ? this.FindWeakening(attribute, promises)
            : new Problem(ProblemKind.Dropped, attribute, IsGuarantee: true);
    }

    private Problem? FindWeakening(DescribedAttribute attribute, List<Constraint> promises)
    {
        string? weakening = Constraint.FindFirstStrengthening(promises, this._comparand);
        return weakening is null
            ? null
            : new Problem(ProblemKind.Weakened, attribute, $"[{attribute.Display}] {weakening}", IsGuarantee: true);
    }
}

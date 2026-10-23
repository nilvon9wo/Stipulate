using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>Finds the problems with one compared target, as preconditions, guarantees, or both.</summary>
internal static class ProblemFinder
{
    /// <remarks>
    /// When both apply, a missing attribute is reported once, as a weakened guarantee (an error,
    /// where a dropped precondition is only a warning); and an attribute that can't be compared once,
    /// on the implementation's attribute.
    /// </remarks>
    public static IEnumerable<Problem> Find(
        ComparedTarget target,
        ImmutableArray<DescribedAttribute> contractAttributes,
        ImmutableArray<DescribedAttribute> implementationAttributes
    )
    {
        AttributeSet contract = new(contractAttributes);
        AttributeSet implementation = new(implementationAttributes);
        IEnumerable<Problem> guarantees = target.HasGuarantees
            ? GuaranteeComparer.Compare(contract, implementation, target.IsNullable)
            : [];
        IEnumerable<Problem> preconditions = target.HasPreconditions
            ? PreconditionComparer.Compare(contract, implementation, target.IsNullable)
            : [];
        return guarantees
            .Where(problem => !target.HasBoth || problem.Kind != ProblemKind.NotComparable)
            .Concat(preconditions.Where(problem => !target.HasBoth || problem.Kind != ProblemKind.Dropped));
    }
}

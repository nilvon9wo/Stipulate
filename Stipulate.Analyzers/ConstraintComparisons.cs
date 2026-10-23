using System.Collections.Generic;
using System.Collections.Immutable;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>The one <see cref="IConstraintComparing"/> for each <see cref="ConstraintKind"/>.</summary>
internal static class ConstraintComparisons
{
    private static readonly ImmutableDictionary<ConstraintKind, IConstraintComparing> ByKind =
        new Dictionary<ConstraintKind, IConstraintComparing>
        {
            [ConstraintKind.Flag] = new FlagComparison(),
            [ConstraintKind.LowerBound] = new LowerBoundComparison(),
            [ConstraintKind.UpperBound] = new UpperBoundComparison(),
            [ConstraintKind.AllowedValues] = new AllowedValuesComparison(),
            [ConstraintKind.DeniedValues] = new DeniedValuesComparison(),
        }.ToImmutableDictionary();

    public static IConstraintComparing Of(ConstraintKind kind) => ByKind[kind];
}

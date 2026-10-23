namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>A lower bound: a higher one is stronger.</summary>
internal sealed class LowerBoundComparison : BoundComparison
{
    public override ConstraintKind StatementKind => ConstraintKind.LowerBound;

    protected override string Name => "lower";

    protected override string InclusiveOperator => ">=";

    protected override string ExclusiveOperator => ">";

    /// <remarks>A length is never negative, so a minimum length of 0 or less rejects nothing.</remarks>
    public override bool IsVacuous(Constraint constraint)
        => base.IsVacuous(constraint) || (constraint.Dimension == Dimensions.Length && IsAtMostZero(constraint));

    protected override bool IsTighter(double value, double other) => value > other;

    private static bool IsAtMostZero(Constraint constraint) => IsAtMostZero(constraint.Bound, constraint.IsExclusive);

    private static bool IsAtMostZero(double bound, bool isExclusive) => bound < 0.0 || (bound is 0.0 && !isExclusive);
}

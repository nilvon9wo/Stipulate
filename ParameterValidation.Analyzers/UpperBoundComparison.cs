namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>An upper bound: a lower one is stronger.</summary>
internal sealed class UpperBoundComparison : BoundComparison
{
    public override ConstraintKind StatementKind => ConstraintKind.UpperBound;

    protected override string Name => "upper";

    protected override string InclusiveOperator => "<=";

    protected override string ExclusiveOperator => "<";

    protected override bool IsTighter(double value, double other) => value < other;
}

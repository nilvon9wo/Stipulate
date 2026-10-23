using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// A numeric bound is satisfied by a bound of the same kind that is at least as strong. A value has
/// to satisfy every bound, so the other side's bounds combine into the strongest of them.
/// </summary>
internal abstract class BoundComparison : IConstraintComparing
{
    private const string InfinityDisplay = "infinity";
    private const string RoundTripFormat = "R";

    public bool IsBound => true;

    public abstract ConstraintKind StatementKind { get; }

    /// <summary>How the messages name the bound: "lower" or "upper".</summary>
    protected abstract string Name { get; }

    protected abstract string InclusiveOperator { get; }

    protected abstract string ExclusiveOperator { get; }

    public virtual bool IsVacuous(Constraint constraint) => double.IsInfinity(constraint.Bound);

    public string? FindStrengthening(Constraint constraint, Comparand other)
    {
        List<Constraint> bounds = other.OfKind(constraint.Dimension, this.StatementKind);
        return bounds.Count == 0
            ? this.DescribeMissingBound(constraint, other.Side)
            : this.DescribeStrongerBound(constraint, bounds, other.Side);
    }

    /// <summary>Whether bound <paramref name="value"/> is tighter than bound <paramref name="other"/>.</summary>
    protected abstract bool IsTighter(double value, double other);

    private string DescribeMissingBound(Constraint constraint, string side)
        => $"requires {this.Describe(constraint)}, but {side} has no {this.Name} bound on {constraint.Dimension}";

    private string? DescribeStrongerBound(Constraint constraint, List<Constraint> bounds, string side)
    {
        Constraint strongest = bounds.Aggregate(this.Stronger);
        bool isStronger = this.IsStronger(constraint, strongest);
        return isStronger
            ? $"requires {this.Describe(constraint)}, but {side} accepts {this.Describe(strongest)}"
            : null;
    }

    private Constraint Stronger(Constraint current, Constraint candidate)
        => this.IsStronger(candidate, current)
            ? candidate
            : current;

    /// <summary>
    /// Whether bound <paramref name="bound"/> rejects something that <paramref name="other"/> accepts.
    /// </summary>
    private bool IsStronger(Constraint bound, Constraint other)
    {
        bool isSameBound = bound.Bound.Equals(other.Bound);
        bool isOnlyExclusive = bound.IsExclusive && !other.IsExclusive;
        return isSameBound
            ? isOnlyExclusive
            : this.IsTighter(bound.Bound, other.Bound);
    }

    private string Describe(Constraint bound)
    {
        string comparison = bound.IsExclusive
            ? this.ExclusiveOperator
            : this.InclusiveOperator;
        string value = bound.BoundDisplay ?? FormatNumber(bound.Bound);
        return $"{bound.Dimension} {comparison} {value}";
    }

    private static string FormatNumber(double bound)
        => double.IsInfinity(bound)
            ? InfinityDisplay
            : bound.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
}

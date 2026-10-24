using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

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

    public string? FindRejection(Constraint constraint, ArgumentValue value)
        => Measure(constraint, value) is { } measured && this.Rejects(constraint, measured)
            ? $"requires {this.Describe(constraint)}, but {constraint.Dimension} is {FormatNumber(measured)}"
            : null;

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

    /// <summary>
    /// The value on the bound's dimension, or <c>null</c> if it isn't known for certain how the
    /// attribute will measure it:
    /// <list type="bullet">
    /// <item>A negative upper bound on Length is <c>MaxLength(-1)</c>'s, which means no maximum.</item>
    /// <item>A bound given as text, such as a date's, is in units no constant has.</item>
    /// <item>
    /// A whole-number bound may be <c>Range(int, int)</c>'s, which first converts the value to an
    /// int, rounding a fraction; so a fraction isn't compared with one.
    /// </item>
    /// </list>
    /// </summary>
    private static double? Measure(Constraint constraint, ArgumentValue value) => constraint.Dimension switch
    {
        Dimensions.Length when constraint.Bound >= 0 => value.Length,
        Dimensions.Value when constraint.BoundDisplay is null && value.Number is { } number
            && !IsRoundedFor(number, constraint.Bound) => number,
        _ => null,
    };

    private static bool IsRoundedFor(double number, double bound) => !IsWholeNumber(number) && IsWholeNumber(bound);

    private static bool IsWholeNumber(double number) => Math.Floor(number).Equals(number);

    /// <summary>Whether the bound rejects <paramref name="measured"/>: the bound itself only if exclusive.</summary>
    private bool Rejects(Constraint bound, double measured)
        => measured.Equals(bound.Bound)
            ? bound.IsExclusive
            : this.IsTighter(bound.Bound, measured);

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

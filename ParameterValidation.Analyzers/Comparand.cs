using System.Collections.Generic;
using System.Linq;

namespace ParameterValidation.Analyzers;

/// <summary>
/// The side a constraint is compared against: all of its constraints, which a value must satisfy
/// together.
/// </summary>
/// <param name="side">How the messages name this side: "the contract" or "the implementation".</param>
internal sealed class Comparand(IReadOnlyList<Constraint> constraints, string side)
{
    private readonly IReadOnlyList<Constraint> _constraints = constraints;

    public string Side { get; } = side;

    public List<Constraint> OfKind(string dimension, ConstraintKind kind)
        => [.. this._constraints.Where(other => other.Dimension == dimension && other.Kind == kind)];

    public bool HasKind(string dimension, ConstraintKind kind) => this.OfKind(dimension, kind).Count > 0;

    /// <summary>The values this side denies on <paramref name="dimension"/>.</summary>
    public HashSet<string> DeniedValues(string dimension)
        => [.. this.OfKind(dimension, ConstraintKind.DeniedValues).SelectMany(static other => other.Values)];

    /// <summary>
    /// The values that every one of this side's allowed-values constraints on
    /// <paramref name="dimension"/> accepts, or <c>null</c> if it has none and so accepts any value.
    /// </summary>
    public HashSet<string>? AllowedValues(string dimension)
    {
        List<Constraint> allowed = this.OfKind(dimension, ConstraintKind.AllowedValues);
        return allowed.Count == 0
            ? null
            : allowed.Skip(1).Aggregate(new HashSet<string>(allowed[0].Values), Intersect);
    }

    private static HashSet<string> Intersect(HashSet<string> values, Constraint other)
    {
        values.IntersectWith(other.Values);
        return values;
    }
}

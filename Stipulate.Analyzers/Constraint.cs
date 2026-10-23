using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>One usage's constraint on one dimension.</summary>
/// <param name="Values">
/// For <see cref="ConstraintKind.AllowedValues"/> and <see cref="ConstraintKind.DeniedValues"/>:
/// the values, as C# literals.
/// </param>
/// <param name="BoundDisplay">
/// How to show <paramref name="Bound"/>, if not as a number: e.g. a date, as written.
/// </param>
internal sealed record Constraint(
    string Dimension,
    ConstraintKind Kind,
    double Bound,
    bool IsExclusive,
    ImmutableArray<string> Values,
    string? BoundDisplay = null
)
{
    public IConstraintComparing Comparison => ConstraintComparisons.Of(this.Kind);

    /// <summary>Whether the constraint rejects nothing, such as <c>MaxLength()</c>'s infinite maximum.</summary>
    public bool IsVacuous => this.Comparison.IsVacuous(this);

    public Statement Statement => new(this.Dimension, this.Comparison.StatementKind);

    /// <returns>What this rejects that <paramref name="other"/> accepts, or <c>null</c> if nothing.</returns>
    public string? FindStrengthening(Comparand other)
        => this.IsVacuous
            ? null
            : this.Comparison.FindStrengthening(this, other);

    /// <returns>
    /// What the first of <paramref name="constraints"/> that rejects something <paramref name="other"/>
    /// accepts rejects, or <c>null</c> if none does.
    /// </returns>
    public static string? FindFirstStrengthening(IEnumerable<Constraint> constraints, Comparand other)
        => constraints
            .Select(constraint => constraint.FindStrengthening(other))
            .FirstOrDefault(static strengthening => strengthening is not null);

    /// <summary>A constraint whose presence is all there is to it, such as <c>[Required]</c>'s.</summary>
    public static Constraint Flag(string dimension) => new(dimension, ConstraintKind.Flag, 0, IsExclusive: false, []);
}

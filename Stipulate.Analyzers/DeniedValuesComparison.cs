using System.Collections.Generic;
using System.Linq;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// A list of denied values: a longer one is stronger. It makes a statement about which values are
/// allowed, just as an allowed-values list does.
/// </summary>
internal sealed class DeniedValuesComparison : IConstraintComparing
{
    public bool IsBound => false;

    public ConstraintKind StatementKind => ConstraintKind.AllowedValues;

    public bool IsVacuous(Constraint constraint) => false;

    public string? FindStrengthening(Constraint constraint, Comparand other)
    {
        HashSet<string> denied = other.DeniedValues(constraint.Dimension);
        HashSet<string>? accepted = other.AllowedValues(constraint.Dimension);
        string? rejected = constraint.Values.FirstOrDefault(value => IsAccepted(value, denied, accepted));
        return rejected is null
            ? null
            : $"rejects {rejected}, which {other.Side} accepts";
    }

    /// <param name="value">One of the constraint's denied values.</param>
    /// <param name="denied">The values the other side denies.</param>
    /// <param name="accepted">The only values accepted, or <c>null</c> if any value not denied is.</param>
    private static bool IsAccepted(string value, HashSet<string> denied, HashSet<string>? accepted)
        => !denied.Contains(value) && (accepted?.Contains(value) ?? true);
}

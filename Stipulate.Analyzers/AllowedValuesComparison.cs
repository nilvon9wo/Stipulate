using System.Collections.Generic;
using System.Linq;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>A list of allowed values: a shorter one is stronger.</summary>
internal sealed class AllowedValuesComparison : IConstraintComparing
{
    private const string ValueSeparator = ", ";

    public bool IsBound => false;

    public ConstraintKind StatementKind => ConstraintKind.AllowedValues;

    public bool IsVacuous(Constraint constraint) => false;

    public string? FindStrengthening(Constraint constraint, Comparand other)
    {
        HashSet<string>? accepted = other.AllowedValues(constraint.Dimension);
        return accepted is null
            ? DescribeUnlimited(constraint, other.Side)
            : DescribeRejected(constraint, accepted, other);
    }

    private static string DescribeUnlimited(Constraint constraint, string side)
    {
        string values = string.Join(ValueSeparator, constraint.Values);
        return $"accepts only {values}, but {side} doesn't limit {constraint.Dimension} to a list";
    }

    private static string? DescribeRejected(Constraint constraint, HashSet<string> accepted, Comparand other)
    {
        HashSet<string> denied = other.DeniedValues(constraint.Dimension);
        string? rejected = accepted
            .FirstOrDefault(value => !constraint.Values.Contains(value) && !denied.Contains(value));
        return rejected is null
            ? null
            : $"rejects {rejected}, which {other.Side} accepts";
    }
}

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// Mirrors <c>Net.NowhereAtAll.Stipulate.Contracts.ValidationComparison</c>; the values must match.
/// </summary>
internal enum ConstraintKind
{
    Flag,
    LowerBound,
    UpperBound,
    AllowedValues,
    DeniedValues,
}

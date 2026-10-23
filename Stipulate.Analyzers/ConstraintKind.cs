namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// Mirrors <c>Net.NowhereAtAll.ParameterValidation.Contracts.ValidationComparison</c>; the values must match.
/// </summary>
internal enum ConstraintKind
{
    Flag,
    LowerBound,
    UpperBound,
    AllowedValues,
    DeniedValues,
}

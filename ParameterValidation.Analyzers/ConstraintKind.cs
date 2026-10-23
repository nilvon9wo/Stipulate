namespace ParameterValidation.Analyzers;

/// <summary>Mirrors <c>ParameterValidation.Contracts.ValidationComparison</c>; the values must match.</summary>
internal enum ConstraintKind
{
    Flag,
    LowerBound,
    UpperBound,
    AllowedValues,
    DeniedValues,
}

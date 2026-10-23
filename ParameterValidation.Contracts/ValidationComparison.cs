namespace ParameterValidation.Contracts;

/// <summary>How a <see cref="ComparableValidationAttribute"/> constrains its dimension.</summary>
public enum ValidationComparison
{
    /// <summary>
    /// The attribute's presence is the constraint; e.g. <c>RequiredAttribute</c> on
    /// <see cref="ValidationDimensions.NotNull"/>. Takes no bound.
    /// </summary>
    Flag,

    /// <summary>
    /// Accepts values whose dimension is at least the bound (above it, if exclusive). A higher
    /// bound is a stronger constraint.
    /// </summary>
    LowerBound,

    /// <summary>
    /// Accepts values whose dimension is at most the bound (below it, if exclusive). A lower
    /// bound is a stronger constraint.
    /// </summary>
    UpperBound,

    /// <summary>Accepts only the listed values. A shorter list is a stronger constraint.</summary>
    AllowedValues,

    /// <summary>Rejects the listed values. A longer list is a stronger constraint.</summary>
    DeniedValues,
}

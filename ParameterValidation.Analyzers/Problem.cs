namespace ParameterValidation.Analyzers;

/// <param name="Attribute">
/// The implementation's attribute; or the contract's, for <see cref="ProblemKind.Dropped"/> and for
/// every problem with a guarantee.
/// </param>
/// <param name="Detail">
/// For <see cref="ProblemKind.Strengthened"/>: what the attribute rejects that the contract accepts.
/// For <see cref="ProblemKind.Weakened"/>: what the contract promises that the implementation doesn't.
/// </param>
/// <param name="IsGuarantee">Whether the problem is with a guarantee rather than a precondition.</param>
internal sealed record Problem(
    ProblemKind Kind,
    DescribedAttribute Attribute,
    string Detail = "",
    bool IsGuarantee = false
);

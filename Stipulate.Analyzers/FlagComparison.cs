namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>A flag, such as <c>[Required]</c>'s NotNull, is satisfied only by the same flag.</summary>
internal sealed class FlagComparison : IConstraintComparing
{
    public bool IsBound => false;

    public ConstraintKind StatementKind => ConstraintKind.Flag;

    public bool IsVacuous(Constraint constraint) => false;

    public string? FindStrengthening(Constraint constraint, Comparand other)
    {
        bool isShared = other.HasKind(constraint.Dimension, ConstraintKind.Flag);
        return isShared
            ? null
            : DescribeRejection(constraint.Dimension, other.Side);
    }

    /// <remarks>Only NotNull and NotBlank say what they reject; another dimension's flag can't tell.</remarks>
    public string? FindRejection(Constraint constraint, ArgumentValue value) => constraint.Dimension switch
    {
        Dimensions.NotNull when value.IsNull => "rejects null",
        Dimensions.NotBlank when value.Constant is string text && string.IsNullOrWhiteSpace(text) =>
            "rejects empty and whitespace-only strings",
        _ => null,
    };

    private static string DescribeRejection(string dimension, string side) => dimension switch
    {
        Dimensions.NotNull => $"rejects null, which {side} accepts",
        Dimensions.NotBlank => $"rejects empty and whitespace-only strings, which {side} accepts",
        _ => $"imposes '{dimension}', which {side} doesn't",
    };
}

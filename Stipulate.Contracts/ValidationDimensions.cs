namespace Net.NowhereAtAll.Stipulate.Contracts;

/// <summary>
/// The dimensions the analyzer uses to describe the built-in <c>DataAnnotations</c> attributes.
/// Describe your own attributes with these where they mean the same thing, so they can be
/// compared with the built-in ones.
/// </summary>
public static class ValidationDimensions
{
    /// <summary>
    /// Rejects <c>null</c> (<c>RequiredAttribute</c>). Use with <see cref="ValidationComparison.Flag"/>.
    /// </summary>
    public const string NotNull = "NotNull";

    /// <summary>
    /// Rejects empty and whitespace-only strings (<c>RequiredAttribute</c> without
    /// <c>AllowEmptyStrings</c>). Use with <see cref="ValidationComparison.Flag"/>.
    /// </summary>
    public const string NotBlank = "NotBlank";

    /// <summary>
    /// A string's length or a collection's count (<c>MinLengthAttribute</c>, <c>MaxLengthAttribute</c>,
    /// <c>LengthAttribute</c>, <c>StringLengthAttribute</c>).
    /// </summary>
    public const string Length = "Length";

    /// <summary>
    /// The value itself (<c>RangeAttribute</c>, <c>AllowedValuesAttribute</c>, <c>DeniedValuesAttribute</c>).
    /// </summary>
    public const string Value = "Value";
}

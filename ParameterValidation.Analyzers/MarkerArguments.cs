using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>The arguments of one <c>[ComparableValidation]</c>, before they are validated.</summary>
/// <param name="Comparison">The <c>ValidationComparison</c>, which might not be a defined one.</param>
/// <param name="ValueConstant">The <c>Value</c>, unless it is missing or <c>null</c>.</param>
internal sealed record MarkerArguments(
    string Dimension,
    int Comparison,
    string? Argument,
    TypedConstant? ValueConstant,
    bool IsExclusive,
    string? ExclusiveArgument
)
{
    /// <summary>The dimension and comparison are the last two constructor arguments, in either form.</summary>
    private const int TrailingArgumentCount = 2;

    private const string ArgumentName = "Argument";
    private const string ValueName = "Value";
    private const string IsExclusiveName = "IsExclusive";
    private const string ExclusiveArgumentName = "ExclusiveArgument";

    public ConstraintKind Kind => (ConstraintKind)this.Comparison;

    public bool IsDefinedKind => this.Comparison is >= (int)ConstraintKind.Flag and <= (int)ConstraintKind.DeniedValues;

    public bool IsBound => this.IsDefinedKind && ConstraintComparisons.Of(this.Kind).IsBound;

    /// <summary>
    /// The <c>Value</c>, converted for the kind; <c>null</c> if it is missing or of the wrong type,
    /// or the kind isn't defined.
    /// </summary>
    public object? Value => this.ValueConstant is { } constant && this.IsDefinedKind
        ? ConstantConversion.ToValue(constant, this.Kind)
        : null;

    /// <returns>The arguments, or <c>null</c> if the marker lacks a dimension or a comparison.</returns>
    public static MarkerArguments? Read(AttributeData marker)
    {
        int offset = marker.ConstructorArguments.Length - TrailingArgumentCount;
        return offset < 0
            ? null
            : ReadFrom(marker, offset);
    }

    public Descriptor ToDescriptor()
        => new(this.Dimension, this.Kind, this.Argument, this.Value, this.IsExclusive, this.ExclusiveArgument);

    private static MarkerArguments? ReadFrom(AttributeData marker, int offset)
    {
        object? dimension = marker.ConstructorArguments[offset].Value;
        object? comparison = marker.ConstructorArguments[offset + 1].Value;
        return dimension is string text && comparison is int number
            ? ReadNamed(marker, text, number)
            : null;
    }

    private static MarkerArguments ReadNamed(AttributeData marker, string dimension, int comparison)
    {
        TypedConstant? value = AttributeArguments.FindNamed(marker, ValueName);
        TypedConstant? nonNullValue = value is { IsNull: false }
            ? value
            : null;
        return new MarkerArguments(
            dimension,
            comparison,
            AttributeArguments.FindNamed(marker, ArgumentName)?.Value as string,
            nonNullValue,
            AttributeArguments.FindNamed(marker, IsExclusiveName) is { Value: true },
            AttributeArguments.FindNamed(marker, ExclusiveArgumentName)?.Value as string
        );
    }
}

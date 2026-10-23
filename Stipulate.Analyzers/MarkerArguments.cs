using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

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

    /// <remarks>Only for a defined kind, as is everything below that depends on it.</remarks>
    public bool IsBound => ConstraintComparisons.Of(this.Kind).IsBound;

    /// <summary>The <c>Value</c>, converted for the kind; <c>null</c> if it is missing or of the wrong type.</summary>
    public object? Value => this.ValueConstant is { } constant
        ? ConstantConversion.ToValue(constant, this.Kind)
        : null;

    /// <summary>Whether an <c>Argument</c> or a <c>Value</c> says where the bound or the values come from.</summary>
    public bool HasSource => this.Argument is not null || this.ValueConstant.HasValue;

    /// <summary>Whether <c>IsExclusive</c> or an <c>ExclusiveArgument</c> is given.</summary>
    public bool HasExclusivity => this.IsExclusive || this.ExclusiveArgument is not null;

    /// <summary>Whether exclusivity is given for a comparison that isn't a bound, where it means nothing.</summary>
    public bool HasMisplacedExclusivity => !this.IsBound && this.HasExclusivity;

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
        object? dimension = AttributeArguments.ScalarValue(marker.ConstructorArguments[offset]);
        object? comparison = AttributeArguments.ScalarValue(marker.ConstructorArguments[offset + 1]);
        return dimension is string text && comparison is int number
            ? ReadNamed(marker, text, number)
            : null;
    }

    private static object? ReadNamedScalar(AttributeData marker, string name)
        => AttributeArguments.FindNamed(marker, name) is { } constant
            ? AttributeArguments.ScalarValue(constant)
            : null;

    private static MarkerArguments ReadNamed(AttributeData marker, string dimension, int comparison)
    {
        TypedConstant? value = AttributeArguments.FindNamed(marker, ValueName);
        TypedConstant? nonNullValue = value is { IsNull: false }
            ? value
            : null;
        return new MarkerArguments(
            dimension,
            comparison,
            ReadNamedScalar(marker, ArgumentName) as string,
            nonNullValue,
            ReadNamedScalar(marker, IsExclusiveName) is true,
            ReadNamedScalar(marker, ExclusiveArgumentName) as string
        );
    }
}

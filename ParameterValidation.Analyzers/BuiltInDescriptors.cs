using System.Collections.Generic;
using System.Collections.Immutable;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>What each of the built-in DataAnnotations validation attributes constrains.</summary>
/// <remarks>Only these exact classes: a derived class can change what it accepts.</remarks>
internal static class BuiltInDescriptors
{
    private const string DataAnnotations = ValidationAttributeTypes.DataAnnotationsNamespace;

    private static readonly ImmutableDictionary<string, ImmutableArray<Descriptor>> ByClassName =
        new Dictionary<string, ImmutableArray<Descriptor>>
        {
            [ValidationAttributeTypes.RequiredAttributeName] =
            [
                new(Dimensions.NotNull, ConstraintKind.Flag),
                new(Dimensions.NotBlank, ConstraintKind.Flag, UnlessArgument: "AllowEmptyStrings"),
            ],
            [DataAnnotations + "MinLengthAttribute"] = [new(Dimensions.Length, ConstraintKind.LowerBound, "length")],

            // MaxLength() means no maximum.
            [DataAnnotations + "MaxLengthAttribute"] =
            [
                new(Dimensions.Length, ConstraintKind.UpperBound, "length", double.PositiveInfinity),
            ],
            [DataAnnotations + "LengthAttribute"] =
            [
                new(Dimensions.Length, ConstraintKind.LowerBound, "minimumLength"),
                new(Dimensions.Length, ConstraintKind.UpperBound, "maximumLength"),
            ],
            [DataAnnotations + "StringLengthAttribute"] =
            [
                new(Dimensions.Length, ConstraintKind.LowerBound, "MinimumLength", 0.0),
                new(Dimensions.Length, ConstraintKind.UpperBound, "maximumLength"),
            ],

            // Range(typeof(T), "min", "max")'s string bounds are read by RangeLimitParser.
            [ValidationAttributeTypes.RangeAttributeName] =
            [
                new(Dimensions.Value, ConstraintKind.LowerBound, "minimum", ExclusiveArgument: "MinimumIsExclusive"),
                new(Dimensions.Value, ConstraintKind.UpperBound, "maximum", ExclusiveArgument: "MaximumIsExclusive"),
            ],
            [DataAnnotations + "AllowedValuesAttribute"] =
            [
                new(Dimensions.Value, ConstraintKind.AllowedValues, "values"),
            ],
            [DataAnnotations + "DeniedValuesAttribute"] =
            [
                new(Dimensions.Value, ConstraintKind.DeniedValues, "values"),
            ],
        }.ToImmutableDictionary();

    /// <returns>The class's descriptors, or <c>null</c> if it isn't one of the built-in attributes.</returns>
    public static ImmutableArray<Descriptor>? Find(string className)
        => ByClassName.TryGetValue(className, out ImmutableArray<Descriptor> descriptors)
            ? descriptors
            : null;
}

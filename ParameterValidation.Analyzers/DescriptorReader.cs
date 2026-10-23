using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>
/// Reads one <c>[ComparableValidation]</c> describing a validation attribute class, checking it
/// against one rule after another; the first rule broken is the reason it is invalid.
/// </summary>
internal static class DescriptorReader
{
    private const string MissingDimensionOrComparison = "it needs a dimension and a comparison";

    private static readonly ImmutableArray<Func<MarkerArguments, ITypeSymbol, string?>> CommonRules =
    [
        RejectEmptyDimension,
        RejectUndefinedComparison,
        RejectNonValidationTarget,
    ];

    private static readonly ImmutableArray<Func<MarkerArguments, ITypeSymbol, string?>> FlagRules =
    [
        RejectFlagArguments,
    ];

    private static readonly ImmutableArray<Func<MarkerArguments, ITypeSymbol, string?>> LimitRules =
    [
        RequireArgumentOrValue,
        RequireArgumentMember,
        RequireValueOfKind,
        RejectExclusiveValues,
        RequireExclusiveArgumentMember,
    ];

    /// <param name="marker">The <c>[ComparableValidation]</c>.</param>
    /// <param name="target">The validation attribute class the marker describes.</param>
    public static DescriptorReading Read(AttributeData marker, ITypeSymbol target)
    {
        MarkerArguments? arguments = MarkerArguments.Read(marker);
        return arguments is null
            ? DescriptorReading.Invalid(MissingDimensionOrComparison)
            : Validate(arguments, target);
    }

    private static DescriptorReading Validate(MarkerArguments arguments, ITypeSymbol target)
    {
        string? error = FindError(CommonRules, arguments, target) ?? FindKindError(arguments, target);
        return error is null
            ? DescriptorReading.Valid(arguments.ToDescriptor())
            : DescriptorReading.Invalid(error);
    }

    /// <remarks>Only called once the comparison is known to be defined.</remarks>
    private static string? FindKindError(MarkerArguments arguments, ITypeSymbol target)
    {
        ImmutableArray<Func<MarkerArguments, ITypeSymbol, string?>> rules = arguments.Kind == ConstraintKind.Flag
            ? FlagRules
            : LimitRules;
        return FindError(rules, arguments, target);
    }

    private static string? FindError(
        ImmutableArray<Func<MarkerArguments, ITypeSymbol, string?>> rules,
        MarkerArguments arguments,
        ITypeSymbol target
    )
        => rules
            .Select(rule => rule(arguments, target))
            .FirstOrDefault(static error => error is not null);

    private static string? RejectEmptyDimension(MarkerArguments arguments, ITypeSymbol target)
        => arguments.Dimension.Trim().Length == 0
            ? "the dimension is empty"
            : null;

    private static string? RejectUndefinedComparison(MarkerArguments arguments, ITypeSymbol target)
        => arguments.IsDefinedKind
            ? null
            : $"{arguments.Comparison} isn't a ValidationComparison";

    private static string? RejectNonValidationTarget(MarkerArguments arguments, ITypeSymbol target)
        => ValidationAttributeTypes.IsValidationAttribute(target)
            ? null
            : $"'{target.ToDisplayString()}' isn't a ValidationAttribute";

    private static string? RejectFlagArguments(MarkerArguments arguments, ITypeSymbol target)
        => arguments is { Argument: null, ValueConstant: null, IsExclusive: false, ExclusiveArgument: null }
            ? null
            : "a Flag takes no Argument, Value, IsExclusive or ExclusiveArgument";

    private static string? RequireArgumentOrValue(MarkerArguments arguments, ITypeSymbol target)
        => arguments is { Argument: null, ValueConstant: null }
            ? $"a {arguments.Kind} needs an Argument or a Value"
            : null;

    private static string? RequireArgumentMember(MarkerArguments arguments, ITypeSymbol target)
        => arguments.Argument is { } argument
            && !AttributeMembers.HasArgument(target, argument, isBooleanRequired: false)
            ? $"'{target.Name}' has no constructor parameter or settable property named '{argument}'"
            : null;

    private static string? RequireValueOfKind(MarkerArguments arguments, ITypeSymbol target)
    {
        string expected = arguments.IsBound
            ? "a number"
            : "an array";
        return arguments.ValueConstant is not null && arguments.Value is null
            ? $"the Value of a {arguments.Kind} must be {expected}"
            : null;
    }

    private static string? RejectExclusiveValues(MarkerArguments arguments, ITypeSymbol target)
        => !arguments.IsBound && (arguments.IsExclusive || arguments.ExclusiveArgument is not null)
            ? "only a LowerBound or an UpperBound can be exclusive"
            : null;

    private static string? RequireExclusiveArgumentMember(MarkerArguments arguments, ITypeSymbol target)
        => arguments.ExclusiveArgument is { } argument
            && !AttributeMembers.HasArgument(target, argument, isBooleanRequired: true)
            ? $"'{target.Name}' has no bool constructor parameter or settable property named '{argument}'"
            : null;
}

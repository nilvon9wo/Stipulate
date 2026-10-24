using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>Works out one attribute usage's constraints from its class's descriptors and its arguments.</summary>
internal static class ConstraintEvaluator
{
    /// <returns>
    /// The usage's constraints, or <c>null</c> if an argument one of them depends on isn't a constant
    /// of the right kind.
    /// </returns>
    public static ImmutableArray<Constraint>? Evaluate(ImmutableArray<Descriptor> descriptors, AttributeData attribute)
    {
        List<ImmutableArray<Constraint>?> evaluated =
            [.. descriptors.Select(descriptor => Evaluate(descriptor, attribute))];
        bool isComparable = evaluated.All(static constraints => constraints is not null);
        return isComparable
            ? [.. evaluated.SelectMany(static constraints => constraints!.Value)]
            : null;
    }

    /// <returns>
    /// The descriptor's constraint, none if the usage removes it, or <c>null</c> if it can't be evaluated.
    /// </returns>
    private static ImmutableArray<Constraint>? Evaluate(Descriptor descriptor, AttributeData attribute)
        => descriptor.Kind == ConstraintKind.Flag
            ? EvaluateFlag(descriptor, attribute)
            : EvaluateLimit(descriptor, attribute);

    private static ImmutableArray<Constraint> EvaluateFlag(Descriptor descriptor, AttributeData attribute)
    {
        bool isRemoved = descriptor.UnlessArgument is { } unless
            && AttributeArguments.Find(attribute, unless) is { Value: true };
        return isRemoved
            ? []
            : [Constraint.Flag(descriptor.Dimension)];
    }

    private static ImmutableArray<Constraint>? EvaluateLimit(Descriptor descriptor, AttributeData attribute)
    {
        bool isExclusive = IsExclusive(descriptor, attribute);
        return ReadValue(descriptor, attribute) switch
        {
            double bound => [new Constraint(descriptor.Dimension, descriptor.Kind, bound, isExclusive, [])],
            ParsedBound parsed => [ToConstraint(descriptor, parsed, isExclusive)],
            ImmutableArray<string> values => [ToConstraint(descriptor, values, FindConstants(descriptor, attribute))],
            _ => null,
        };
    }

    private static Constraint ToConstraint(Descriptor descriptor, ParsedBound parsed, bool isExclusive)
        => new(descriptor.Dimension, descriptor.Kind, parsed.Value, isExclusive, [], parsed.Display);

    private static Constraint ToConstraint(
        Descriptor descriptor,
        ImmutableArray<string> values,
        ImmutableArray<TypedConstant> constants
    )
        => new(descriptor.Dimension, descriptor.Kind, 0, IsExclusive: false, values, Constants: constants);

    /// <returns>
    /// The usage's array of values, or default if the values are the descriptor's own. Only called
    /// for values read from an array, either the usage's or else the descriptor's.
    /// </returns>
    private static ImmutableArray<TypedConstant> FindConstants(Descriptor descriptor, AttributeData attribute)
        => FindArgument(descriptor, attribute) is { } array
            ? array.Values
            : default;

    private static TypedConstant? FindArgument(Descriptor descriptor, AttributeData attribute)
        => descriptor.Argument is { } name
            ? AttributeArguments.Find(attribute, name)
            : null;

    /// <summary>The usage's argument, converted for the kind, or else the descriptor's default.</summary>
    private static object? ReadValue(Descriptor descriptor, AttributeData attribute)
        => FindArgument(descriptor, attribute) is { } constant
            ? ConvertArgument(descriptor.Kind, constant, attribute)
            : descriptor.Value;

    /// <summary>A bound can also be one of <c>Range(typeof(T), ...)</c>'s string limits.</summary>
    private static object? ConvertArgument(ConstraintKind kind, TypedConstant constant, AttributeData attribute)
        => ConstantConversion.ToValue(constant, kind) ?? RangeLimitParser.Parse(constant, attribute);

    private static bool IsExclusive(Descriptor descriptor, AttributeData attribute)
    {
        TypedConstant? argument = descriptor.ExclusiveArgument is { } name
            ? AttributeArguments.Find(attribute, name)
            : null;
        return argument is { Value: bool isExclusive }
            ? isExclusive
            : descriptor.IsExclusive;
    }
}

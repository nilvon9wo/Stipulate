using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

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

/// <summary>
/// How an attribute class constrains one dimension, before any particular usage's arguments are
/// known: built in for the DataAnnotations attributes, or read from a <c>[ComparableValidation]</c>.
/// </summary>
/// <param name="Value">The bound (<see cref="double"/>) or values (<see cref="ImmutableArray{T}"/> of string) to use when <paramref name="Argument"/> isn't supplied.</param>
/// <param name="UnlessArgument">For a flag: the <see cref="bool"/> argument that, when true, removes it.</param>
internal sealed record Descriptor(
	string Dimension,
	ConstraintKind Kind,
	string? Argument = null,
	object? Value = null,
	bool IsExclusive = false,
	string? ExclusiveArgument = null,
	string? UnlessArgument = null);

/// <summary>One usage's constraint on one dimension.</summary>
/// <param name="Values">For <see cref="ConstraintKind.AllowedValues"/> and <see cref="ConstraintKind.DeniedValues"/>: the values, as C# literals.</param>
/// <param name="BoundDisplay">How to show <paramref name="Bound"/>, if not as a number: e.g. a date, as written.</param>
internal sealed record Constraint(string Dimension, ConstraintKind Kind, double Bound, bool IsExclusive, ImmutableArray<string> Values, string? BoundDisplay = null);

/// <summary>A bound read from a string, such as <c>Range(typeof(DateTime), "2020-01-01", ...)</c>'s.</summary>
/// <param name="Display">The string as written.</param>
internal sealed record ParsedBound(double Value, string Display);

/// <summary>A validation attribute on a parameter, with what it constrains.</summary>
/// <param name="Key">Equal for two usages of the same attribute class with the same arguments.</param>
/// <param name="Constraints">The constraints, or <c>null</c> if the usage can't be compared.</param>
internal sealed record DescribedAttribute(AttributeData Data, string Key, string Display, ImmutableArray<Constraint>? Constraints);

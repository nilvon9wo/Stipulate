using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace ParameterValidation.Analyzers;

internal enum ProblemKind
{
	/// <summary>The implementation's attribute rejects something the contract accepts.</summary>
	Strengthened,

	/// <summary>The implementation's attribute can't be compared with the contract.</summary>
	NotComparable,

	/// <summary>The implementation doesn't restate the contract's attribute.</summary>
	Dropped,
}

/// <param name="Attribute">The implementation's attribute, or for <see cref="ProblemKind.Dropped"/> the contract's.</param>
/// <param name="Detail">For <see cref="ProblemKind.Strengthened"/>: what the attribute rejects that the contract accepts.</param>
internal sealed record Problem(ProblemKind Kind, DescribedAttribute Attribute, string Detail = "");

/// <summary>
/// Checks that one parameter's validation attributes in an implementation accept everything that
/// the corresponding parameter's attributes in its contract accept.
/// </summary>
/// <remarks>
/// A parameter's attributes all have to accept a value, so the contract's constraints on a
/// dimension combine into the strongest of them; each of the implementation's constraints must be
/// no stronger than that. Undescribed attributes in the contract are ignored, which can only
/// produce extra reports, never miss one.
/// </remarks>
internal static class PreconditionComparer
{
	/// <param name="implementationAcceptsNull">
	/// Whether the implementation's parameter is annotated as nullable, which explicitly drops a
	/// contract's <c>[Required]</c>.
	/// </param>
	public static IEnumerable<Problem> Compare(
		ImmutableArray<DescribedAttribute> contract,
		ImmutableArray<DescribedAttribute> implementation,
		bool implementationAcceptsNull)
	{
		HashSet<string> contractKeys = [.. contract.Select(static attribute => attribute.Key)];
		List<Constraint> contractConstraints = [.. contract.SelectMany(static attribute => attribute.Constraints ?? [])];

		foreach (DescribedAttribute attribute in implementation)
		{
			if (contractKeys.Contains(attribute.Key))
			{
				continue;
			}

			if (attribute.Constraints is not { } constraints)
			{
				yield return new Problem(ProblemKind.NotComparable, attribute);
				continue;
			}

			if (constraints.Select(constraint => FindStrengthening(constraint, contractConstraints)).FirstOrDefault(static detail => detail is not null) is { } strengthening)
			{
				yield return new Problem(ProblemKind.Strengthened, attribute, $"[{attribute.Display}] {strengthening}");
			}
		}

		HashSet<string> implementationKeys = [.. implementation.Select(static attribute => attribute.Key)];
		HashSet<string> implementationDimensions = [.. implementation.SelectMany(static attribute => attribute.Constraints ?? []).Select(static constraint => constraint.Dimension)];
		if (implementationAcceptsNull)
		{
			implementationDimensions.Add(Dimensions.NotNull);
			implementationDimensions.Add(Dimensions.NotBlank);
		}

		foreach (DescribedAttribute attribute in contract)
		{
			// Restated exactly; or the same class with other arguments, already reported if it can't
			// be compared; or a deliberate statement about the same dimension, such as MinLength(0).
			bool addressed = implementationKeys.Contains(attribute.Key)
				|| implementation.Any(other => SymbolEqualityComparer.Default.Equals(other.Data.AttributeClass?.OriginalDefinition, attribute.Data.AttributeClass?.OriginalDefinition))
				|| attribute.Constraints is { IsEmpty: false } constraints && constraints.Any(constraint => implementationDimensions.Contains(constraint.Dimension));
			if (!addressed)
			{
				yield return new Problem(ProblemKind.Dropped, attribute);
			}
		}
	}

	/// <returns>What <paramref name="constraint"/> rejects that the contract accepts, or <c>null</c> if nothing.</returns>
	private static string? FindStrengthening(Constraint constraint, List<Constraint> contract)
	{
		if (IsVacuous(constraint))
		{
			return null;
		}

		List<Constraint> sameDimension = [.. contract.Where(other => other.Dimension == constraint.Dimension)];
		List<Constraint> sameKind = [.. sameDimension.Where(other => other.Kind == constraint.Kind)];
		switch (constraint.Kind)
		{
			case ConstraintKind.Flag:
				return sameKind.Count > 0 ? null : constraint.Dimension switch
				{
					Dimensions.NotNull => "rejects null, which the contract accepts",
					Dimensions.NotBlank => "rejects empty and whitespace-only strings, which the contract accepts",
					_ => $"imposes '{constraint.Dimension}', which the contract doesn't",
				};

			case ConstraintKind.LowerBound:
			case ConstraintKind.UpperBound:
				bool isLower = constraint.Kind == ConstraintKind.LowerBound;
				if (sameKind.Count == 0)
				{
					return $"requires {DescribeBound(constraint)}, but the contract has no {(isLower ? "lower" : "upper")} bound on {constraint.Dimension}";
				}

				Constraint strongest = sameKind.Aggregate((a, b) => IsStronger(b, a) ? b : a);
				return IsStronger(constraint, strongest)
					? $"requires {DescribeBound(constraint)}, but the contract accepts {DescribeBound(strongest)}"
					: null;

			case ConstraintKind.AllowedValues:
			{
				List<Constraint> allowed = sameKind;
				if (allowed.Count == 0)
				{
					return $"accepts only {string.Join(", ", constraint.Values)}, but the contract doesn't limit {constraint.Dimension} to a list";
				}

				HashSet<string> denied = [.. sameDimension.Where(static other => other.Kind == ConstraintKind.DeniedValues).SelectMany(static other => other.Values)];
				string? missing = Intersect(allowed).FirstOrDefault(value => !constraint.Values.Contains(value) && !denied.Contains(value));
				return missing is null ? null : $"rejects {missing}, which the contract accepts";
			}

			case ConstraintKind.DeniedValues:
			{
				HashSet<string> denied = [.. sameKind.SelectMany(static other => other.Values)];
				List<Constraint> allowed = [.. sameDimension.Where(static other => other.Kind == ConstraintKind.AllowedValues)];
				HashSet<string>? accepted = allowed.Count == 0 ? null : Intersect(allowed);
				string? extra = constraint.Values.FirstOrDefault(value => !denied.Contains(value) && (accepted is null || accepted.Contains(value)));
				return extra is null ? null : $"rejects {extra}, which the contract accepts";
			}

			default:
				return null;
		}
	}

	/// <summary>
	/// Whether a bound rejects nothing: <c>MaxLength()</c>'s infinite maximum, or a minimum length
	/// of 0 such as <c>StringLength</c>'s default <c>MinimumLength</c>.
	/// </summary>
	private static bool IsVacuous(Constraint constraint)
		=> constraint.Kind is ConstraintKind.LowerBound or ConstraintKind.UpperBound
			&& (double.IsInfinity(constraint.Bound)
				|| constraint is { Dimension: Dimensions.Length, Kind: ConstraintKind.LowerBound }
					&& (constraint.Bound < 0 || constraint.Bound == 0 && !constraint.IsExclusive));

	/// <summary>The values that every one of the allowed-values constraints accepts.</summary>
	private static HashSet<string> Intersect(List<Constraint> allowed)
	{
		HashSet<string> result = [.. allowed[0].Values];
		foreach (Constraint other in allowed.Skip(1))
		{
			result.IntersectWith(other.Values);
		}

		return result;
	}

	/// <summary>Whether bound <paramref name="a"/> rejects something that bound <paramref name="b"/> of the same kind accepts.</summary>
	private static bool IsStronger(Constraint a, Constraint b)
	{
		if (a.Bound != b.Bound)
		{
			return a.Kind == ConstraintKind.LowerBound ? a.Bound > b.Bound : a.Bound < b.Bound;
		}

		return a.IsExclusive && !b.IsExclusive;
	}

	private static string DescribeBound(Constraint bound)
	{
		string comparison = (bound.Kind, bound.IsExclusive) switch
		{
			(ConstraintKind.LowerBound, false) => ">=",
			(ConstraintKind.LowerBound, true) => ">",
			(_, false) => "<=",
			(_, true) => "<",
		};
		string value = double.IsInfinity(bound.Bound) ? "infinity" : bound.Bound.ToString("R", CultureInfo.InvariantCulture);
		return $"{bound.Dimension} {comparison} {value}";
	}
}

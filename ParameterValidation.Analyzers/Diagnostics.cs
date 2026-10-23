using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <remarks>
/// In the messages, {0} is the implementation's parameter or property (e.g. "Parameter 'array' of
/// 'Multiplier.Multiply(int[])'") and {1} the contract it implements or overrides.
/// </remarks>
internal static class Diagnostics
{
	private const string Category = "ParameterValidation";

	public static readonly DiagnosticDescriptor Strengthened = new(
		"PV1001",
		"Implementation strengthens its contract's precondition",
		"{0} strengthens the precondition of '{1}': {2}",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "An implementation or override must accept every argument its contract accepts (the Liskov substitution principle): its validation attributes may be weaker than the contract's, but not stronger.");

	public static readonly DiagnosticDescriptor NotComparableInSolution = new(
		"PV1002",
		"Validation attribute can't be compared with the contract",
		"{0}: [{2}] can't be compared with the attributes of '{1}'. Describe '{3}' with [ComparableValidation], or repeat the contract's attribute exactly.",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The analyzer can only tell whether an attribute is weaker than the contract if it knows what the attribute constrains. The attribute is declared in this solution, so its class can be given a [ComparableValidation] description.");

	public static readonly DiagnosticDescriptor Dropped = new(
		RestateProperties.DroppedId,
		"Implementation doesn't restate its contract's validation attribute",
		"{0} doesn't restate [{2}] from '{1}'. Restate it, or state a weaker constraint for each of its bounds to accept more on purpose.",
		Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Dropping a validation attribute is a legal weakening, but it should be explicit so that the implementation's signature shows what it accepts. State a weaker constraint for each of its bounds (e.g. [MinLength(0)] for a lower bound on length, [MaxLength] for an upper one), or for [Required] make the parameter or property nullable.");

	public static readonly DiagnosticDescriptor NotComparableExternal = new(
		"PV1005",
		"Validation attribute from outside the solution can't be compared with the contract",
		"{0}: [{2}] can't be compared with the attributes of '{1}', because '{3}' comes from '{4}', outside this solution. Repeat the contract's attribute exactly, or describe it with [assembly: ComparableValidation(typeof({3}), ...)].",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The analyzer can only tell whether an attribute is weaker than the contract if it knows what the attribute constrains. The attribute's class belongs to another package and can't be changed, but it can be described from an assembly in this solution.");

	public static readonly DiagnosticDescriptor InvalidDescription = new(
		"PV1006",
		"Invalid [ComparableValidation]",
		"This [ComparableValidation] is invalid because {0}",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "An attribute class with an invalid description is treated as undescribed, so it can only be compared with identical usages.");

	public static readonly DiagnosticDescriptor Weakened = new(
		RestateProperties.WeakenedId,
		"Implementation weakens its contract's guarantee",
		"{0} weakens the guarantee of '{1}': {2}",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "What an implementation hands back (a property's value, an out or ref parameter) must satisfy everything its contract promises: its validation attributes may be stronger than the contract's, but not weaker or missing. A property with both a getter and a setter, or a ref parameter, both takes and hands back values, so its attributes must match its contract's.");
}

namespace ParameterValidation.Analyzers;

/// <summary>
/// Set on a missing attribute's diagnostic (PV1003, or PV1007 when the attribute is missing
/// altogether) to tell ParameterValidation.CodeFixes, which links this file, which of the contract's
/// attributes to restate.
/// </summary>
internal static class RestateProperties
{
	public const string DroppedId = "PV1003";
	public const string WeakenedId = "PV1007";

	/// <summary>The contract member's documentation comment ID.</summary>
	public const string Contract = "Contract";

	/// <summary>The parameter's ordinal, or <see cref="PropertyValue"/>.</summary>
	public const string Target = "Target";

	/// <summary>The attribute's index in the target's attributes.</summary>
	public const string AttributeIndex = "AttributeIndex";

	/// <summary>The attribute as the messages show it.</summary>
	public const string AttributeDisplay = "AttributeDisplay";

	/// <summary>The <see cref="Target"/> for a property's own attributes.</summary>
	public const string PropertyValue = "value";
}

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// Set on a missing attribute's diagnostic (PV1002, or PV1003 when the attribute is missing
/// altogether) to tell ParameterValidation.CodeFixes, which links this file, which of the contract's
/// attributes to restate.
/// </summary>
internal static class RestateProperties
{
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

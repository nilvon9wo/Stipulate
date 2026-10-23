namespace Net.NowhereAtAll.ParameterValidation.Generator;

/// <summary>The validation attributes to check one parameter against.</summary>
/// <param name="AttributeConstructions">C# expressions that construct each attribute as declared.</param>
internal sealed record ParameterCheck(string ParameterName, EquatableArray<string> AttributeConstructions);

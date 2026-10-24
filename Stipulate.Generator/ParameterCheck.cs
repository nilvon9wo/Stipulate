namespace Net.NowhereAtAll.Stipulate.Generator;

/// <summary>The validation attributes to check one parameter against.</summary>
/// <param name="AttributeConstructions">C# expressions that construct each attribute as declared.</param>
/// <param name="ShowsValue">Whether the parameter has <c>[ShowValueInMessage]</c>.</param>
internal sealed record ParameterCheck(
    string ParameterName,
    EquatableArray<string> AttributeConstructions,
    bool ShowsValue
);

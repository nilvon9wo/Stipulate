using Mono.Cecil;

namespace ParameterValidation.Weaver;

/// <summary>One validation attribute to check one parameter against.</summary>
internal sealed record WovenCheck(
    ParameterDefinition Parameter,
    CustomAttribute Attribute,
    TypeDefinition AttributeType
);

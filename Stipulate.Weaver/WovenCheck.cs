using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>One validation attribute to check one parameter against.</summary>
internal sealed record WovenCheck(
    ParameterDefinition Parameter,
    CustomAttribute Attribute,
    TypeDefinition AttributeType
);

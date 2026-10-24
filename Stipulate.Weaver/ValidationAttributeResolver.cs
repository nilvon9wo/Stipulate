using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>Recognises the validation attributes among a parameter's custom attributes.</summary>
internal static class ValidationAttributeResolver
{
    /// <returns>
    /// The attribute's type definition if it derives from <c>ValidationAttribute</c>; otherwise <c>null</c>.
    /// </returns>
    public static TypeDefinition? Resolve(CustomAttribute attribute)
    {
        TypeDefinition attributeType = CecilTypeHierarchy.Resolve(attribute.AttributeType);
        return CecilTypeHierarchy.SelfAndBaseTypes(attributeType)
            .Any(static type => type.FullName == WellKnownReferences.ValidationAttributeName)
            ? attributeType
            : null;
    }
}

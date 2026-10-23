using Mono.Cecil;

namespace ParameterValidation.Weaver;

/// <summary>Recognises the validation attributes among a parameter's custom attributes.</summary>
internal sealed class ValidationAttributeResolver(WellKnownReferences references)
{
    private readonly WellKnownReferences _references = references;

    /// <returns>
    /// The attribute's type definition if it derives from <c>ValidationAttribute</c>; otherwise <c>null</c>.
    /// </returns>
    public TypeDefinition? Resolve(CustomAttribute attribute)
    {
        TypeDefinition attributeType = CecilTypeHierarchy.Resolve(attribute.AttributeType);
        TypeDefinition? validationAttribute = CecilTypeHierarchy.SelfAndBaseTypes(attributeType)
            .FirstOrDefault(static type => type.FullName == WellKnownReferences.ValidationAttributeName);
        if (validationAttribute is not null)
        {
            this._references.EnsureValidationAttribute(validationAttribute);
        }

        return validationAttribute is null
            ? null
            : attributeType;
    }

    /// <summary>
    /// Whether a <c>null</c> rejected by the attribute should be reported as an <c>ArgumentNullException</c>.
    /// </summary>
    public static bool IsRequiredAttribute(TypeDefinition attributeType)
        => CecilTypeHierarchy.SelfAndBaseTypes(attributeType)
            .Any(static type => type.FullName == WellKnownReferences.RequiredAttributeName);
}

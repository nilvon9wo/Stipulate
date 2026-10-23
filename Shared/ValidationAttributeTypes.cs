using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.ParameterValidation;

/// <summary>
/// Recognises <c>System.ComponentModel.DataAnnotations</c> validation attributes. Linked into each
/// Roslyn component, as <see cref="TypeHierarchy"/> is.
/// </summary>
internal static class ValidationAttributeTypes
{
    public const string DataAnnotationsNamespace = "System.ComponentModel.DataAnnotations.";
    public const string ValidationAttributeName = DataAnnotationsNamespace + "ValidationAttribute";
    public const string RequiredAttributeName = DataAnnotationsNamespace + "RequiredAttribute";
    public const string RangeAttributeName = DataAnnotationsNamespace + "RangeAttribute";

    /// <summary>Whether <paramref name="type"/> is, or derives from, <c>ValidationAttribute</c>.</summary>
    public static bool IsValidationAttribute(ITypeSymbol? type)
        => TypeHierarchy.SelfAndBaseTypes(type)
            .Any(static current => current.ToDisplayString() == ValidationAttributeName);
}

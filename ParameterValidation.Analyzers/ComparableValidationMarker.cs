using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// Recognises usages of <c>Net.NowhereAtAll.ParameterValidation.Contracts.ComparableValidationAttribute</c>, in its
/// two forms: on a validation attribute class, taking a dimension and a comparison; or on an
/// assembly, taking the type it describes first.
/// </summary>
internal static class ComparableValidationMarker
{
    public const string Name = "Net.NowhereAtAll.ParameterValidation.Contracts.ComparableValidationAttribute";
    public const string ContractsAssemblyName = "Net.NowhereAtAll.ParameterValidation.Contracts";

    private const int ClassFormArgumentCount = 2;
    private const int AssemblyFormArgumentCount = 3;

    public static bool IsMarker(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() == Name;

    public static bool IsClassForm(AttributeData marker)
        => marker.ConstructorArguments.Length == ClassFormArgumentCount;

    public static bool IsAssemblyForm(AttributeData marker)
        => marker.ConstructorArguments.Length == AssemblyFormArgumentCount;

    /// <returns>The type an assembly-form marker describes, or <c>null</c> if it isn't one.</returns>
    public static ITypeSymbol? FindDescribedType(AttributeData marker)
        => IsAssemblyForm(marker)
            ? AttributeArguments.ScalarValue(marker.ConstructorArguments[0]) as ITypeSymbol
            : null;
}

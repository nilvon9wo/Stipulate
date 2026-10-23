using System.Linq;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>Finds the arguments an attribute class can be given.</summary>
internal static class AttributeMembers
{
    /// <summary>
    /// Whether <paramref name="attributeClass"/> has a constructor parameter, or a settable
    /// property or field, called <paramref name="name"/>.
    /// </summary>
    /// <param name="attributeClass">The attribute class.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="isBooleanRequired">Whether the argument must be a <see cref="bool"/>.</param>
    public static bool HasArgument(INamedTypeSymbol attributeClass, string name, bool isBooleanRequired)
    {
        ArgumentType argumentType = new(isBooleanRequired);
        return argumentType.HasSettableMember(attributeClass, name)
            || argumentType.HasConstructorParameter(attributeClass, name);
    }
}

/// <summary>The type an argument must have: any, or <see cref="bool"/>.</summary>
file sealed class ArgumentType(bool isBooleanRequired)
{
    private readonly bool _isBooleanRequired = isBooleanRequired;

    public bool HasSettableMember(ITypeSymbol attributeClass, string name)
        => TypeHierarchy.SelfAndBaseTypes(attributeClass)
            .SelectMany(type => type.GetMembers(name))
            .Any(this.IsSettable);

    public bool HasConstructorParameter(INamedTypeSymbol attributeClass, string name)
        => attributeClass.InstanceConstructors
                .SelectMany(static constructor => constructor.Parameters)
                .Any(parameter => parameter.Name == name && this.Fits(parameter.Type));

    private bool Fits(ITypeSymbol type) => !this._isBooleanRequired || type.SpecialType == SpecialType.System_Boolean;

    private bool IsSettable(ISymbol member) => member switch
    {
        IPropertySymbol property => IsSettable(property) && this.Fits(property.Type),
        IFieldSymbol field => IsSettable(field) && this.Fits(field.Type),
        _ => false,
    };

    private static bool IsSettable(IPropertySymbol property) => property is { IsStatic: false, SetMethod: not null };

    /// <remarks>A constant is static, so it isn't settable either.</remarks>
    private static bool IsSettable(IFieldSymbol field) => field is { IsStatic: false, IsReadOnly: false };
}

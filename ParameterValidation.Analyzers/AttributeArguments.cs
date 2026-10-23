using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>Reads an attribute usage's arguments by name.</summary>
internal static class AttributeArguments
{
    /// <summary>
    /// The value given for a named argument, or else for a constructor parameter, called
    /// <paramref name="name"/>; or <c>null</c> if the usage gives neither.
    /// </summary>
    public static TypedConstant? Find(AttributeData attribute, string name)
        => FindNamed(attribute, name) ?? FindConstructorArgument(attribute, name);

    /// <summary>
    /// The constant's value, or <c>null</c> if it is an array: <see cref="TypedConstant.Value"/>
    /// throws for an array, and an argument can be one wherever an attribute class says so.
    /// </summary>
    public static object? ScalarValue(TypedConstant constant)
        => constant.Kind == TypedConstantKind.Array
            ? null
            : constant.Value;

    /// <summary>The value given for the named argument <paramref name="name"/>, or <c>null</c> if none is.</summary>
    public static TypedConstant? FindNamed(AttributeData attribute, string name)
        => attribute.NamedArguments
            .Where(named => named.Key == name)
            .Select(static named => (TypedConstant?)named.Value)
            .FirstOrDefault();

    private static TypedConstant? FindConstructorArgument(AttributeData attribute, string name)
    {
        ImmutableArray<IParameterSymbol> parameters = attribute.AttributeConstructor?.Parameters ?? [];
        int count = Math.Min(parameters.Length, attribute.ConstructorArguments.Length);
        return Enumerable.Range(0, count)
            .Where(index => parameters[index].Name == name)
            .Select(index => (TypedConstant?)attribute.ConstructorArguments[index])
            .FirstOrDefault();
    }
}

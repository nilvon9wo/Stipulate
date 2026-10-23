using Mono.Cecil;

namespace ParameterValidation.Weaver;

/// <summary>Walks a type definition and its base types, resolving each.</summary>
internal static class CecilTypeHierarchy
{
    public static IEnumerable<TypeDefinition> SelfAndBaseTypes(TypeDefinition type)
    {
        for (TypeDefinition? current = type; current is not null; current = ResolveBase(current))
        {
            yield return current;
        }
    }

    /// <exception cref="WeavingException">The type can't be resolved from the references.</exception>
    public static TypeDefinition Resolve(TypeReference type)
        => type.Resolve()
            ?? throw new WeavingException($"Could not resolve type '{type.FullName}' from '{type.Scope.Name}'.");

    private static TypeDefinition? ResolveBase(TypeDefinition type)
        => type.BaseType is { } baseType
            ? Resolve(baseType)
            : null;
}

using Mono.Cecil;

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

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

    /// <exception cref="WeavingException">
    /// The type can't be resolved from the references: its assembly isn't among them, or doesn't declare it.
    /// </exception>
    public static TypeDefinition Resolve(TypeReference type)
    {
        try
        {
            return type.Resolve()
                ?? throw new WeavingException($"Could not resolve type '{type.FullName}' from '{type.Scope.Name}'.");
        }
        catch (AssemblyResolutionException exception)
        {
            throw new WeavingException(
                $"Could not resolve type '{type.FullName}': its assembly '{exception.AssemblyReference.FullName}' "
                + "is not among the references."
            );
        }
    }

    private static TypeDefinition? ResolveBase(TypeDefinition type)
        => type.BaseType is { } baseType
            ? Resolve(baseType)
            : null;
}

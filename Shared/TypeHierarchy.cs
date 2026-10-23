using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ParameterValidation;

/// <summary>
/// Walks a type and its base types. Linked into each Roslyn component (the analyzer, the code fix
/// and the generator), which can't share an assembly.
/// </summary>
internal static class TypeHierarchy
{
    public static IEnumerable<ITypeSymbol> SelfAndBaseTypes(ITypeSymbol? type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}

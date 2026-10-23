using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.ParameterValidation.Analyzers;

namespace Net.NowhereAtAll.ParameterValidation.CodeFixes;

/// <summary>Finds the contract's attribute that a diagnostic's <see cref="RestateProperties"/> point to.</summary>
internal static class ContractAttributeLocator
{
    /// <returns>The attribute, or <c>null</c> if the properties are missing or point nowhere.</returns>
    public static AttributeData? Find(Compilation compilation, ImmutableDictionary<string, string?> properties)
    {
        string? id = properties.GetValueOrDefault(RestateProperties.Contract);
        ISymbol? member = id is null
            ? null
            : DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);
        ISymbol? owner = FindOwner(member, properties.GetValueOrDefault(RestateProperties.Target));
        ImmutableArray<AttributeData> attributes = owner?.GetAttributes() ?? [];
        int? index = ParseIndex(properties.GetValueOrDefault(RestateProperties.AttributeIndex));
        return index < attributes.Length
            ? attributes[index.Value]
            : null;
    }

    /// <summary>The index of the attribute among its target's, if the diagnostic says.</summary>
    public static int? ParseIndex(string? text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            ? index
            : null;

    /// <returns>The property, or the parameter at the given ordinal, whose attribute is restated.</returns>
    private static ISymbol? FindOwner(ISymbol? member, string? target)
        => target == RestateProperties.PropertyValue
            ? member as IPropertySymbol
            : FindParameter(member, ParseIndex(target));

    private static IParameterSymbol? FindParameter(ISymbol? member, int? ordinal)
    {
        ImmutableArray<IParameterSymbol> parameters = member switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property => property.Parameters,
            _ => [],
        };
        return ordinal < parameters.Length
            ? parameters[ordinal.Value]
            : null;
    }
}

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>What the code fix needs to restate a missing attribute; see <see cref="RestateProperties"/>.</summary>
internal static class RestatePropertiesFactory
{
    /// <param name="target">The compared target the attribute is missing from.</param>
    /// <param name="attribute">One of the contract target's own attributes.</param>
    /// <remarks>Every method and property has a documentation comment ID.</remarks>
    public static ImmutableDictionary<string, string?> Create(ComparedTarget target, DescribedAttribute attribute)
    {
        string id = DocumentationCommentId.CreateDeclarationId(target.Contract.OriginalDefinition)!;
        return Create(id, target.ContractTarget, attribute);
    }

    private static ImmutableDictionary<string, string?> Create(
        string id,
        ISymbol contractTarget,
        DescribedAttribute attribute
    )
    {
        int index = contractTarget.GetAttributes().IndexOf(attribute.Data);
        string targetValue = contractTarget is IParameterSymbol parameter
            ? Format(parameter.Ordinal)
            : RestateProperties.PropertyValue;
        return ImmutableDictionary<string, string?>.Empty
            .Add(RestateProperties.Contract, id)
            .Add(RestateProperties.Target, targetValue)
            .Add(RestateProperties.AttributeIndex, Format(index))
            .Add(RestateProperties.AttributeDisplay, attribute.Display);
    }

    private static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);
}

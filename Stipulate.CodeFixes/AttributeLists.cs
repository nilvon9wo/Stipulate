using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Net.NowhereAtAll.Stipulate.CodeFixes;

/// <summary>The attribute lists of a parameter or a property.</summary>
internal static class AttributeLists
{
    /// <summary>The index <see cref="FindValidationList"/> returns when there is no such list.</summary>
    public const int None = -1;

    /// <param name="declaration">A parameter, or a property or indexer.</param>
    public static SyntaxList<AttributeListSyntax> Of(SyntaxNode declaration)
        => declaration is ParameterSyntax parameter
            ? parameter.AttributeLists
            : ((BasePropertyDeclarationSyntax)declaration).AttributeLists;

    /// <summary>
    /// The index of the last of <paramref name="declaration"/>'s attribute lists that holds only
    /// validation attributes, which a restated attribute joins; or <see cref="None"/> if there's
    /// none, so that it gets a list of its own rather than joining, say, a <c>[JsonIgnore]</c>.
    /// </summary>
    public static int FindValidationList(
        SyntaxNode declaration,
        SemanticModel model,
        CancellationToken cancellationToken
    )
    {
        SyntaxList<AttributeListSyntax> lists = Of(declaration);
        return Enumerable.Range(0, lists.Count)
            .Reverse()
            .Where(index => IsValidationList(lists[index], model, cancellationToken))
            .DefaultIfEmpty(None)
            .First();
    }

    private static bool IsValidationList(
        AttributeListSyntax list,
        SemanticModel model,
        CancellationToken cancellationToken
    )
        => list.Target is null
            && list.Attributes.All(attribute => IsValidationAttribute(attribute, model, cancellationToken));

    private static bool IsValidationAttribute(
        AttributeSyntax attribute,
        SemanticModel model,
        CancellationToken cancellationToken
    )
        => model.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol constructor
            && ValidationAttributeTypes.IsValidationAttribute(constructor.ContainingType);
}

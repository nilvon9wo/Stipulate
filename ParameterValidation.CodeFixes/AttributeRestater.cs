using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using ParameterValidation.Analyzers;

namespace ParameterValidation.CodeFixes;

/// <summary>Restates the contract's attributes that diagnostics point to on their declarations.</summary>
internal static class AttributeRestater
{
    /// <summary>Marks the attribute list that this edit restates attributes into.</summary>
    private static readonly SyntaxAnnotation RestatedList = new(nameof(RestatedList));

    public static async Task<Document> RestateAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken
    )
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        List<Restatement> restatements = [.. Order(diagnostics)
            .Select(diagnostic => FindRestatement(editor, diagnostic))
            .OfType<Restatement>()];
        foreach (Restatement restatement in restatements)
        {
            Restate(editor, restatement, cancellationToken);
        }

        return restatements.Count == 0
            ? document
            : await TidyAsync(editor.GetChangedDocument(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>In the order the contract declares them, whatever order the diagnostics come in.</summary>
    private static IEnumerable<Diagnostic> Order(ImmutableArray<Diagnostic> diagnostics)
        => diagnostics
            .OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(static diagnostic => GetAttributeIndex(diagnostic));

    private static int GetAttributeIndex(Diagnostic diagnostic)
    {
        string? text = diagnostic.Properties.GetValueOrDefault(RestateProperties.AttributeIndex);
        return ContractAttributeLocator.ParseIndex(text) ?? 0;
    }

    private static Restatement? FindRestatement(DocumentEditor editor, Diagnostic diagnostic)
    {
        SyntaxNode? declaration = FindDeclaration(editor.OriginalRoot, diagnostic);
        Compilation compilation = editor.SemanticModel.Compilation;
        AttributeData? attribute = ContractAttributeLocator.Find(compilation, diagnostic.Properties);
        return declaration is null || attribute is null
            ? null
            : new Restatement(declaration, attribute);
    }

    private static SyntaxNode? FindDeclaration(SyntaxNode root, Diagnostic diagnostic)
        => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
            .AncestorsAndSelf()
            .FirstOrDefault(static node => node is ParameterSyntax or BasePropertyDeclarationSyntax);

    private static void Restate(DocumentEditor editor, Restatement restatement, CancellationToken cancellationToken)
    {
        AttributeListSyntax generated = (AttributeListSyntax)editor.Generator.Attribute(restatement.Attribute);
        AttributeSyntax restated = generated.Attributes[0]
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation, Formatter.Annotation);
        int validationList =
            AttributeLists.FindValidationList(restatement.Declaration, editor.SemanticModel, cancellationToken);
        editor.ReplaceNode(
            restatement.Declaration,
            (current, generator) => AddAttribute(current, generator, generated, restated, validationList)
        );
    }

    /// <summary>Several fixes to one declaration share a list: the one they joined, or the first one's.</summary>
    private static SyntaxNode AddAttribute(
        SyntaxNode current,
        SyntaxGenerator generator,
        AttributeListSyntax generated,
        AttributeSyntax restated,
        int validationList
    )
    {
        SyntaxList<AttributeListSyntax> lists = AttributeLists.Of(current);
        AttributeListSyntax? joined = lists.FirstOrDefault(static list => list.HasAnnotation(RestatedList));
        AttributeListSyntax? target = joined ?? lists.ElementAtOrDefault(validationList);
        return target is null
            ? generator.AddAttributes(current, Annotate(generated.WithAttributes([restated])))
            : current.ReplaceNode(target, Annotate(target.AddAttributes(restated)));
    }

    private static AttributeListSyntax Annotate(AttributeListSyntax list)
        => list.WithAdditionalAnnotations(RestatedList, Formatter.Annotation);

    private static async Task<Document> TidyAsync(Document document, CancellationToken cancellationToken)
    {
        Document imported = await ImportAdder
            .AddImportsAsync(document, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Document reduced = await Simplifier
            .ReduceAsync(imported, Simplifier.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await Formatter
            .FormatAsync(reduced, Formatter.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}

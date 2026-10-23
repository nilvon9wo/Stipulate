using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using ParameterValidation.Analyzers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParameterValidation.CodeFixes;

/// <summary>
/// Restates a contract's validation attribute that an implementation leaves out (PV1003, or PV1007
/// when the attribute is missing altogether), adding a <c>using</c> for it if needed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RestateAttributeCodeFix)), Shared]
public sealed class RestateAttributeCodeFix : CodeFixProvider
{
	private const string EquivalenceKey = nameof(RestateAttributeCodeFix);

	public override ImmutableArray<string> FixableDiagnosticIds { get; } = [RestateProperties.DroppedId, RestateProperties.WeakenedId];

	public override FixAllProvider GetFixAllProvider() => new FixAll();

	public override Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			if (diagnostic.Properties.TryGetValue(RestateProperties.AttributeDisplay, out string? display))
			{
				context.RegisterCodeFix(
					CodeAction.Create(
						$"Restate [{display}] from the contract",
						cancellationToken => RestateAsync(context.Document, [diagnostic], cancellationToken),
						EquivalenceKey),
					diagnostic);
			}
		}

		return Task.CompletedTask;
	}

	private static async Task<Document> RestateAsync(Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
	{
		DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
		bool changed = false;

		// In the order the contract declares them, whatever order the diagnostics come in.
		foreach (Diagnostic diagnostic in diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(GetAttributeIndex))
		{
			if (editor.OriginalRoot.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.AncestorsAndSelf()
					.FirstOrDefault(static node => node is ParameterSyntax or BasePropertyDeclarationSyntax) is not { } declaration
				|| FindAttribute(editor.SemanticModel.Compilation, diagnostic.Properties) is not { } attribute)
			{
				continue;
			}

			editor.AddAttribute(declaration, editor.Generator.Attribute(attribute)
				.WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation, Formatter.Annotation));
			changed = true;
		}

		if (!changed)
		{
			return document;
		}

		Document result = await ImportAdder.AddImportsAsync(editor.GetChangedDocument(), Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken).ConfigureAwait(false);
		result = await Simplifier.ReduceAsync(result, Simplifier.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
		return await Formatter.FormatAsync(result, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static int GetAttributeIndex(Diagnostic diagnostic)
		=> diagnostic.Properties.TryGetValue(RestateProperties.AttributeIndex, out string? text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
			? index
			: 0;

	/// <summary>The contract's attribute that <paramref name="properties"/> point to; see <see cref="RestateProperties"/>.</summary>
	private static AttributeData? FindAttribute(Compilation compilation, ImmutableDictionary<string, string?> properties)
	{
		if (!properties.TryGetValue(RestateProperties.Contract, out string? id) || id is null
			|| !properties.TryGetValue(RestateProperties.Target, out string? target)
			|| !properties.TryGetValue(RestateProperties.AttributeIndex, out string? indexText)
			|| !int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
		{
			return null;
		}

		ISymbol? member = DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);
		ISymbol? owner = target == RestateProperties.PropertyValue
			? member as IPropertySymbol
			: int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal)
				? GetParameters(member).ElementAtOrDefault(ordinal)
				: null;
		ImmutableArray<AttributeData> attributes = owner?.GetAttributes() ?? [];
		return index < attributes.Length ? attributes[index] : null;
	}

	private static ImmutableArray<IParameterSymbol> GetParameters(ISymbol? member) => member switch
	{
		IMethodSymbol method => method.Parameters,
		IPropertySymbol property => property.Parameters,
		_ => [],
	};

	/// <summary>Restates every missing attribute in a document in one edit, so none are lost to conflicts.</summary>
	private sealed class FixAll : DocumentBasedFixAllProvider
	{
		protected override string GetFixAllTitle(FixAllContext fixAllContext) => "Restate the contract's attributes";

		protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
			=> await RestateAsync(document, diagnostics, fixAllContext.CancellationToken).ConfigureAwait(false);
	}
}

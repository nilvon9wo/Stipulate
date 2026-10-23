using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace ParameterValidation.CodeFixes;

/// <summary>Restates every missing attribute in a document in one edit, so none are lost to conflicts.</summary>
internal sealed class RestateAllFixer : DocumentBasedFixAllProvider
{
    private const string Title = "Restate the contract's attributes";

    protected override string GetFixAllTitle(FixAllContext fixAllContext) => Title;

    protected override async Task<Document?> FixAllAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> diagnostics
    )
        => await AttributeRestater
            .RestateAsync(document, diagnostics, fixAllContext.CancellationToken)
            .ConfigureAwait(false);
}

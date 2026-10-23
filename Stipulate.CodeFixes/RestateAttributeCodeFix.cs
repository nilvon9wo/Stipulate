using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Net.NowhereAtAll.ParameterValidation.Analyzers;

namespace Net.NowhereAtAll.ParameterValidation.CodeFixes;

/// <summary>
/// Restates a contract's validation attribute that an implementation leaves out (PV1002, or PV1003
/// when the attribute is missing altogether), adding a <c>using</c> for it if needed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RestateAttributeCodeFix))]
[Shared]
public sealed class RestateAttributeCodeFix : CodeFixProvider
{
    public const string EquivalenceKey = nameof(RestateAttributeCodeFix);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [DiagnosticIds.PreconditionDropped, DiagnosticIds.GuaranteeWeakened];

    public override FixAllProvider GetFixAllProvider() => new RestateAllFixer();

    /// <remarks>Only a diagnostic that says which attribute to restate can be fixed.</remarks>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics.Where(IsRestatable))
        {
            context.RegisterCodeFix(CreateAction(context.Document, diagnostic), diagnostic);
        }

        return Task.CompletedTask;
    }

    private static bool IsRestatable(Diagnostic diagnostic)
        => diagnostic.Properties.ContainsKey(RestateProperties.AttributeDisplay);

    private static CodeAction CreateAction(Document document, Diagnostic diagnostic)
        => CodeAction.Create(
            $"Restate [{diagnostic.Properties[RestateProperties.AttributeDisplay]}] from the contract",
            cancellationToken => AttributeRestater.RestateAsync(document, [diagnostic], cancellationToken),
            EquivalenceKey
        );
}

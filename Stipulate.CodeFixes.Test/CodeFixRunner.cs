using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Net.NowhereAtAll.ParameterValidation.Analyzers;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.CodeFixes.Test;

/// <summary>Applies <see cref="RestateAttributeCodeFix"/> to C# source in an in-memory workspace.</summary>
internal static class CodeFixRunner
{
    /// <summary>More rounds than any test needs: a fix that never converges fails rather than hangs.</summary>
    private const int MaxRounds = 10;

    private const string EditorConfig = "root = true\n[*.cs]\nindent_style = space\nindent_size = 4\n";

    /// <summary>Fixes the first fixable diagnostic, round after round, until none are left.</summary>
    public static async Task<FixResult> FixEachAsync(
        [StringSyntax("C#")] string source,
        params MetadataReference[] references
    )
    {
        Document document = CreateDocument(source, references);
        Document fixedDocument = await FixRoundsAsync(document, MaxRounds).ConfigureAwait(false);
        return await FixResult.ReadAsync(fixedDocument).ConfigureAwait(false);
    }

    /// <summary>Fixes every fixable diagnostic in one fix-all.</summary>
    public static async Task<FixResult> FixAllAsync(
        [StringSyntax("C#")] string source,
        params MetadataReference[] references
    )
    {
        Document document = CreateDocument(source, references);
        ImmutableArray<Diagnostic> fixable = await GetFixableAsync(document).ConfigureAwait(false);
        Document fixedDocument = await ApplyFixAllAsync(document, fixable).ConfigureAwait(false);
        return await FixResult.ReadAsync(fixedDocument).ConfigureAwait(false);
    }

    /// <summary>The code actions the fix registers for each of <paramref name="diagnostics"/>.</summary>
    public static async Task<ImmutableArray<CodeAction>> RegisterAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics
    )
    {
        RestateAttributeCodeFix fix = new();
        List<CodeAction> actions = [];
        foreach (Diagnostic diagnostic in diagnostics)
        {
            CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken);
            await fix.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        }

        return [.. actions];
    }

    public static Document CreateDocument([StringSyntax("C#")] string source, params MetadataReference[] references)
    {
        AdhocWorkspace workspace = new();
        ProjectInfo project = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            TestCompilation.AssemblyName,
            TestCompilation.AssemblyName,
            LanguageNames.CSharp,
            compilationOptions: TestCompilation.CompilationOptions,
            parseOptions: TestCompilation.ParseOptions,
            metadataReferences: [.. TestCompilation.References, .. references]
        );
        return workspace.AddProject(project)
            .AddAnalyzerConfigDocument(".editorconfig", SourceText.From(EditorConfig), filePath: "/src/.editorconfig")
            .Project
            .AddDocument("Test.cs", source, filePath: "/src/Test.cs");
    }

    public static async Task<ImmutableArray<Diagnostic>> GetFixableAsync(Document document)
    {
        Compilation? compilation = await document.Project.GetCompilationAsync(CancellationToken).ConfigureAwait(false);
        ImmutableArray<Diagnostic> diagnostics = await compilation!
            .WithAnalyzers([new PreconditionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken)
            .ConfigureAwait(false);
        return [.. diagnostics
            .Where(static diagnostic => diagnostic.Properties.ContainsKey(RestateProperties.AttributeDisplay))
            .OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    public static async Task<Document> ApplyAsync(Document document, CodeAction action)
    {
        ImmutableArray<CodeActionOperation> operations =
            await action.GetOperationsAsync(CancellationToken).ConfigureAwait(false);
        ApplyChangesOperation change = Assert.Single(operations.OfType<ApplyChangesOperation>());
        return change.ChangedSolution.GetDocument(document.Id)!;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static async Task<Document> FixRoundsAsync(Document document, int roundsLeft)
    {
        ImmutableArray<Diagnostic> fixable = await GetFixableAsync(document).ConfigureAwait(false);
        return fixable.IsEmpty || roundsLeft == 0
            ? document
            : await FixRoundsAsync(await FixFirstAsync(document, fixable[0]).ConfigureAwait(false), roundsLeft - 1)
                .ConfigureAwait(false);
    }

    private static async Task<Document> FixFirstAsync(Document document, Diagnostic diagnostic)
    {
        ImmutableArray<CodeAction> actions = await RegisterAsync(document, [diagnostic]).ConfigureAwait(false);
        return await ApplyAsync(document, Assert.Single(actions)).ConfigureAwait(false);
    }

    private static async Task<Document> ApplyFixAllAsync(Document document, ImmutableArray<Diagnostic> fixable)
    {
        RestateAttributeCodeFix fix = new();
        FixAllContext context = new(
            document,
            fix,
            FixAllScope.Document,
            RestateAttributeCodeFix.EquivalenceKey,
            fix.FixableDiagnosticIds,
            new FixedDiagnosticProvider(fixable),
            CancellationToken
        );
        CodeAction? action = await fix.GetFixAllProvider().GetFixAsync(context).ConfigureAwait(false);
        return await ApplyAsync(document, action!).ConfigureAwait(false);
    }
}

/// <summary>Hands the fix-all exactly the diagnostics found beforehand.</summary>
file sealed class FixedDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
{
    private readonly ImmutableArray<Diagnostic> _diagnostics = diagnostics;

    public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
        Project project,
        CancellationToken cancellationToken
    )
        => Task.FromResult<IEnumerable<Diagnostic>>(this._diagnostics);

    public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        CancellationToken cancellationToken
    )
        => Task.FromResult<IEnumerable<Diagnostic>>(this._diagnostics);

    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        CancellationToken cancellationToken
    )
        => Task.FromResult(Enumerable.Empty<Diagnostic>());
}

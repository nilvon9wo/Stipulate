using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using ParameterValidation.CodeFixes;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ParameterValidation.Analyzers.Test;

/// <summary>Applies <see cref="RestateAttributeCodeFix"/> to C# source in an in-memory workspace.</summary>
internal static class CodeFixVerifier
{
	/// <summary>
	/// Fixes the first fixable diagnostic, or with <paramref name="fixAll"/> all of them at once,
	/// until none are left; then checks the result, and that it compiles.
	/// </summary>
	public static async Task VerifyAsync(
		[StringSyntax("C#")] string source,
		[StringSyntax("C#")] string expected,
		bool fixAll = false,
		params MetadataReference[] references)
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		using AdhocWorkspace workspace = new();
		Project project = workspace
			.AddProject(ProjectInfo.Create(
				ProjectId.CreateNewId(),
				VersionStamp.Default,
				"Test",
				"Test",
				LanguageNames.CSharp,
				compilationOptions: AnalyzerVerifier.CompilationOptions,
				parseOptions: AnalyzerVerifier.ParseOptions,
				metadataReferences: [.. AnalyzerVerifier.References, .. references]))
			.AddAnalyzerConfigDocument(".editorconfig", SourceText.From("root = true\n[*.cs]\nindent_style = tab\n"), filePath: "/src/.editorconfig")
			.Project;
		Document document = project.AddDocument("Test.cs", source, filePath: "/src/Test.cs");

		RestateAttributeCodeFix fix = new();
		for (int round = 0; round < 10; round++)
		{
			ImmutableArray<Diagnostic> fixable = await GetFixableAsync(document, fix, cancellationToken).ConfigureAwait(false);
			if (fixable.IsEmpty)
			{
				break;
			}

			CodeAction? action;
			if (fixAll)
			{
				FixAllContext context = new(document, fix, FixAllScope.Document, nameof(RestateAttributeCodeFix), fix.FixableDiagnosticIds, new Diagnostics(fixable), cancellationToken);
				action = await fix.GetFixAllProvider().GetFixAsync(context).ConfigureAwait(false);
			}
			else
			{
				List<CodeAction> actions = [];
				await fix.RegisterCodeFixesAsync(new CodeFixContext(document, fixable[0], (registered, _) => actions.Add(registered), cancellationToken)).ConfigureAwait(false);
				action = Assert.Single(actions);
			}

			Assert.NotNull(action);
			ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
			document = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution.GetDocument(document.Id)!;
			if (fixAll)
			{
				// One fix-all must fix everything; the final check below makes sure it did.
				break;
			}
		}

		string actual = (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
		Assert.Equal(expected.ReplaceLineEndings(), actual.ReplaceLineEndings());

		Compilation compilation = (await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;
		Diagnostic[] errors = [.. compilation.GetDiagnostics(cancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error)];
		Assert.True(errors.Length == 0, $"The fixed source doesn't compile:\n{string.Join("\n", errors.Select(static e => e.ToString()))}");
		Assert.Empty(await GetFixableAsync(document, fix, cancellationToken).ConfigureAwait(false));
	}

	private static async Task<ImmutableArray<Diagnostic>> GetFixableAsync(Document document, RestateAttributeCodeFix fix, CancellationToken cancellationToken)
	{
		Compilation compilation = (await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;
		ImmutableArray<Diagnostic> diagnostics = await compilation
			.WithAnalyzers([new PreconditionAnalyzer()])
			.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
		return [.. diagnostics
			.Where(diagnostic => fix.FixableDiagnosticIds.Contains(diagnostic.Id) && diagnostic.Properties.ContainsKey("AttributeDisplay"))
			.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)];
	}

	private sealed class Diagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
	{
		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

		public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
			=> Task.FromResult(Enumerable.Empty<Diagnostic>());
	}
}

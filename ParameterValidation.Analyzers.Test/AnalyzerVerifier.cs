using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using ParameterValidation.Contracts;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// Runs <see cref="PreconditionAnalyzer"/> over C# source in memory. Expected diagnostics are
/// marked up in the source as <c>{|PV1001:text|}</c>, as in Roslyn's own analyzer tests.
/// </summary>
internal static class AnalyzerVerifier
{
	public const string Usings = """
		using System.ComponentModel.DataAnnotations;
		using ParameterValidation.Contracts;

		""";

	/// <summary>
	/// The running .NET's framework assemblies plus ParameterValidation.Contracts. The trusted
	/// platform assemblies only sometimes include the test's own dependencies (it depends on the
	/// test host), so Contracts is added explicitly.
	/// </summary>
	private static readonly Lazy<ImmutableArray<MetadataReference>> PlatformReferences = new(static () =>
	{
		string contracts = typeof(ComparableValidationAttribute).Assembly.Location;
		return [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Where(static path => !Path.GetFileName(path).StartsWith("ParameterValidation.", StringComparison.OrdinalIgnoreCase))
			.Append(contracts)
			.Select(static path => MetadataReference.CreateFromFile(path))];
	});

	public static Task VerifyAsync([StringSyntax("C#")] string markup, params MetadataReference[] references)
		=> VerifyAsync(markup, solutionRoot: null, references);

	public static async Task VerifyAsync([StringSyntax("C#")] string markup, string? solutionRoot, params MetadataReference[] references)
	{
		(string source, List<(string Id, TextSpan Span)> expected) = Parse(Usings + markup);
		ImmutableArray<Diagnostic> actual = await GetDiagnosticsAsync(source, solutionRoot, references).ConfigureAwait(false);

		string[] expectedLines = [.. expected.Select(e => $"{e.Id} at '{source.Substring(e.Span.Start, e.Span.Length)}'").Order()];
		string[] actualLines = [.. actual.Select(d => $"{d.Id} at '{source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length)}'").Order()];
		if (!expectedLines.SequenceEqual(actualLines))
		{
			Assert.Fail($"""
				Expected:
				  {string.Join("\n  ", expectedLines)}
				Actual:
				  {string.Join("\n  ", actual.Select(static d => $"{d.Id} at {d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}"))}
				""");
		}
	}

	/// <param name="source">Plain source, without <see cref="Usings"/> or markup.</param>
	public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string? solutionRoot = null, params MetadataReference[] references)
	{
		CSharpCompilation compilation = Compile(source, "Test", references);
		Diagnostic[] errors = [.. compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)];
		Assert.True(errors.Length == 0, $"The test source doesn't compile:\n{string.Join("\n", errors.Select(static e => e.ToString()))}");

		AnalyzerOptions options = new([], new GlobalOptionsProvider(solutionRoot));
		return await compilation
			.WithAnalyzers([new PreconditionAnalyzer()], options)
			.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>Compiles another assembly, e.g. a package's, for the test source to reference.</summary>
	/// <param name="filePath">Where the reference claims to have been loaded from; <c>null</c> for nowhere.</param>
	public static MetadataReference CompileToImage([StringSyntax("C#")] string source, string assemblyName, string? filePath = null, params MetadataReference[] references)
	{
		using MemoryStream stream = new();
		EmitResult result = Compile(Usings + source, assemblyName, references).Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
		Assert.True(result.Success, string.Join("\n", result.Diagnostics));
		return MetadataReference.CreateFromImage(ImmutableArray.Create(stream.ToArray()), filePath: filePath);
	}

	public static ImmutableArray<MetadataReference> References => PlatformReferences.Value;

	public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Latest);

	public static CSharpCompilationOptions CompilationOptions { get; } = new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

	public static CSharpCompilation Compile(string source, string assemblyName, params MetadataReference[] references)
		=> CSharpCompilation.Create(
			assemblyName,
			[CSharpSyntaxTree.ParseText(source, ParseOptions, cancellationToken: TestContext.Current.CancellationToken)],
			[.. PlatformReferences.Value, .. references],
			CompilationOptions);

	private static (string Source, List<(string Id, TextSpan Span)> Expected) Parse(string markup)
	{
		StringBuilder source = new();
		List<(string Id, TextSpan Span)> expected = [];
		int position = 0;
		while (true)
		{
			int open = markup.IndexOf("{|", position, StringComparison.Ordinal);
			if (open < 0)
			{
				_ = source.Append(markup, position, markup.Length - position);
				return (source.ToString(), expected);
			}

			int colon = markup.IndexOf(':', open);
			int close = markup.IndexOf("|}", colon, StringComparison.Ordinal);
			_ = source.Append(markup, position, open - position);
			int start = source.Length;
			_ = source.Append(markup, colon + 1, close - colon - 1);
			expected.Add((markup.Substring(open + 2, colon - open - 2), TextSpan.FromBounds(start, source.Length)));
			position = close + 2;
		}
	}

	private sealed class GlobalOptionsProvider(string? solutionRoot) : AnalyzerConfigOptionsProvider
	{
		private static readonly Options Empty = new(null);

		public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(solutionRoot);

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

		private sealed class Options(string? solutionRoot) : AnalyzerConfigOptions
		{
			public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
			{
				value = key == "build_property.ParameterValidationSolutionRoot" ? solutionRoot : null;
				return value is not null;
			}
		}
	}
}

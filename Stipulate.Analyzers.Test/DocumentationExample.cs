using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// One <c>```csharp</c> example from README.md or docs/, and the diagnostics its comments say the
/// analyzer reports on it: a comment such as <c>// error ST1001: ...</c> or <c>// warning ST1002</c>.
/// </summary>
/// <remarks>
/// A fence just after <c>&lt;!-- sketch --&gt;</c> is illustrative and isn't checked. One just after
/// <c>&lt;!-- reference: Name --&gt;</c> is compiled as assembly <c>Name</c>, from outside the
/// solution, and referenced by the examples that follow it on the same page, until another
/// reference of the same name replaces it.
/// </remarks>
/// <param name="Page">The page's path from the repository root, with forward slashes.</param>
/// <param name="Line">The line of the opening fence.</param>
/// <param name="References">The reference examples before this one on its page, by assembly name.</param>
internal sealed partial record DocumentationExample(
    string Page,
    int Line,
    string Code,
    ImmutableArray<(string Name, string Code)> References
)
{
    private const string Sketch = "sketch";
    private const string ReferencePrefix = "reference: ";

    private static readonly string RepositoryRoot = typeof(DocumentationExample).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(static attribute => attribute.Key == nameof(RepositoryRoot))
        .Value!;

    /// <summary>An optional marker line, the opening fence, the code and the closing fence, equally indented.</summary>
    private const string FencePattern = @"^(?<indent>[ \t]*)(?:<!-- (?<marker>[^>]*?) -->\n\k<indent>)?"
        + @"```csharp\n(?<code>.*?)\n\k<indent>```";

    private const string ShownDiagnosticPattern = @"//\s*(?<severity>error|warning) (?<id>ST\d{4})\b";

    /// <summary>The examples the analyzer must check: every page's, except sketches and references.</summary>
    public static IEnumerable<DocumentationExample> ReadAll() => Pages().SelectMany(ReadPage);

    public static DocumentationExample Read(string page, int line)
        => ReadPage(page).Single(example => example.Line == line);

    /// <summary>What the comments show, as sorted <c>"error ST1001"</c> entries.</summary>
    public ImmutableArray<string> ShownDiagnostics
        => [.. ShownDiagnostic().Matches(this.Code).Select(Describe).Order(StringComparer.Ordinal)];

    public MetadataReference[] CompileReferences()
        => [.. this.References.Select(static reference => Compile(reference.Name, reference.Code))];

    public override string ToString() => $"{this.Page}:{this.Line}";

    private static string Describe(Match match) => $"{match.Groups["severity"].Value} {match.Groups["id"].Value}";

    private static IEnumerable<string> Pages()
    {
        string docs = Path.Combine(RepositoryRoot, "docs");
        IEnumerable<string> docPages = Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories);
        return docPages
            .Prepend(Path.Combine(RepositoryRoot, "README.md"))
            .Select(static path => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/'));
    }

    private static IEnumerable<DocumentationExample> ReadPage(string page)
    {
        string text = File.ReadAllText(Path.Combine(RepositoryRoot, page)).ReplaceLineEndings("\n");
        List<(string Name, string Code)> references = [];
        foreach (Match fence in Fence().Matches(text))
        {
            string marker = fence.Groups["marker"].Value;
            string code = Unindent(fence.Groups["code"].Value, fence.Groups["indent"].Value);
            if (marker.StartsWith(ReferencePrefix, StringComparison.Ordinal))
            {
                string name = marker.Substring(ReferencePrefix.Length);
                _ = references.RemoveAll(reference => reference.Name == name);
                references.Add((name, code));
            }
            else if (marker != Sketch)
            {
                yield return new DocumentationExample(page, LineOf(text, fence.Index), code, [.. references]);
            }
        }
    }

    private static string Unindent(string code, string indent)
        => string.Join('\n', code.Split('\n').Select(line => UnindentLine(line, indent)));

    private static string UnindentLine(string line, string indent)
        => line.StartsWith(indent, StringComparison.Ordinal)
            ? line.Substring(indent.Length)
            : line;

    private static MetadataReference Compile(string name, string code) => TestCompilation.CompileToImage(code, name);

    [GeneratedRegex(FencePattern, RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Fence();

    [GeneratedRegex(ShownDiagnosticPattern)]
    private static partial Regex ShownDiagnostic();

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.CodeFixes.Test;

/// <summary>A document after the code fix ran on it.</summary>
/// <param name="Text">The fixed source, with its line endings normalised.</param>
/// <param name="Errors">The fixed source's compilation errors.</param>
/// <param name="RemainingFixable">The diagnostics the fix could still fix.</param>
internal sealed record FixResult(
    string Text,
    ImmutableArray<Diagnostic> Errors,
    ImmutableArray<Diagnostic> RemainingFixable
)
{
    public static async Task<FixResult> ReadAsync(Document document)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string text = (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        Compilation? compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        ImmutableArray<Diagnostic> errors = [.. compilation!
            .GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        ImmutableArray<Diagnostic> remaining = await CodeFixRunner.GetFixableAsync(document).ConfigureAwait(false);
        return new FixResult(text.ReplaceLineEndings(), errors, remaining);
    }

    /// <summary>
    /// Asserts the fix produced <paramref name="expected"/>, which compiles and needs no more fixing.
    /// </summary>
    public void AssertFixedTo(string expected)
    {
        Assert.Equal(expected.ReplaceLineEndings(), this.Text);
        Assert.Empty(this.Errors);
        Assert.Empty(this.RemainingFixable);
    }
}

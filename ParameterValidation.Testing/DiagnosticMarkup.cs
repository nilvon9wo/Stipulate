using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace Net.NowhereAtAll.ParameterValidation.Testing;

/// <summary>
/// C# source with the diagnostics expected in it marked up as <c>{|PV1001:text|}</c>, as in Roslyn's
/// own analyzer tests, preceded by <see cref="TestCompilation.Usings"/>.
/// </summary>
public sealed partial class DiagnosticMarkup
{
    private const string IdGroup = "id";
    private const string TextGroup = "text";

    public DiagnosticMarkup([StringSyntax("C#")] string markup)
    {
        string text = TestCompilation.Usings + markup;
        MatchCollection matches = Markup().Matches(text);
        this.Source = Markup().Replace(text, static match => match.Groups[TextGroup].Value);
        this.Expected = [.. matches.Select((match, index) => ToExpected(match, RemovedBefore(matches, index)))];
    }

    /// <summary>The source without the markup.</summary>
    public string Source { get; }

    public ImmutableArray<ExpectedDiagnostic> Expected { get; }

    [GeneratedRegex(@"\{\|(?<id>[A-Z]+[0-9]+):(?<text>.*?)\|\}", RegexOptions.Singleline)]
    private static partial Regex Markup();

    /// <summary>How many characters of markup the matches before <paramref name="index"/> take up.</summary>
    private static int RemovedBefore(MatchCollection matches, int index)
        => matches.Take(index).Sum(static match => match.Length - match.Groups[TextGroup].Length);

    private static ExpectedDiagnostic ToExpected(Match match, int removedBefore)
    {
        TextSpan span = new(match.Index - removedBefore, match.Groups[TextGroup].Length);
        return new ExpectedDiagnostic(match.Groups[IdGroup].Value, span);
    }
}

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// The analyzer's diagnostic IDs, numbered by category: ST1xxx for the contract rules themselves,
/// ST2xxx for attributes that can't be compared. (ST0xxx are the build's own errors, reported by
/// Stipulate.Weaver and Directory.Build.targets.) Linked into Stipulate.CodeFixes.
/// Once shipped, an ID never changes; see AnalyzerReleases.Shipped.md.
/// </summary>
internal static class DiagnosticIds
{
    public const string PreconditionStrengthened = "ST1001";
    public const string PreconditionDropped = "ST1002";
    public const string GuaranteeWeakened = "ST1003";

    public const string NotComparableInSolution = "ST2001";
    public const string NotComparableExternal = "ST2002";
    public const string InvalidDescription = "ST2003";
}

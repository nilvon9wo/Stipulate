namespace ParameterValidation.Analyzers;

/// <summary>
/// The analyzer's diagnostic IDs, numbered by category: PV1xxx for the contract rules themselves,
/// PV2xxx for attributes that can't be compared. (PV0xxx are the build's own errors, reported by
/// ParameterValidation.Weaver and Directory.Build.targets.) Linked into ParameterValidation.CodeFixes.
/// Once shipped, an ID never changes; see AnalyzerReleases.Shipped.md.
/// </summary>
internal static class DiagnosticIds
{
    public const string PreconditionStrengthened = "PV1001";
    public const string PreconditionDropped = "PV1002";
    public const string GuaranteeWeakened = "PV1003";

    public const string NotComparableInSolution = "PV2001";
    public const string NotComparableExternal = "PV2002";
    public const string InvalidDescription = "PV2003";
}

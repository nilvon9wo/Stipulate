namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// The weaver's MSBuild error codes. ST0004 (the weaver crashed or never started) and ST0005 (it
/// hasn't been built) are reported by Directory.Build.targets, which runs it.
/// </summary>
internal static class WeavingErrorCodes
{
    /// <summary>The assembly can't be woven, e.g. a type it uses can't be resolved.</summary>
    public const string WeavingFailed = "ST0001";

    /// <summary>A file stayed in use by another process through every retry.</summary>
    public const string FileInUse = "ST0002";

    /// <summary>The weaver failed in a way it didn't anticipate.</summary>
    public const string Unexpected = "ST0003";

    /// <summary>A warning: the symbols don't match the assembly, so they were left unchanged.</summary>
    public const string SymbolsMismatch = "ST0006";

    /// <summary>The assembly has something to check but not the check itself, ArgumentChecks.</summary>
    public const string ChecksMissing = "ST0007";
}

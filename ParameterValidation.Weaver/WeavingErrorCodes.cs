namespace ParameterValidation.Weaver;

/// <summary>
/// The weaver's MSBuild error codes. PV0004 (the weaver crashed or never started) and PV0005 (it
/// hasn't been built) are reported by Directory.Build.targets, which runs it.
/// </summary>
internal static class WeavingErrorCodes
{
    /// <summary>The assembly can't be woven, e.g. a type it uses can't be resolved.</summary>
    public const string WeavingFailed = "PV0001";

    /// <summary>A file stayed in use by another process through every retry.</summary>
    public const string FileInUse = "PV0002";

    /// <summary>The weaver failed in a way it didn't anticipate.</summary>
    public const string Unexpected = "PV0003";

    /// <summary>A warning: the symbols don't match the assembly, so they were left unchanged.</summary>
    public const string SymbolsMismatch = "PV0006";
}

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

/// <summary>
/// Rewrites a compiled assembly so that every method validates its parameters' DataAnnotations
/// attributes on entry.
/// </summary>
/// <remarks>
/// Usage: <c>ParameterValidation.Weaver &lt;assembly&gt; &lt;references-file&gt;</c>, where the
/// references file lists the assembly's compile references, one path per line.
/// Exits with <see cref="SuccessExitCode"/> on success and <see cref="ReportedFailureExitCode"/> on a
/// failure that has already been reported as an MSBuild error; any other exit code means the weaver
/// crashed or never started (which also covers <c>dotnet</c> itself failing to launch it).
/// </remarks>
internal static class WeaverCommand
{
    public const int SuccessExitCode = 0;
    public const int ReportedFailureExitCode = 2;

    private const int ArgumentCount = 2;
    private const string Usage = "Usage: Net.NowhereAtAll.ParameterValidation.Weaver <assembly> <references-file>";

    public static int Run(string[] arguments, TextWriter output, TextWriter error)
        => arguments.Length == ArgumentCount
            ? Weave(arguments[0], arguments[1], output, error)
            : ReportUsage(error);

    private static int ReportUsage(TextWriter error)
    {
        error.WriteLine(Usage);
        return ReportedFailureExitCode;
    }

    private static int Weave(string assemblyPath, string referencesPath, TextWriter output, TextWriter error)
    {
        try
        {
            AssemblyWeaver weaver = new(output, FileRetry.Create(output));
            int? wovenCount = weaver.Weave(assemblyPath, File.ReadAllLines(referencesPath));
            output.WriteLine(Describe(assemblyPath, wovenCount));
            return SuccessExitCode;
        }
        catch (WeavingException exception)
        {
            return ReportError(error, assemblyPath, exception.Code, exception.Message);
        }
        catch (Exception exception)
        {
            string message = $"Unexpected {exception.GetType().Name}: {exception.Message}"
                + $"{Environment.NewLine}{exception.StackTrace}";
            return ReportError(error, assemblyPath, WeavingErrorCodes.Unexpected, message);
        }
    }

    private static string Describe(string assemblyPath, int? wovenCount)
    {
        string fileName = Path.GetFileName(assemblyPath);
        return wovenCount is null
            ? $"ParameterValidation: {fileName} is already woven."
            : $"ParameterValidation: wove {wovenCount} method(s) in {fileName}.";
    }

    /// <summary>
    /// In MSBuild's canonical error format, so the failure shows up in the Error List with its real cause.
    /// </summary>
    private static int ReportError(TextWriter error, string assemblyPath, string code, string message)
    {
        error.WriteLine($"{assemblyPath} : error {code}: {message}");
        return ReportedFailureExitCode;
    }
}

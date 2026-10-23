namespace ParameterValidation.Weaver;

/// <summary>
/// Rewrites a compiled assembly so that every method validates its parameters' DataAnnotations
/// attributes on entry.
/// </summary>
/// <remarks>
/// Usage: <c>ParameterValidation.Weaver &lt;assembly&gt; &lt;references-file&gt;</c>, where the
/// references file lists the assembly's compile references, one path per line.
/// Exits with 0 on success and <see cref="ReportedFailureExitCode"/> on a failure that has already
/// been reported as an MSBuild error; any other exit code means the weaver crashed or never started
/// (which also covers <c>dotnet</c> itself failing to launch it).
/// </remarks>
internal static class Program
{
	private const int ReportedFailureExitCode = 2;

	private static int Main(string[] args)
	{
		if (args.Length != 2)
		{
			Console.Error.WriteLine("Usage: ParameterValidation.Weaver <assembly> <references-file>");
			return ReportedFailureExitCode;
		}

		string assemblyPath = args[0];
		try
		{
			int? wovenCount = AssemblyWeaver.Weave(assemblyPath, File.ReadAllLines(args[1]));
			Console.WriteLine(wovenCount is null
				? $"ParameterValidation: {Path.GetFileName(assemblyPath)} is already woven."
				: $"ParameterValidation: wove {wovenCount} method(s) in {Path.GetFileName(assemblyPath)}.");
			return 0;
		}
		catch (WeavingException ex)
		{
			return ReportError(assemblyPath, ex.Code, ex.Message);
		}
		catch (Exception ex)
		{
			return ReportError(assemblyPath, "PV0003", $"Unexpected {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
		}
	}

	private static int ReportError(string assemblyPath, string code, string message)
	{
		// MSBuild canonical error format, so the failure shows up in the Error List with its real cause.
		Console.Error.WriteLine($"{assemblyPath} : error {code}: {message}");
		return ReportedFailureExitCode;
	}
}

namespace ParameterValidation.Weaver;

/// <summary>
/// Retries file operations that fail because another process (another build, a test run, an
/// antivirus scan) briefly has the file open.
/// </summary>
internal static class FileRetry
{
	private const int MaxAttempts = 10;
	private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

	public static T Run<T>(string description, Func<T> operation)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return operation();
			}
			catch (Exception ex) when (IsFileInUse(ex) && attempt < MaxAttempts)
			{
				Console.WriteLine($"ParameterValidation: Could not {description}: {ex.Message} Retrying ({attempt}/{MaxAttempts - 1})...");
				Thread.Sleep(RetryDelay);
			}
			catch (Exception ex) when (IsFileInUse(ex))
			{
				throw new WeavingException($"Could not {description} after {MaxAttempts} attempts: {ex.Message}", "PV0002");
			}
		}
	}

	public static void Run(string description, Action operation) => Run(description, () =>
	{
		operation();
		return true;
	});

	/// <remarks>
	/// Windows reports replacing a file that another process has open as an
	/// <see cref="UnauthorizedAccessException"/> rather than an <see cref="IOException"/>.
	/// </remarks>
	private static bool IsFileInUse(Exception ex) => ex is IOException or UnauthorizedAccessException;
}

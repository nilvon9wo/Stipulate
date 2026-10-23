namespace ParameterValidation.Weaver;

/// <param name="code">The MSBuild error code the failure is reported under.</param>
internal sealed class WeavingException(string message, string code = "PV0001") : Exception(message)
{
	public string Code { get; } = code;
}

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

/// <summary>A failure the weaver anticipates, reported as an MSBuild error.</summary>
/// <param name="code">
/// The MSBuild error code the failure is reported under; see <see cref="WeavingErrorCodes"/>.
/// </param>
internal sealed class WeavingException(string message, string code = WeavingErrorCodes.WeavingFailed)
    : Exception(message)
{
    public string Code { get; } = code;
}

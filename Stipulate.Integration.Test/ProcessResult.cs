namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>What a finished <c>dotnet</c> process reported.</summary>
/// <param name="Output">Its standard output followed by its standard error.</param>
public sealed record ProcessResult(int ExitCode, string Output)
{
    /// <summary>Asserts the process succeeded, showing everything it printed if it didn't.</summary>
    public ProcessResult AssertSucceeded()
    {
        Assert.True(this.ExitCode == 0, this.Output);
        return this;
    }
}

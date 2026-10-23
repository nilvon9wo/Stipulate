namespace Net.NowhereAtAll.ParameterValidation.Weaver.Test;

/// <summary>
/// Retrying a file operation while another process has the file open, and only then: a pause is
/// recorded rather than slept.
/// </summary>
public class FileRetryTest
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    private readonly List<TimeSpan> _pauses = [];
    private readonly StringWriter _log = new();

    [Fact]
    public void Run_WhenTheOperationSucceeds_ReturnsItsResultWithoutPausing()
    {
        // Arrange
        FileRetry retry = this.CreateRetry();

        // Act
        int result = retry.Run("read the assembly", 21, static state => state * 2);

        // Assert
        Assert.Equal(42, result);
        Assert.Empty(this._pauses);
    }

    [Fact]
    public void Run_WhenTheOperationHasNoResult_RunsIt()
    {
        // Arrange
        FileRetry retry = this.CreateRetry();
        List<string> runs = [];

        // Act
        retry.Run("replace the assembly", runs, static state => state.Add("ran"));

        // Assert
        Assert.Equal(["ran"], runs);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Run_WhenTheFileIsBrieflyInUse_PausesAndRetries(Type exceptionType)
    {
        // Arrange
        FileRetry retry = this.CreateRetry();
        Exception inUse = CreateException(exceptionType);
        Queue<Exception> failures = new([inUse]);

        // Act
        string result = retry.Run("read the assembly", failures, Succeed);

        // Assert
        Assert.Equal("done", result);
        Assert.Equal([Delay], this._pauses);
        Assert.Equal(
            $"ParameterValidation: Could not read the assembly: {inUse.Message} Retrying (1/2)...",
            this._log.ToString().TrimEnd()
        );
    }

    [Fact]
    public void Run_WhenTheFileStaysInUse_ThrowsWeavingExceptionAfterTheLastAttempt()
    {
        // Arrange
        FileRetry retry = this.CreateRetry();
        Queue<Exception> failures = new(
            [new IOException("first"), new IOException("second"), new IOException("last")]
        );

        // Act
        WeavingException exception = Assert.Throws<WeavingException>(
            () => retry.Run("read the assembly", failures, Succeed)
        );

        // Assert
        Assert.Equal(WeavingErrorCodes.FileInUse, exception.Code);
        Assert.Equal("Could not read the assembly after 3 attempts: last", exception.Message);
        Assert.Equal([Delay, Delay], this._pauses);
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(InvalidOperationException))]
    public void Run_WhenTheOperationFailsForAnotherReason_RethrowsWithoutRetrying(Type exceptionType)
    {
        // Arrange - waiting won't bring back a missing file
        FileRetry retry = this.CreateRetry();
        Exception failure = CreateException(exceptionType);
        Queue<Exception> failures = new([failure]);

        // Act
        Exception exception = Record.Exception(() => retry.Run("read the assembly", failures, Succeed))!;

        // Assert
        Assert.Same(failure, exception);
        Assert.Empty(this._pauses);
    }

    private static Exception CreateException(Type exceptionType)
        => (Exception)Activator.CreateInstance(exceptionType, "the failure")!;

    /// <summary>Throws each queued failure in turn, then succeeds.</summary>
    private static string Succeed(Queue<Exception> failures)
        => failures.TryDequeue(out Exception? failure)
            ? throw failure
            : "done";

    private FileRetry CreateRetry() => new(MaxAttempts, Delay, this._log, this._pauses.Add);
}

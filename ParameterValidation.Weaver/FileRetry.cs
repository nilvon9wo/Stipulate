namespace ParameterValidation.Weaver;

/// <summary>
/// Retries file operations that fail because another process (another build, a test run, an
/// antivirus scan) briefly has the file open.
/// </summary>
/// <param name="pause">Waits between attempts; <see cref="Thread.Sleep(TimeSpan)"/> outside tests.</param>
internal sealed class FileRetry(int maxAttempts, TimeSpan delay, TextWriter log, Action<TimeSpan> pause)
{
    private const int DefaultMaxAttempts = 10;
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(500);

    private readonly int _maxAttempts = maxAttempts;
    private readonly TimeSpan _delay = delay;
    private readonly TextWriter _log = log;
    private readonly Action<TimeSpan> _pause = pause;

    public static FileRetry Create(TextWriter log) => new(DefaultMaxAttempts, DefaultDelay, log, Thread.Sleep);

    /// <param name="description">What the operation does, as the messages say it: "read the assembly".</param>
    /// <param name="state">What the operation needs, passed in rather than captured.</param>
    /// <param name="operation">Runs once per attempt, before this returns.</param>
    public TResult Run<TState, TResult>(string description, TState state, Func<TState, TResult> operation)
        => this.Attempt(description, state, operation, 1);

    /// <inheritdoc cref="Run{TState, TResult}(string, TState, Func{TState, TResult})"/>
    public void Run<TState>(string description, TState state, Action<TState> operation)
        => this.Run(description, (State: state, Operation: operation), Invoke);

    private static bool Invoke<TState>((TState State, Action<TState> Operation) action)
    {
        action.Operation(action.State);
        return true;
    }

    private TResult Attempt<TState, TResult>(
        string description,
        TState state,
        Func<TState, TResult> operation,
        int attempt
    )
    {
        try
        {
            return operation(state);
        }
        catch (Exception exception) when (IsFileInUse(exception) && attempt < this._maxAttempts)
        {
            this._log.WriteLine(
                $"ParameterValidation: Could not {description}: {exception.Message} "
                + $"Retrying ({attempt}/{this._maxAttempts - 1})..."
            );
            this._pause(this._delay);
        }
        catch (Exception exception) when (IsFileInUse(exception))
        {
            throw new WeavingException(
                $"Could not {description} after {this._maxAttempts} attempts: {exception.Message}",
                WeavingErrorCodes.FileInUse
            );
        }

        return this.Attempt(description, state, operation, attempt + 1);
    }

    /// <remarks>
    /// Windows reports replacing a file that another process has open as an
    /// <see cref="UnauthorizedAccessException"/> rather than an <see cref="IOException"/>. A missing
    /// file is an <see cref="IOException"/> too, but waiting won't bring it back.
    /// </remarks>
    private static bool IsFileInUse(Exception exception)
        => exception is (IOException or UnauthorizedAccessException)
            and not (FileNotFoundException or DirectoryNotFoundException);
}

using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Generator.Test;

/// <summary>
/// What an intercepted call does: it checks each validation attribute on each parameter, as the
/// attribute was declared, and only then calls the original method.
/// </summary>
public class InterceptedCallTest
{
    private const string Caller = "Caller";

    private const string Source = """
        public enum Color { Red, Green }

        public class Target
        {
            public static int Count([Required, MinLength(1)] int[]? array) => array!.Length;

            public static string Text([Required] string? text) => text!;

            public static void Log([Range(1, 10)] int level) { }

            public int Grow([Range(1, 10)] int by) => by * 2;

            public static void Messaged([Range(1, 10, ErrorMessage = "Out of range")] int value) { }

            public static void Typed([Range(typeof(decimal), "1", "10")] decimal value) { }

            public static void Allowed([AllowedValues(1, "two", null, Color.Green)] object? value) { }

            public static int Pair([Range(1, 10)] int first, [Range(1, 10)] int second) => first + second;
        }

        public static class Caller
        {
            public static int Count(int[]? array) => Target.Count(array);

            public static int CountAgain(int[]? array) => Target.Count(array);

            public static string Text(string? text) => Target.Text(text);

            public static void Log(int level) => Target.Log(level);

            public static int Grow(int by) => new Target().Grow(by);

            public static void Messaged(int value) => Target.Messaged(value);

            public static void Typed(decimal value) => Target.Typed(value);

            public static void Allowed(object? value) => Target.Allowed(value);

            public static void AllowedGreen() => Target.Allowed(Color.Green);

            public static int Pair(int first, int second) => Target.Pair(first, second);
        }
        """;

    private static readonly Lazy<Assembly> Intercepted = new(static () => GeneratorRunner.GenerateAndLoad(Source));

    [Theory]
    [InlineData("Count")]
    [InlineData("CountAgain")]
    public void Call_WhenEveryArgumentIsValid_ReturnsTheOriginalResult(string methodName)
    {
        // Arrange
        int[] array = [1, 2, 3];

        // Act
        object? result = InterceptedCode.Invoke(Intercepted.Value, Caller, methodName, array);

        // Assert
        Assert.Equal(3, result);
    }

    [Theory]
    [InlineData("Count")]
    [InlineData("CountAgain")]
    public void Call_WhenARequiredArgumentIsNull_ThrowsArgumentNullException(string methodName)
    {
        // Arrange - both callers share one interceptor

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, methodName, [null])
        );

        // Assert
        Assert.Equal("array", exception.ParamName);
        Assert.StartsWith("The array field is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Call_WhenAnArgumentIsInvalid_ThrowsArgumentExceptionWithTheAttributesMessage()
    {
        // Arrange
        int[] empty = [];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Count", empty)
        );

        // Assert
        Assert.Equal("array", exception.ParamName);
        Assert.StartsWith(
            "The field array must be a string or array type with a minimum length of '1'.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Call_WhenRequiredRejectsAValueThatIsNotNull_ThrowsArgumentException()
    {
        // Arrange - an empty string is not null, but [Required] still rejects it

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Text", string.Empty)
        );

        // Assert
        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Call_WhenTheMethodReturnsNothing_StillChecksItsArguments()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Log", 11)
        );

        // Assert
        Assert.Equal("level", exception.ParamName);
    }

    [Fact]
    public void Call_WhenAnInstanceMethodsArgumentIsValid_CallsItOnTheInstance()
    {
        // Arrange - nothing beyond the arguments

        // Act
        object? result = InterceptedCode.Invoke(Intercepted.Value, Caller, "Grow", 4);

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void Call_WhenAnInstanceMethodsArgumentIsInvalid_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Grow", 11)
        );

        // Assert
        Assert.Equal("by", exception.ParamName);
    }

    [Fact]
    public void Call_WhenAPropertyIsSetOnTheAttribute_UsesItsValue()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Messaged", 11)
        );

        // Assert
        Assert.StartsWith("Out of range", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.5", true)]
    [InlineData("5.5", false)]
    [InlineData("10.5", true)]
    public void Call_WhenAnArgumentIsAType_ChecksAgainstThatType(string text, bool isRejected)
    {
        // Arrange
        decimal value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        Exception? exception = Record.Exception(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Typed", value)
        );

        // Assert
        Assert.Equal(isRejected, exception is ArgumentException);
    }

    [Theory]
    [InlineData(1)]
    [InlineData("two")]
    [InlineData(null)]
    public void Call_WhenAParamsArrayHoldsTheValue_CallsTheOriginalMethod(object? allowed)
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Allowed", allowed)
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Call_WhenAParamsArrayHoldsAnEnum_RebuildsItAsThatEnum()
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "AllowedGreen")
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Call_WhenAParamsArrayDoesNotHoldTheValue_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Allowed", "three")
        );

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData(11, 5, "first")]
    [InlineData(5, 11, "second")]
    public void Call_WhenOneOfSeveralParametersIsInvalid_NamesThatParameter(int first, int second, string invalid)
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Pair", first, second)
        );

        // Assert
        Assert.Equal(invalid, exception.ParamName);
    }
}

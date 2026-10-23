namespace Net.NowhereAtAll.ParameterValidation.Generator.Test;

/// <summary>
/// Which calls the generator intercepts: only those to a plain method with validated parameters
/// that the interceptor can reproduce and call.
/// </summary>
public class CallSiteSelectionTest
{
    private const string InterceptsLocation = "[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(";
    private const string Interceptor = "        public static ";

    [Theory]
    [InlineData(
        "generic method",
        "static void Check<T>([Required] T value) { } static void Run() => Check<string?>(null);"
    )]
    [InlineData(
        "generic type",
        "class Box<T> { public static void Check([Required] string? text) { } } "
        + "static void Run() => Box<int>.Check(null);"
    )]
    [InlineData(
        "by-reference parameter",
        "static void Check([Required] ref string? text) { } static void Run() { string? text = null; Check(ref text); }"
    )]
    [InlineData(
        "by-reference return",
        "static int s_field; static ref int Check([Range(1, 2)] int value) => ref s_field; "
        + "static void Run() => Check(1);"
    )]
    [InlineData(
        "read-only by-reference return",
        "static int s_field; static ref readonly int Check([Range(1, 2)] int value) => ref s_field; "
        + "static void Run() => Check(1);"
    )]
    [InlineData(
        "inaccessible method",
        "private static void Check([Required] string? text) { } static void Run() => Check(null);"
    )]
    [InlineData(
        "local function",
        "static void Run() { Check(null); static void Check([Required] string? text) { } }"
    )]
    [InlineData(
        "delegate",
        "static void Run() { System.Action<string?> check = static text => { }; check(null); }"
    )]
    [InlineData(
        "unchecked parameters",
        "static void Check(string? text) { } static void Run() => Check(null);"
    )]
    [InlineData(
        "other attributes",
        "static void Check([System.ComponentModel.Description(\"text\")] string? text) { } "
        + "static void Run() => Check(null);"
    )]
    [InlineData(
        "not a method",
        "static void Run() { string text = nameof(Run); }"
    )]
    public void Generate_WhenTheCallCannotOrNeedNotBeIntercepted_GeneratesNothing(string reason, string members)
    {
        // Arrange
        string source = $"public static class Target {{ {members} }}";

        // Act
        string? generated = GeneratorRunner.Generate(source);

        // Assert
        Assert.True(generated is null, $"A call with a {reason} was intercepted:\n{generated}");
    }

    [Fact]
    public void Generate_WhenAnExtensionMethodIsCalled_GeneratesNothing()
    {
        // Arrange - the interceptor would have to reproduce the extension's signature
        const string source = """
            public static class Extensions
            {
                public static void Check(this string text, [Required] string? other) { }
            }

            public static class Caller
            {
                public static void Run() => "text".Check(null);
            }
            """;

        // Act
        string? generated = GeneratorRunner.Generate(source);

        // Assert
        Assert.Null(generated);
    }

    [Fact]
    public void Generate_WhenOneMethodIsCalledTwice_WritesOneInterceptorForBothCalls()
    {
        // Arrange
        const string source = """
            public static class Target
            {
                public static void Check([Required] string? text) { }

                public static void Run()
                {
                    Check("first");
                    Check("second");
                }
            }
            """;

        // Act
        string generated = GeneratorRunner.Generate(source)!;

        // Assert
        Assert.Equal(2, Count(generated, InterceptsLocation));
        Assert.Equal(1, Count(generated, Interceptor));
    }

    [Fact]
    public void Generate_WhenTwoMethodsAreCalled_WritesAnInterceptorForEach()
    {
        // Arrange
        const string source = """
            public static class Target
            {
                public static void Check([Required] string? text) { }

                public static void Check([Range(1, 10)] int number) { }

                public static void Run()
                {
                    Check("text");
                    Check(5);
                }
            }
            """;

        // Act
        string generated = GeneratorRunner.Generate(source)!;

        // Assert
        Assert.Equal(2, Count(generated, InterceptsLocation));
        Assert.Equal(2, Count(generated, Interceptor));
    }

    private static int Count(string text, string value)
        => text.Split(value).Length - 1;
}

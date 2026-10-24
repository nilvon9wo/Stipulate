using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// What a woven method does on entry: it checks each validation attribute on each parameter and
/// throws as the attribute's own error message describes, before any of its original code runs.
/// </summary>
public class WovenCheckTest
{
    private const string Target = "Target";

    private const string Source = """
        public enum Color { Red, Green }
        public sealed class NotValidationAttribute : System.Attribute { }
        public sealed class StrictRequiredAttribute : RequiredAttribute { }

        public class Target
        {
            public int Size { get; }

            public Target() { }

            public Target([Range(1, 10)] int size) { this.Size = size; }

            public static int Count([Required, MinLength(1)] int[]? array) => array!.Length;

            public static string Text([Required] string? text) => text!;

            public static string Strict([StrictRequired] string? text) => text!;

            public static int Pair([Range(1, 10)] int first, [Range(1, 10)] int second) => first + second;

            public static Color Paint([AllowedValues(Color.Green)] Color color) => color;

            public static int? Maybe([Range(1, 10)] int? number) => number;

            public static void Put<T>([Required] T value) { }

            public static void PutText(string? text) => Put(text);

            public static void PutNumber(int number) => Put(number);

            public static void Unmarked([NotValidation] string? text) { }

            public static void Referenced([Required] ref string? text) { }

            public static void Read([Required] in string? text) { }

            public static void Written([Required] out string? text) { text = null; }

            public int Grow([Range(1, 10)] int by) => by * 2;

            public static async System.Threading.Tasks.Task<int> LaterAsync([Required] string? text)
            {
                await System.Threading.Tasks.Task.Yield();
                return text!.Length;
            }

            public static int SumPositives([MinLength(1)] int[] numbers)
            {
                int sum = 0;
                foreach (int number in numbers)
                {
                    if (number > 0)
                    {
                        sum += number;
                    }
                }

                return sum;
            }
        }
        """;

    private static readonly Lazy<Assembly> Woven = new(static () => WeavingDirectory.WeaveAndLoad(Source));

    [Fact]
    public void Invoke_WhenEveryArgumentIsValid_RunsTheOriginalCode()
    {
        // Arrange
        int[] array = [1, 2, 3];

        // Act
        object? result = WovenCode.Invoke(Woven.Value, Target, "Count", array);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void Invoke_WhenARequiredArgumentIsNull_ThrowsArgumentNullException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Count", [null])
        );

        // Assert
        Assert.Equal("array", exception.ParamName);
        Assert.StartsWith("The array field is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_WhenAnArgumentIsInvalid_ThrowsArgumentExceptionWithTheAttributesMessage()
    {
        // Arrange
        int[] empty = [];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Count", empty)
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
    public void Invoke_WhenRequiredRejectsAValueThatIsNotNull_ThrowsArgumentException()
    {
        // Arrange - an empty string is not null, but [Required] still rejects it

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Text", string.Empty)
        );

        // Assert
        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAnAttributeDerivedFromRequiredRejectsNull_ThrowsArgumentNullException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Strict", [null])
        );

        // Assert
        Assert.Equal("text", exception.ParamName);
    }

    [Theory]
    [InlineData(11, 5, "first")]
    [InlineData(5, 11, "second")]
    public void Invoke_WhenOneOfSeveralParametersIsInvalid_NamesThatParameter(int first, int second, string invalid)
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Pair", first, second)
        );

        // Assert
        Assert.Equal(invalid, exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAnEnumArgumentIsAllowed_RunsTheOriginalCode()
    {
        // Arrange
        object green = WovenCode.EnumValue(Woven.Value, "Color", "Green");

        // Act
        object? result = WovenCode.Invoke(Woven.Value, Target, "Paint", green);

        // Assert
        Assert.Equal(green, result);
    }

    [Fact]
    public void Invoke_WhenAnEnumArgumentIsNotAllowed_ThrowsArgumentException()
    {
        // Arrange
        object red = WovenCode.EnumValue(Woven.Value, "Color", "Red");

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Paint", red)
        );

        // Assert
        Assert.Equal("color", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public void Invoke_WhenANullableValueIsValid_RunsTheOriginalCode(int? number)
    {
        // Arrange - nothing beyond the arguments

        // Act
        object? result = WovenCode.Invoke(Woven.Value, Target, "Maybe", number);

        // Assert
        Assert.Equal(number, result);
    }

    [Fact]
    public void Invoke_WhenANullableValueIsInvalid_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Maybe", 11)
        );

        // Assert
        Assert.Equal("number", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAGenericArgumentIsNull_ThrowsArgumentNullException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => WovenCode.Invoke(Woven.Value, Target, "PutText", [null])
        );

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAGenericArgumentIsAValueType_ChecksItBoxed()
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "PutNumber", 0));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Invoke_WhenAnAttributeIsNotAValidationAttribute_DoesNotCheckIt()
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "Unmarked", [null]));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Referenced")]
    [InlineData("Read")]
    [InlineData("Written")]
    public void Invoke_WhenAParameterIsByReference_DoesNotCheckIt(string methodName)
    {
        // Arrange - what a by-reference parameter refers to may change, so it isn't checked

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, methodName, [null]));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Invoke_WhenAnInstanceMethodsArgumentIsInvalid_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Grow", 11)
        );

        // Assert
        Assert.Equal("by", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAnInstanceMethodsArgumentIsValid_RunsTheOriginalCode()
    {
        // Arrange - nothing beyond the arguments

        // Act
        object? result = WovenCode.Invoke(Woven.Value, Target, "Grow", 4);

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void Construct_WhenAConstructorsArgumentIsInvalid_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Construct(Woven.Value, Target, 11)
        );

        // Assert
        Assert.Equal("size", exception.ParamName);
    }

    [Fact]
    public void Construct_WhenAConstructorsArgumentIsValid_RunsTheOriginalCode()
    {
        // Arrange - nothing beyond the arguments

        // Act
        object target = WovenCode.Construct(Woven.Value, Target, 7);

        // Assert
        Assert.Equal(7, target.GetType().GetProperty("Size")!.GetValue(target));
    }

    [Fact]
    public void Invoke_WhenAnAsyncMethodsArgumentIsInvalid_ThrowsBeforeReturningATask()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => WovenCode.Invoke(Woven.Value, Target, "LaterAsync", [null])
        );

        // Assert
        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenTheOriginalCodeBranches_StillBranchesCorrectly()
    {
        // Arrange - inserting the checks moves every original instruction, branch targets included
        int[] numbers = [3, -1, 4, -1, 5];

        // Act
        object? result = WovenCode.Invoke(Woven.Value, Target, "SumPositives", numbers);

        // Assert
        Assert.Equal(12, result);
    }
}

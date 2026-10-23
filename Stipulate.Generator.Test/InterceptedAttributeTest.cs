using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Generator.Test;

/// <summary>
/// The interceptor constructs each attribute again, in C#: every kind of constant an attribute
/// argument can hold must be written back as an expression with the same value.
/// </summary>
public class InterceptedAttributeTest
{
    private const string Caller = "Caller";

    private const string Source = """
        using static System.Globalization.CultureInfo;
        using System.Linq;

        /// <summary>Rejects everything, with a message listing what it was constructed with.</summary>
        public sealed class EveryPrimitiveAttribute(
            string text, bool flag, char letter, sbyte tiny, byte octet, short small, ushort unsignedSmall,
            int number, uint unsignedNumber, long large, ulong unsignedLarge, float single, double precise
        ) : ValidationAttribute
        {
            private readonly object[] _values =
            [
                text, flag, letter, tiny, octet, small, unsignedSmall,
                number, unsignedNumber, large, unsignedLarge, single, precise,
            ];

            public override bool IsValid(object? value) => false;

            public override string FormatErrorMessage(string name)
                => string.Join(
                    ",",
                    this._values.Select(static value => System.Convert.ToString(value, InvariantCulture))
                );
        }

        /// <summary>Rejects everything, with a message describing its array arguments.</summary>
        public sealed class ArraysAttribute(int[]? numbers, object[] nested) : ValidationAttribute
        {
            public string[] Names { get; set; } = [];

            public string? Note;

            public override bool IsValid(object? value) => false;

            public override string FormatErrorMessage(string name)
                => $"{(numbers is null ? "null" : string.Join("+", numbers))};"
                    + $"{string.Join("+", nested.Select(Describe))};{string.Join("+", this.Names)};{this.Note}";

            private static string Describe(object? element)
                => element is int[] inner ? $"[{string.Join("+", inner)}]" : $"{element}";
        }

        public sealed class RealsAttribute(float notANumber, double positive, double negative) : ValidationAttribute
        {
            private readonly object[] _values = [notANumber, positive, negative];

            public override bool IsValid(object? value) => false;

            public override string FormatErrorMessage(string name)
                => string.Join(
                    ",",
                    this._values.Select(static value => System.Convert.ToString(value, InvariantCulture))
                );
        }

        public static class Target
        {
            public static void Reals(
                [Reals(float.NaN, double.PositiveInfinity, double.NegativeInfinity)] int value
            ) { }

            public static void Unbounded([Range(0, double.PositiveInfinity)] double value) { }

            public static void Primitives(
                [EveryPrimitive(
                    "te\"xt", true, '\'', -1, 255, -2, 65535, -3, uint.MaxValue, -4L, ulong.MaxValue, 1.5f, 2.5
                )]
                int value
            ) { }

            public static void Arrays(
                [Arrays(null, new object[] { 1, new[] { 2, 3 } }, Names = new[] { "a", "b" }, Note = "n")] int value
            ) { }

            public static void Filled([Arrays(new[] { 4, 5 }, new object[0])] int value) { }
        }

        public static class Caller
        {
            public static void Primitives() => Target.Primitives(0);

            public static void Reals() => Target.Reals(0);

            public static void Unbounded(double value) => Target.Unbounded(value);

            public static void Arrays() => Target.Arrays(0);

            public static void Filled() => Target.Filled(0);
        }
        """;

    private static readonly Lazy<Assembly> Intercepted = new(static () => GeneratorRunner.GenerateAndLoad(Source));

    [Fact]
    public void Call_WhenTheAttributeTakesEveryPrimitive_RebuildsEachValueExactly()
    {
        // Arrange - the string and the char need escaping
        const string expected = "te\"xt,True,',-1,255,-2,65535,-3,4294967295,-4,18446744073709551615,1.5,2.5";

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Primitives")
        );

        // Assert
        Assert.StartsWith(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Call_WhenRealArgumentsAreNotNumbersOrInfinite_RebuildsThemExactly()
    {
        // Arrange - none of these has a literal

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Reals")
        );

        // Assert
        Assert.StartsWith("NaN,Infinity,-Infinity", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1.0, true)]
    [InlineData(1e300, false)]
    public void Call_WhenARangeIsUnbounded_ChecksOnlyItsLowerBound(double value, bool isRejected)
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Unbounded", value)
        );

        // Assert
        Assert.Equal(isRejected, exception is ArgumentException);
    }

    [Fact]
    public void Call_WhenArgumentsAreArrays_RebuildsThemAsArrays()
    {
        // Arrange - a null array, an object array holding an array, and arrays set by name

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Arrays")
        );

        // Assert
        Assert.StartsWith("null;1+[2+3];a+b;n", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Call_WhenArraysAreFilledOrEmpty_RebuildsTheirElements()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => InterceptedCode.Invoke(Intercepted.Value, Caller, "Filled")
        );

        // Assert
        Assert.StartsWith("4+5;;;", exception.Message, StringComparison.Ordinal);
    }
}

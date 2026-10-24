using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// The woven check rebuilds each attribute from the constants stored in metadata: every kind of
/// constant an attribute argument can hold, and every way of setting one.
/// </summary>
public class WovenAttributeTest
{
    private const string Target = "Target";

    private const string Source = """
        using static System.Globalization.CultureInfo;
        using System.Linq;

        public enum Color { Red, Green }

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

        public sealed class NullTextAttribute(string? text) : ValidationAttribute
        {
            private readonly string? _text = text;

            public override bool IsValid(object? value) => false;

            public override string FormatErrorMessage(string name) => this._text ?? "null";
        }

        public sealed class LimitAttribute : ValidationAttribute
        {
            public int Field;

            public int Property { get; set; }

            public override bool IsValid(object? value) => (int)value! <= this.Field + this.Property;
        }

        public static class Target
        {
            public static void Primitives(
                [EveryPrimitive(
                    "text", true, 'c', -1, 255, -2, 65535, -3, uint.MaxValue, -4L, ulong.MaxValue, 1.5f, 2.5
                )]
                int value
            ) { }

            public static void Null([NullText(null)] int value) { }

            public static void Typed([Range(typeof(decimal), "1", "10")] decimal value) { }

            public static void Allowed(
                [AllowedValues(1, "two", null, typeof(int), Color.Green, new[] { 1 })] object? value
            ) { }

            public static void Messaged([Range(1, 10, ErrorMessage = "Out of range")] int value) { }

            public static void Limited([Limit(Field = 3, Property = 4)] int value) { }
        }
        """;

    private static readonly Lazy<Assembly> Woven = new(static () => WeavingDirectory.WeaveAndLoad(Source));

    [Fact]
    public void Invoke_WhenTheAttributeTakesEveryPrimitive_RebuildsEachValueExactly()
    {
        // Arrange
        const string expected = "text,True,c,-1,255,-2,65535,-3,4294967295,-4,18446744073709551615,1.5,2.5";

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Primitives", 0)
        );

        // Assert
        Assert.StartsWith(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_WhenAnArgumentIsNull_RebuildsItAsNull()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Null", 0)
        );

        // Assert
        Assert.StartsWith("null", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("10.5")]
    public void Invoke_WhenAnArgumentIsAType_ChecksAgainstThatType(string outside)
    {
        // Arrange
        decimal value = decimal.Parse(outside, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Typed", value)
        );

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAnArgumentIsATypeAndTheValueIsInside_RunsTheOriginalCode()
    {
        // Arrange - nothing beyond the arguments

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "Typed", 5.5m));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(1)]
    [InlineData("two")]
    [InlineData(null)]
    [InlineData(typeof(int))]
    public void Invoke_WhenAnObjectArrayHoldsTheValue_RunsTheOriginalCode(object? allowed)
    {
        // Arrange - each element is stored boxed, with its own type

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "Allowed", allowed));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Invoke_WhenAnObjectArrayHoldsABoxedEnum_RebuildsItAsThatEnum()
    {
        // Arrange
        object green = WovenCode.EnumValue(Woven.Value, "Color", "Green");

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "Allowed", green));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Invoke_WhenAnObjectArrayDoesNotHoldTheValue_ThrowsArgumentException()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Allowed", "three")
        );

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Invoke_WhenAnInheritedPropertyIsSet_UsesItsValue()
    {
        // Arrange - ErrorMessage is declared by ValidationAttribute, the attribute's base type

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Messaged", 11)
        );

        // Assert
        Assert.StartsWith("Out of range", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    public void Invoke_WhenAFieldAndAPropertyAreSet_UsesBoth(int value, bool isRejected)
    {
        // Arrange - the limit is Field + Property, 3 + 4

        // Act
        Exception? exception = Record.Exception(() => WovenCode.Invoke(Woven.Value, Target, "Limited", value));

        // Assert
        Assert.Equal(isRejected, exception is ArgumentException);
    }
}

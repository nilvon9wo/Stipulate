namespace ArrayFooTest;

/// <summary>
/// The sample's own behaviour, and the weaver enforcing its <c>[Required, MinLength(1)]</c> in an
/// ordinary build.
/// </summary>
public class ArrayMultiplierTest
{
    [Fact]
    public void Multiply_WhenTheArrayIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        int[] array = null!;

        // Act
        void Multiply() => ArrayFoo.ArrayMultiplier.Multiply(array);

        // Assert
        _ = Assert.Throws<ArgumentNullException>(Multiply);
    }

    [Fact]
    public void Multiply_WhenTheArrayIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        int[] array = [];

        // Act
        void Multiply() => ArrayFoo.ArrayMultiplier.Multiply(array);

        // Assert
        _ = Assert.Throws<ArgumentException>(Multiply);
    }

    [Theory]
    [InlineData(new[] { 1 }, new[] { 1 })]
    [InlineData(new[] { 1, 2 }, new[] { 1, 2 })]
    [InlineData(new[] { 4, 7 }, new[] { 4, 28 })]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, new[] { 1, 2, 6, 12, 20 })]
    [InlineData(new[] { 4, 7, 8, 12, 10 }, new[] { 4, 28, 56, 96, 120 })]
    public void Multiply_WhenTheArrayHasElements_MultipliesEachByTheOneBeforeIt(int[] array, int[] expected)
    {
        // Arrange - nothing beyond the data row

        // Act
        int[] result = ArrayFoo.ArrayMultiplier.Multiply(array);

        // Assert
        Assert.Equal(expected, result);
    }
}

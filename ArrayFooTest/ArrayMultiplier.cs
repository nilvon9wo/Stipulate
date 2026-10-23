namespace ArrayFooTest;

public class ArrayMultiplier
{
	[Fact]
	public void TestMultiply_ArrayWithNull_ThrowsArgumentNullException()
	{
		// Arrange
		int[]? input = null;

		// Act
		ArgumentNullException? caughtException = null;
		try
		{
			_ = ArrayFoo.ArrayMultiplier.Multiply(input!);
		}
		catch (ArgumentNullException ex)
		{
			caughtException = ex;
		}

		// Assert
		Assert.NotNull(caughtException);
	}

	[Fact]
	public void TestMultiply_ArrayWithZeroElements_ThrowsException()
	{
		// Arrange
		int[]? input = [];

		// Act
		ArgumentException? caughtException = null;
		try
		{
			_ = ArrayFoo.ArrayMultiplier.Multiply(input);
		}
		catch (ArgumentException ex)
		{
			caughtException = ex;
		}

		// Assert
		Assert.NotNull(caughtException);
	}

	[Fact]
	public void TestMultiply_ArrayWithOneElements_ReturnsCorrectResult_1()
	{
		// Arrange
		int[] input = [1];
		// Act
		int[] result = ArrayFoo.ArrayMultiplier.Multiply(input);

		// Assert
		Assert.Equal([1], result);
	}

	[Fact]
	public void TestMultiply_ArrayWithTwoElements_ReturnsCorrectResult_1()
	{
		// Arrange
		int[] input = [1, 2];
		// Act
		int[] result = ArrayFoo.ArrayMultiplier.Multiply(input);

		// Assert
		Assert.Equal([1, 2], result);
	}

	[Fact]
	public void TestMultiply_ArrayWithTwoElements_ReturnsCorrectResult_2()
	{
		// Arrange
		int[] input = [4, 7];
		// Act
		int[] result = ArrayFoo.ArrayMultiplier.Multiply(input);

		// Assert
		Assert.Equal([4, 28], result);
	}

	[Fact]
	public void TestMultiply_ArrayWithFiveElements_ReturnsCorrectResult_1()
	{
		// Arrange
		int[] input = [1, 2, 3, 4, 5];
		// Act
		int[] result = ArrayFoo.ArrayMultiplier.Multiply(input);

		// Assert
		Assert.Equal([1, 2, 6, 12, 20], result);
	}

	[Fact]
	public void TestMultiply_ArrayWithFiveElements_ReturnsCorrectResult_2()
	{
		// Arrange
		int[] input = [4, 7, 8, 12, 10];
		// Act
		int[] result = ArrayFoo.ArrayMultiplier.Multiply(input);

		// Assert
		Assert.Equal([4, 28, 56, 96, 120], result);
	}
}

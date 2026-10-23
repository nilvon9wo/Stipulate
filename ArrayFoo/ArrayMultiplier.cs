using System.ComponentModel.DataAnnotations;

namespace ArrayFoo;

public class ArrayMultiplier
{
	public static int[] Multiply([Required, MinLength(1)] int[] array)
	{
		int previousValue = 1;
		List<int> result = [];
		foreach (int currentValue in array)
		{
			result.Add(previousValue * currentValue);
			previousValue = currentValue;
		}

		return [.. result];
	}
}

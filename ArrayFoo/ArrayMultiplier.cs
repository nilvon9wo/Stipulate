using System.ComponentModel.DataAnnotations;

namespace ArrayFoo;

/// <summary>The sample that inspired ParameterValidation: its attributes are enforced by the weaver.</summary>
public static class ArrayMultiplier
{
    /// <summary>What the first element is multiplied by, having no element before it.</summary>
    private const int FirstMultiplier = 1;

    /// <summary>Multiplies each element by the one before it.</summary>
    public static int[] Multiply([Required, MinLength(1)] int[] array)
        => [.. array.Prepend(FirstMultiplier).Zip(array, static (previous, current) => previous * current)];
}

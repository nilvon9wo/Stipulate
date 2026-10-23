using System.Collections;

namespace Net.NowhereAtAll.ParameterValidation.Generator.Test;

/// <summary>An array compared by its elements, so the incremental pipeline can cache what holds one.</summary>
public class EquatableArrayTest
{
    [Fact]
    public void Equals_WhenTheElementsAreEqual_IsTrue()
    {
        // Arrange
        EquatableArray<string> first = new(["a", "b"]);
        EquatableArray<string> second = new(["a", "b"]);

        // Act
        bool areEqual = first == second;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData("a", "c")]
    [InlineData("a")]
    public void Equals_WhenTheElementsDiffer_IsFalse(params string[] elements)
    {
        // Arrange
        EquatableArray<string> first = new(["a", "b"]);
        EquatableArray<string> second = new([.. elements]);

        // Act
        bool areDifferent = first != second;

        // Assert
        Assert.True(areDifferent);
    }

    [Fact]
    public void Equals_WhenComparedAsObjects_ComparesTheElements()
    {
        // Arrange
        EquatableArray<string> array = new(["a"]);
        object same = new EquatableArray<string>(["a"]);
        object other = "a";

        // Act
        bool equalsSame = array.Equals(same);
        bool equalsOther = array.Equals(other);

        // Assert
        Assert.True(equalsSame);
        Assert.False(equalsOther);
    }

    [Fact]
    public void GetEnumerator_WhenEnumerated_YieldsTheElementsInOrder()
    {
        // Arrange
        EquatableArray<string> array = new(["a", "b"]);

        // Act
        List<object> untyped = [.. (IEnumerable)array];

        // Assert
        Assert.Equal(["a", "b"], array);
        Assert.Equal(["a", "b"], untyped);
    }
}

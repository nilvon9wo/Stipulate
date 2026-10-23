namespace ParameterValidation.Contracts.Test;

/// <summary>
/// The description of a validation attribute, as the analyzer reads it back. The attribute itself
/// has no behaviour; these prove its two forms record what they are given.
/// </summary>
public class ComparableValidationAttributeTest
{
    [Fact]
    public void Constructor_WhenDescribingItsOwnClass_RecordsNoAttributeType()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ComparableValidationAttribute description = new(ValidationDimensions.Length, ValidationComparison.LowerBound);

        // Assert
        Assert.Null(description.AttributeType);
        Assert.Equal(ValidationDimensions.Length, description.Dimension);
        Assert.Equal(ValidationComparison.LowerBound, description.Comparison);
    }

    [Fact]
    public void Constructor_WhenDescribingAnotherClass_RecordsIt()
    {
        // Arrange
        Type described = typeof(System.ComponentModel.DataAnnotations.EmailAddressAttribute);

        // Act
        ComparableValidationAttribute description = new(described, "Email", ValidationComparison.Flag);

        // Assert
        Assert.Same(described, description.AttributeType);
        Assert.Equal("Email", description.Dimension);
        Assert.Equal(ValidationComparison.Flag, description.Comparison);
    }

    [Fact]
    public void Constructor_WhenNoNamedArgumentsAreGiven_LeavesThemUnset()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ComparableValidationAttribute description = new(ValidationDimensions.Value, ValidationComparison.UpperBound);

        // Assert
        Assert.Null(description.Argument);
        Assert.Null(description.Value);
        Assert.False(description.IsExclusive);
        Assert.Null(description.ExclusiveArgument);
    }

    [Fact]
    public void NamedArguments_WhenSet_AreRecorded()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ComparableValidationAttribute description = new(ValidationDimensions.Value, ValidationComparison.UpperBound)
        {
            Argument = "maximum",
            Value = 10,
            IsExclusive = true,
            ExclusiveArgument = "MaximumIsExclusive",
        };

        // Assert
        Assert.Equal("maximum", description.Argument);
        Assert.Equal(10, description.Value);
        Assert.True(description.IsExclusive);
        Assert.Equal("MaximumIsExclusive", description.ExclusiveArgument);
    }
}

using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers.Test;

/// <summary>Reading an attribute usage's arguments by name.</summary>
public class AttributeArgumentsTest
{
    [Fact]
    public void Find_WhenTheConstructorIsUnknown_ReturnsNull()
    {
        // Arrange
        FakeAttributeData attribute = new();

        // Act
        TypedConstant? argument = AttributeArguments.Find(attribute, "length");

        // Assert
        Assert.Null(argument);
    }
}

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>Recognising a <c>[ComparableValidation]</c>.</summary>
public class ComparableValidationMarkerTest
{
    [Fact]
    public void IsMarker_WhenTheAttributeClassIsUnknown_ReturnsFalse()
    {
        // Arrange
        FakeAttributeData attribute = new();

        // Act
        bool isMarker = ComparableValidationMarker.IsMarker(attribute);

        // Assert
        Assert.False(isMarker);
    }
}

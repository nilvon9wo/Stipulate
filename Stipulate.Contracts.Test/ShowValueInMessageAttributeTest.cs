using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Contracts.Test;

/// <summary>
/// The marker that asks for a parameter's value in a check's message. The attribute itself has no
/// behaviour; the weaver and the generator find it on the parameter, so this proves it is there.
/// </summary>
public class ShowValueInMessageAttributeTest
{
    [Fact]
    public void GetCustomAttribute_WhenAParameterIsMarked_FindsIt()
    {
        // Arrange
        ParameterInfo parameter = typeof(ShowValueInMessageAttributeTest)
            .GetMethod(nameof(Marked), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        // Act
        ShowValueInMessageAttribute? marker = parameter.GetCustomAttribute<ShowValueInMessageAttribute>();

        // Assert
        Assert.NotNull(marker);
    }

    private static void Marked([ShowValueInMessage] string text) => _ = text;
}

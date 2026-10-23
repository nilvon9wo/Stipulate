using System.Reflection;
using ParameterValidation.Contracts;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// The analyzer can't reference ParameterValidation.Contracts (it would have to load it into the
/// compiler), so it mirrors the contracts' enum and constants by value; these keep the two in step.
/// </summary>
public class ContractsMirrorTest
{
    [Fact]
    public void ConstraintKind_WhenComparedWithValidationComparison_HasTheSameNamesAndValues()
    {
        // Arrange
        Dictionary<string, int> contracts = Enum.GetValues<ValidationComparison>()
            .ToDictionary(static comparison => comparison.ToString(), static comparison => (int)comparison);

        // Act
        Dictionary<string, int> mirror = Enum.GetValues<ConstraintKind>()
            .ToDictionary(static kind => kind.ToString(), static kind => (int)kind);

        // Assert
        Assert.Equal(contracts, mirror);
    }

    [Fact]
    public void Dimensions_WhenComparedWithValidationDimensions_HasTheSameConstants()
    {
        // Arrange
        Dictionary<string, object?> contracts = ReadConstants(typeof(ValidationDimensions));

        // Act
        Dictionary<string, object?> mirror = ReadConstants(typeof(Dimensions));

        // Assert
        Assert.Equal(contracts, mirror);
    }

    private static Dictionary<string, object?> ReadConstants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral)
            .ToDictionary(static field => field.Name, static field => field.GetRawConstantValue());
}

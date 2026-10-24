namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>What each kind of constraint is: a bound, or not, and what it makes a statement about.</summary>
public class ConstraintComparisonsTest
{
    [Theory]
    [InlineData((int)ConstraintKind.Flag, false)]
    [InlineData((int)ConstraintKind.LowerBound, true)]
    [InlineData((int)ConstraintKind.UpperBound, true)]
    [InlineData((int)ConstraintKind.AllowedValues, false)]
    [InlineData((int)ConstraintKind.DeniedValues, false)]
    public void Of_WhenGivenAKind_SaysWhetherItIsABound(int kind, bool expected)
    {
        // Arrange - nothing beyond the data row

        // Act
        IConstraintComparing comparison = ConstraintComparisons.Of((ConstraintKind)kind);

        // Assert
        Assert.Equal(expected, comparison.IsBound);
    }

    [Theory]
    [InlineData((int)ConstraintKind.Flag, (int)ConstraintKind.Flag)]
    [InlineData((int)ConstraintKind.LowerBound, (int)ConstraintKind.LowerBound)]
    [InlineData((int)ConstraintKind.UpperBound, (int)ConstraintKind.UpperBound)]
    [InlineData((int)ConstraintKind.AllowedValues, (int)ConstraintKind.AllowedValues)]
    [InlineData((int)ConstraintKind.DeniedValues, (int)ConstraintKind.AllowedValues)]
    public void Of_WhenGivenAKind_SaysWhatItMakesAStatementAbout(int kind, int expected)
    {
        // Arrange - nothing beyond the data row

        // Act
        IConstraintComparing comparison = ConstraintComparisons.Of((ConstraintKind)kind);

        // Assert
        Assert.Equal((ConstraintKind)expected, comparison.StatementKind);
    }
}

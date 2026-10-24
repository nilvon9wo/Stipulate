namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>
/// A real build of a consumer project, in each StipulateMode, and what the program it
/// produces then does with an invalid argument.
/// </summary>
/// <remarks>
/// The builds run one at a time (xunit.runner.json): each restores the tools' projects, which share
/// their obj folders.
/// </remarks>
public class BuildModeTest
{
    private const string Validated = "validated: value";
    private const string NotValidated = "not validated";

    private const string Program = """
        using System.ComponentModel.DataAnnotations;

        try
        {
            Target.Check(0);
            Console.WriteLine("not validated");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"validated: {exception.ParamName}");
        }

        public static class Target
        {
            public static void Check([Range(1, 10)] int value) { }
        }
        """;

    [Theory]
    [InlineData("Weaving", Validated)]
    [InlineData("Interceptors", Validated)]
    [InlineData("None", NotValidated)]
    public void Build_InEachMode_ValidatesOnlyWhenEnforcing(string mode, string expected)
    {
        // Arrange
        using ConsumerProject project = ConsumerProject.Create(Program);
        _ = project.Build($"StipulateMode={mode}").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal(expected, run.Output.Trim());
    }

    [Fact]
    public void Build_ByDefault_Weaves()
    {
        // Arrange
        using ConsumerProject project = ConsumerProject.Create(Program);

        // Act
        ProcessResult build = project.Build().AssertSucceeded();

        // Assert
        Assert.Contains(
            "Stipulate: wove 1 method(s) in Consumer.dll.",
            build.Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Build_InInterceptorsMode_DoesNotWeave()
    {
        // Arrange
        using ConsumerProject project = ConsumerProject.Create(Program);

        // Act
        ProcessResult build = project.Build("StipulateMode=Interceptors").AssertSucceeded();

        // Assert
        Assert.DoesNotContain("Stipulate: wove", build.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenNothingChanged_DoesNotWeaveAgain()
    {
        // Arrange - the second build skips compiling, so the assembly in obj/ is already woven
        using ConsumerProject project = ConsumerProject.Create(Program);
        _ = project.Build().AssertSucceeded();

        // Act
        ProcessResult build = project.Build().AssertSucceeded();

        // Assert
        Assert.Contains("Stipulate: Consumer.dll is already woven.", build.Output, StringComparison.Ordinal);
        Assert.Equal(Validated, project.RunProgram().AssertSucceeded().Output.Trim());
    }
}

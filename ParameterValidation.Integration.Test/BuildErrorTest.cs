namespace Net.NowhereAtAll.ParameterValidation.Integration.Test;

/// <summary>
/// The errors a consumer's build reports, with their codes, when ParameterValidation can't do its job.
/// </summary>
public class BuildErrorTest
{
    private const string Program = """
        using System.ComponentModel.DataAnnotations;

        Target.Check(1);

        public static class Target
        {
            public static void Check([Range(1, 10)] int value) { }
        }
        """;

    [Fact]
    public void Build_WhenTheWeaverHasNotBeenBuilt_ReportsPV0005()
    {
        // Arrange
        using ConsumerProject project = ConsumerProject.Create(Program);
        string missing = Path.Combine(project.Path, "missing", "Weaver.dll");

        // Act
        ProcessResult build = project.Build($"ParameterValidationWeaverPath={missing}");

        // Assert
        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains("error PV0005", build.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenTheWeaverCannotStart_ReportsPV0004()
    {
        // Arrange - dotnet refuses to run a file that isn't an assembly
        using ConsumerProject project = ConsumerProject.Create(Program);
        project.Write("Broken.dll", "not an assembly");
        string broken = Path.Combine(project.Path, "Broken.dll");

        // Act
        ProcessResult build = project.Build($"ParameterValidationWeaverPath={broken}");

        // Assert
        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains("error PV0004", build.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenAnImplementationDropsAPrecondition_ReportsPV1002()
    {
        // Arrange - the analyzer runs in the build, and its warnings fail it
        using ConsumerProject project = ConsumerProject.Create("""
            using System.ComponentModel.DataAnnotations;

            new Scale().Set(1);

            public interface IScale { void Set([Range(1, 10)] int level); }

            public class Scale : IScale
            {
                public void Set(int level) { }
            }
            """);

        // Act
        ProcessResult build = project.Build();

        // Assert
        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains("error PV1002", build.Output, StringComparison.Ordinal);
    }
}

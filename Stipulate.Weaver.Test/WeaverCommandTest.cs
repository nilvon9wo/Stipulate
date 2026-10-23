using System.Reflection;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// The weaver as the build runs it: its arguments, what it prints, and its exit codes, which
/// Directory.Build.targets tells apart.
/// </summary>
public class WeaverCommandTest
{
    private const string Checked = "public static class Target { public static void Run([Required] string? text) { } }";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Run_WhenGivenTheWrongNumberOfArguments_ReportsTheUsage(int argumentCount)
    {
        // Arrange
        string[] arguments = [.. Enumerable.Repeat("argument", argumentCount)];
        using StringWriter error = new();

        // Act
        int exitCode = WeaverCommand.Run(arguments, TextWriter.Null, error);

        // Assert
        Assert.Equal(WeaverCommand.ReportedFailureExitCode, exitCode);
        Assert.Equal(
            "Usage: Net.NowhereAtAll.Stipulate.Weaver <assembly> <references-file>",
            error.ToString().TrimEnd()
        );
    }

    [Fact]
    public void Run_WhenItWeaves_ReportsHowManyMethods()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(Checked);
        using StringWriter output = new();

        // Act
        int exitCode = WeaverCommand.Run([directory.AssemblyPath, directory.ReferencesPath], output, TextWriter.Null);

        // Assert
        Assert.Equal(WeaverCommand.SuccessExitCode, exitCode);
        Assert.Equal("Stipulate: wove 1 method(s) in Test.dll.", output.ToString().TrimEnd());
    }

    [Fact]
    public void Run_WhenTheAssemblyIsAlreadyWoven_SaysSo()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(Checked);
        _ = directory.Weave();
        using StringWriter output = new();

        // Act
        int exitCode = WeaverCommand.Run([directory.AssemblyPath, directory.ReferencesPath], output, TextWriter.Null);

        // Assert
        Assert.Equal(WeaverCommand.SuccessExitCode, exitCode);
        Assert.Equal("Stipulate: Test.dll is already woven.", output.ToString().TrimEnd());
    }

    [Fact]
    public void Run_WhenWeavingFails_ReportsAnMSBuildError()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(
            "public static class Target { public static void Run([Positive] int number) { } }",
            TestCompilation.CompileToImage(
                "public sealed class PositiveAttribute : ValidationAttribute { }",
                "Library"
            )
        );
        using StringWriter error = new();

        // Act
        int exitCode = WeaverCommand.Run([directory.AssemblyPath, directory.ReferencesPath], TextWriter.Null, error);

        // Assert
        Assert.Equal(WeaverCommand.ReportedFailureExitCode, exitCode);
        Assert.Equal(
            $"{directory.AssemblyPath} : error ST0001: Could not resolve type 'PositiveAttribute': its assembly "
                + "'Library, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' is not among the references.",
            error.ToString().TrimEnd()
        );
    }

    [Fact]
    public void Run_WhenTheWeaverFailsUnexpectedly_ReportsAnMSBuildErrorWithTheException()
    {
        // Arrange
        using WeavingDirectory directory = WeavingDirectory.Create();
        directory.Compile(Checked);
        File.Delete(directory.ReferencesPath);
        using StringWriter error = new();

        // Act
        int exitCode = WeaverCommand.Run([directory.AssemblyPath, directory.ReferencesPath], TextWriter.Null, error);

        // Assert
        Assert.Equal(WeaverCommand.ReportedFailureExitCode, exitCode);
        Assert.StartsWith(
            $"{directory.AssemblyPath} : error ST0003: Unexpected FileNotFoundException: ",
            error.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Main_WhenRun_ExitsWithTheCommandsExitCode()
    {
        // Arrange - with no arguments it only prints the usage
        MethodInfo main = typeof(WeaverCommand).Assembly.EntryPoint!;

        // Act
        object? exitCode = main.Invoke(null, [Array.Empty<string>()]);

        // Assert
        Assert.Equal(WeaverCommand.ReportedFailureExitCode, exitCode);
    }
}

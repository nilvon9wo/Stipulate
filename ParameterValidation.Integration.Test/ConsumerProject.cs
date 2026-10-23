using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Net.NowhereAtAll.ParameterValidation.Integration.Test;

/// <summary>
/// A console project in a temporary directory that picks up this repository's Directory.Build.props
/// and Directory.Build.targets, as every project in the repository does.
/// </summary>
internal sealed class ConsumerProject : IDisposable
{
    private const string Name = "Consumer";
    private const string DirectoryPrefix = "ParameterValidation.Integration.Test.";
    private const string DotnetHostPath = "DOTNET_HOST_PATH";
    private const string Dotnet = "dotnet";

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private static readonly string RepositoryRoot = Metadata("RepositoryRoot");
    private static readonly string Configuration = Metadata("Configuration");

    private ConsumerProject(string path) => this.Path = path;

    public string Path { get; }

    /// <param name="program">The project's Program.cs.</param>
    public static ConsumerProject Create([StringSyntax("C#")] string program)
    {
        ConsumerProject project = new(Directory.CreateTempSubdirectory(DirectoryPrefix).FullName);
        project.Write("Directory.Build.props", Import("Directory.Build.props"));
        project.Write("Directory.Build.targets", Import("Directory.Build.targets"));
        project.Write(Name + ".csproj", ProjectFile);
        project.Write("Program.cs", program);
        return project;
    }

    /// <summary>
    /// Builds the project; the tools it references are already built, in this configuration, and
    /// are not built again.
    /// </summary>
    /// <param name="properties">MSBuild properties, as <c>Name=Value</c>.</param>
    public ProcessResult Build(params string[] properties)
        => Run(
            this.Path,
            [
                "build",
                "--nologo",
                "--configuration", Configuration,
                "-p:BuildProjectReferences=false",
                .. properties.Select(static property => "-p:" + property),
            ]
        );

    /// <summary>Runs the built program.</summary>
    public ProcessResult RunProgram()
        => Run(this.Path, [System.IO.Path.Combine(this.Path, "bin", Configuration, "net10.0", Name + ".dll")]);

    public void Write(string fileName, string contents)
        => File.WriteAllText(System.IO.Path.Combine(this.Path, fileName), contents);

    public void Dispose() => Directory.Delete(this.Path, recursive: true);

    private static string Import(string fileName)
        => $"<Project><Import Project=\"{System.IO.Path.Combine(RepositoryRoot, fileName)}\" /></Project>";

    private static ProcessResult Run(string workingDirectory, IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable(DotnetHostPath) ?? Dotnet, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output + error.Result);
    }

    private static string Metadata(string key)
        => typeof(ConsumerProject).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key)
            .Value!;
}

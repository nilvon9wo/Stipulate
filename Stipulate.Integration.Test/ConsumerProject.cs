using System.Diagnostics.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>
/// A project in a temporary directory that uses Stipulate: either as every project in this
/// repository does, through its Directory.Build.props and Directory.Build.targets, or as a
/// consumer of the package does, through a PackageReference.
/// </summary>
internal sealed class ConsumerProject : IDisposable
{
    private const string Name = "Consumer";
    private const string DirectoryPrefix = "Stipulate.Integration.Test.";
    private const string NetTarget = "net10.0";

    private readonly string _targetFramework;

    private ConsumerProject(string path, string targetFramework)
    {
        this.Path = path;
        this._targetFramework = targetFramework;
    }

    public string Path { get; }

    /// <summary>A console program wired to this repository's projects.</summary>
    /// <param name="program">The project's Program.cs.</param>
    public static ConsumerProject Create([StringSyntax("C#")] string program)
    {
        ConsumerProject project = CreateEmpty(NetTarget);
        project.Write("Directory.Build.props", Import("Directory.Build.props"));
        project.Write("Directory.Build.targets", Import("Directory.Build.targets"));
        project.Write(Name + ".csproj", ProjectFile("Exe", NetTarget, string.Empty));
        project.Write("Program.cs", program);
        return project;
    }

    /// <summary>
    /// A project that references the Stipulate package from <paramref name="feed"/>, restored into
    /// a package folder of its own, so that no other build's copy of the same version is used.
    /// </summary>
    /// <param name="feed">Where the package comes from.</param>
    /// <param name="source">The project's one source file, Program.cs.</param>
    /// <param name="outputType">The project's OutputType: Exe or Library.</param>
    /// <param name="targetFramework">The project's TargetFramework.</param>
    /// <param name="items">More of the project file's items, such as package references.</param>
    public static ConsumerProject CreateFromPackage(
        PackageFeed feed,
        [StringSyntax("C#")] string source,
        string outputType = "Exe",
        string targetFramework = NetTarget,
        string items = ""
    )
    {
        ConsumerProject project = CreateEmpty(targetFramework);
        project.Write("nuget.config", feed.NuGetConfig);
        string packageReference = $"""<PackageReference Include="Stipulate" Version="{Dotnet.PackageVersion}" />""";
        string packages = System.IO.Path.Combine(project.Path, "packages");
        string properties = $"<RestorePackagesPath>{packages}</RestorePackagesPath>";
        project.Write(Name + ".csproj", ProjectFile(outputType, targetFramework, properties, packageReference + items));
        project.Write("Program.cs", source);
        return project;
    }

    /// <summary>
    /// Builds the project; the tools it uses are already built, in this configuration, and are not
    /// built again.
    /// </summary>
    /// <param name="properties">MSBuild properties, as <c>Name=Value</c>.</param>
    public ProcessResult Build(params string[] properties)
        => Dotnet.Run(
            this.Path,
            [
                "build",
                "--nologo",
                "--configuration", Dotnet.Configuration,
                "-p:BuildProjectReferences=false",
                .. properties.Select(static property => "-p:" + property),
            ]
        );

    /// <summary>Runs the built program.</summary>
    public ProcessResult RunProgram() => Dotnet.Run(this.Path, [this.OutputPath(".dll")]);

    /// <summary>Where the build put the project's assembly, or another of its outputs.</summary>
    public string OutputPath(string extension)
        => System.IO.Path.Combine(this.Path, "bin", Dotnet.Configuration, this._targetFramework, Name + extension);

    public void Write(string fileName, string contents)
        => File.WriteAllText(System.IO.Path.Combine(this.Path, fileName), contents);

    public void Dispose() => Directory.Delete(this.Path, recursive: true);

    private static ConsumerProject CreateEmpty(string targetFramework)
        => new(Directory.CreateTempSubdirectory(DirectoryPrefix).FullName, targetFramework);

    private static string Import(string fileName)
        => $"""<Project><Import Project="{System.IO.Path.Combine(Dotnet.RepositoryRoot, fileName)}" /></Project>""";

    private static string ProjectFile(string outputType, string targetFramework, string properties, string items = "")
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>{outputType}</OutputType>
                <TargetFramework>{targetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <LangVersion>latest</LangVersion>
                {properties}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """;
}

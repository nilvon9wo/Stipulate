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
    private const string LibraryName = "Library";
    private const string MiddleLibraryName = "Middle";

    /// <summary>The temporary directory, holding the project and any other it references.</summary>
    private readonly string _directory;
    private readonly string _targetFramework;
    private readonly bool _buildsProjectReferences;

    private ConsumerProject(string directory, string path, string targetFramework, bool buildsProjectReferences)
    {
        this._directory = directory;
        this.Path = path;
        this._targetFramework = targetFramework;
        this._buildsProjectReferences = buildsProjectReferences;
    }

    public string Path { get; }

    private static string PackageReference
        => $"""<PackageReference Include="Stipulate" Version="{Dotnet.PackageVersion}" />""";

    /// <summary>A console program wired to this repository's projects.</summary>
    /// <param name="program">The project's Program.cs.</param>
    public static ConsumerProject Create([StringSyntax("C#")] string program)
    {
        string directory = CreateDirectory();
        ConsumerProject project = new(directory, directory, NetTarget, buildsProjectReferences: false);
        project.Write("Directory.Build.props", Import("Directory.Build.props"));
        project.Write("Directory.Build.targets", Import("Directory.Build.targets"));
        project.Write(Name + ".csproj", ProjectFile("Exe", NetTarget, string.Empty));
        project.Write("Program.cs", program);
        return project;
    }

    /// <summary>
    /// A project that references the Stipulate package from <paramref name="feed"/>.
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
        string directory = CreateDirectory();
        ConsumerProject project = new(directory, directory, targetFramework, buildsProjectReferences: true);
        WriteFeed(directory, feed);
        string projectFile = ProjectFile(outputType, targetFramework, string.Empty, PackageReference + items);
        project.Write(Name + ".csproj", projectFile);
        project.Write("Program.cs", source);
        return project;
    }

    /// <summary>
    /// A console program that doesn't reference the Stipulate package itself, but references a
    /// library that does, directly or through other libraries: it gets the package only through
    /// the library.
    /// </summary>
    /// <param name="feed">Where the package comes from.</param>
    /// <param name="libraryReference">
    /// How the program references the library, and each library the one before it; a package is
    /// packed here, with the library's defaults, into a feed of the project's own.
    /// </param>
    /// <param name="library">The library's one source file.</param>
    /// <param name="program">The program's Program.cs.</param>
    /// <param name="librariesBetween">How many libraries, with nothing of Stipulate's in them, stand between.</param>
    public static ConsumerProject CreateOnLibrary(
        PackageFeed feed,
        LibraryReference libraryReference,
        [StringSyntax("C#")] string library,
        [StringSyntax("C#")] string program,
        int librariesBetween = 0
    )
    {
        string directory = CreateDirectory();
        WriteFeed(directory, feed);
        string reference = PackageReference;
        for (int index = 0; index <= librariesBetween; index++)
        {
            string name = index == 0 ? LibraryName : MiddleLibraryName + index;
            string source = index == 0 ? library : $"public static class {name} {{ }}";
            reference = WriteLibrary(directory, name, source, reference, libraryReference);
        }

        string path = System.IO.Path.Combine(directory, Name);
        _ = Directory.CreateDirectory(path);
        ConsumerProject project = new(directory, path, NetTarget, buildsProjectReferences: true);
        project.Write(Name + ".csproj", ProjectFile("Exe", NetTarget, string.Empty, reference));
        project.Write("Program.cs", program);
        return project;
    }

    /// <summary>
    /// Builds the project. A project wired to this repository doesn't build its references: the
    /// tools it uses are already built, in this configuration.
    /// </summary>
    /// <param name="properties">MSBuild properties, as <c>Name=Value</c>.</param>
    public ProcessResult Build(params string[] properties)
        => Dotnet.Run(
            this.Path,
            [
                "build",
                "--nologo",
                "--configuration", Dotnet.Configuration,
                "-p:BuildProjectReferences=" + this._buildsProjectReferences,
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

    public void Dispose() => Directory.Delete(this._directory, recursive: true);

    private static string CreateDirectory() => Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;

    private static string LocalFeed(string directory) => System.IO.Path.Combine(directory, "feed");

    /// <summary>Writes a library that has <paramref name="reference"/>, packed if it is to be a package.</summary>
    /// <returns>The item by which the next project references this library.</returns>
    private static string WriteLibrary(
        string directory,
        string name,
        string source,
        string reference,
        LibraryReference libraryReference
    )
    {
        string libraryPath = Directory.CreateDirectory(System.IO.Path.Combine(directory, name)).FullName;
        string libraryProject = System.IO.Path.Combine(libraryPath, name + ".csproj");
        File.WriteAllText(libraryProject, ProjectFile("Library", NetTarget, string.Empty, reference));
        File.WriteAllText(System.IO.Path.Combine(libraryPath, name + ".cs"), source);
        if (libraryReference == LibraryReference.Project)
        {
            return $"""<ProjectReference Include="{libraryProject}" />""";
        }

        string[] pack =
        [
            "pack", libraryProject,
            "--nologo",
            "--configuration", Dotnet.Configuration,
            "--output", LocalFeed(directory),
        ];
        _ = Dotnet.Run(directory, pack).AssertSucceeded();
        return $"""<PackageReference Include="{name}" Version="1.0.0" />""";
    }

    /// <summary>
    /// The feeds, for every project under <paramref name="directory"/>, restored into a package
    /// folder of its own, so that no other build's copy of the same version is used.
    /// </summary>
    private static void WriteFeed(string directory, PackageFeed feed)
    {
        string localFeed = Directory.CreateDirectory(LocalFeed(directory)).FullName;
        File.WriteAllText(System.IO.Path.Combine(directory, "nuget.config"), feed.NuGetConfig(localFeed));
        string packages = System.IO.Path.Combine(directory, "packages");
        File.WriteAllText(
            System.IO.Path.Combine(directory, "Directory.Build.props"),
            $"<Project><PropertyGroup><RestorePackagesPath>{packages}</RestorePackagesPath></PropertyGroup></Project>"
        );
    }

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

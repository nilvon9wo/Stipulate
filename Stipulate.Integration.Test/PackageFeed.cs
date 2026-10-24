namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>
/// The Stipulate package, packed once for the whole test run from this configuration's build, in
/// a folder that serves as a NuGet feed.
/// </summary>
public sealed class PackageFeed : IDisposable
{
    private const string PackageProject = "Stipulate.Package";

    public PackageFeed()
    {
        this.Path = Directory.CreateTempSubdirectory("Stipulate.Integration.Test.Feed.").FullName;
        string project = System.IO.Path.Combine(Dotnet.RepositoryRoot, PackageProject);
        this.PackResult = Dotnet.Run(
            Dotnet.RepositoryRoot,
            ["pack", project, "--nologo", "--configuration", Dotnet.Configuration, "--no-build", "--output", this.Path]
        );
    }

    public string Path { get; }

    /// <summary>What packing reported; a test that needs the package asserts it succeeded.</summary>
    public ProcessResult PackResult { get; }

    public string PackagePath => System.IO.Path.Combine(this.Path, $"Stipulate.{Dotnet.PackageVersion}.nupkg");

    /// <summary>
    /// A nuget.config that finds Stipulate here, packages a test packs itself in
    /// <paramref name="localFeed"/>, and everything else on nuget.org.
    /// </summary>
    public string NuGetConfig(string localFeed)
        => $"""
            <configuration>
              <packageSources>
                <clear />
                <add key="Stipulate" value="{this.Path}" />
                <add key="Local" value="{localFeed}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;

    public void Dispose() => Directory.Delete(this.Path, recursive: true);
}

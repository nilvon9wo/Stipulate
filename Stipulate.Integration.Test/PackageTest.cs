using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

[assembly: AssemblyFixture(typeof(Net.NowhereAtAll.Stipulate.Integration.Test.PackageFeed))]

namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>
/// The Stipulate package as a consumer gets it: what it contains, and what a project referencing
/// it gets, in each mode and for each kind of target framework.
/// </summary>
public class PackageTest(PackageFeed feed)
{
    private const string Validated = "validated: value";

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

    /// <summary>A library with nothing but a validated method, for any target framework.</summary>
    private const string Library = """
        using System.ComponentModel.DataAnnotations;

        public static class Target
        {
            public static void Check([Range(1, 10)] int value) { }
        }
        """;

    /// <summary>A library with a contract, and a validated method, for a program to build on.</summary>
    private const string ContractLibrary = """
        using System.ComponentModel.DataAnnotations;

        public interface IScale { void Set([Range(1, 10)] int level); }

        public static class Levels
        {
            public static void Check([Range(1, 10)] int level) { }
        }
        """;

    /// <summary>Calls the library's validated method, then its own, saying of each whether it validated.</summary>
    private const string ProgramOnLibrary = """
        using System.ComponentModel.DataAnnotations;

        Console.WriteLine(Validates(() => Levels.Check(0)));
        Console.WriteLine(Validates(() => Target.Check(0)));

        static string Validates(Action call)
        {
            try
            {
                call();
                return "not validated";
            }
            catch (ArgumentException)
            {
                return "validated";
            }
        }

        public static class Target
        {
            public static void Check([Range(1, 10)] int value) { }
        }
        """;

    /// <summary>Implements the library's contract, demanding more than it accepts.</summary>
    private const string StrongerScale = """

        public class Scale : IScale
        {
            public void Set([Range(2, 10)] int level) { }
        }
        """;

    private static readonly string[] ExpectedFiles =
    [
        "README.md",
        "analyzers/dotnet/cs/Net.NowhereAtAll.Stipulate.Analyzers.dll",
        "analyzers/dotnet/cs/Net.NowhereAtAll.Stipulate.CodeFixes.dll",
        "analyzers/dotnet/cs/Net.NowhereAtAll.Stipulate.Generator.dll",
        "buildTransitive/Stipulate.targets",
        "lib/netstandard2.0/Net.NowhereAtAll.Stipulate.Contracts.dll",
        "lib/netstandard2.0/Net.NowhereAtAll.Stipulate.Contracts.xml",
        "tools/net8.0/Mono.Cecil.Mdb.dll",
        "tools/net8.0/Mono.Cecil.Pdb.dll",
        "tools/net8.0/Mono.Cecil.Rocks.dll",
        "tools/net8.0/Mono.Cecil.dll",
        "tools/net8.0/Net.NowhereAtAll.Stipulate.Weaver.deps.json",
        "tools/net8.0/Net.NowhereAtAll.Stipulate.Weaver.dll",
        "tools/net8.0/Net.NowhereAtAll.Stipulate.Weaver.runtimeconfig.json",
    ];

    private readonly PackageFeed _feed = feed;

    [Fact]
    public void Pack_WhenPacked_ContainsEachPartWhereNuGetLooksForIt()
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ZipArchive package = ZipFile.OpenRead(this._feed.PackagePath);

        // Act
        string[] files = [.. package.Entries
            .Select(static entry => entry.FullName)
            .Where(IsContent)
            .Order(StringComparer.Ordinal)];

        // Assert
        Assert.Equal(ExpectedFiles, files);
    }

    [Fact]
    public void Pack_WhenPacked_HasNoDependencies()
    {
        // Arrange - the analyzer, code fix, generator and weaver are tools, and Contracts has none
        _ = this._feed.PackResult.AssertSucceeded();
        using ZipArchive package = ZipFile.OpenRead(this._feed.PackagePath);
        using Stream nuspec = package.GetEntry("Stipulate.nuspec")!.Open();

        // Act
        XElement[] dependencies = [.. XDocument.Load(nuspec)
            .Descendants()
            .Where(static element => element.Name.LocalName == "dependency")];

        // Assert
        Assert.Empty(dependencies);
    }

    [Theory]
    [InlineData("Weaving", Validated)]
    [InlineData("Interceptors", Validated)]
    [InlineData("None", "not validated")]
    public void Build_WhenTheProjectReferencesThePackage_ValidatesOnlyWhenEnforcing(string mode, string expected)
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(this._feed, Program);
        _ = project.Build($"StipulateMode={mode}").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal(expected, run.Output.Trim());
    }

    [Fact]
    public void Build_WhenTheProjectReferencesThePackage_RunsTheAnalyzer()
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            """
            using System.ComponentModel.DataAnnotations;

            new Scale().Set(1);

            public interface IScale { void Set([Range(1, 10)] int level); }

            public class Scale : IScale
            {
                public void Set(int level) { }
            }
            """
        );

        // Act
        ProcessResult build = project.Build().AssertSucceeded();

        // Assert
        Assert.Contains("warning ST1002", build.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenALibraryTargetsNetStandard_WeavesIt()
    {
        // Arrange - netstandard2.0 gets ValidationAttribute from a package, not the framework
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            Library,
            outputType: "Library",
            targetFramework: "netstandard2.0",
            items: """<PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" />"""
        );
        _ = project.Build().AssertSucceeded();
        Assembly library = new AssemblyLoadContext(nameof(PackageTest), isCollectible: true)
            .LoadFromStream(new MemoryStream(File.ReadAllBytes(project.OutputPath(".dll"))));
        MethodInfo check = library.GetType("Target", throwOnError: true)!.GetMethod("Check")!;

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => check.Invoke(null, BindingFlags.DoNotWrapExceptions, binder: null, [0], culture: null)
        );

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Build_WhenAProjectTargetsTheNetFramework_WeavesIt()
    {
        // Arrange - the woven IL binds to mscorlib, not System.Runtime; it can only be run on Windows
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            Library,
            outputType: "Library",
            targetFramework: "net472",
            items: """<Reference Include="System.ComponentModel.DataAnnotations" />"""
        );

        // Act
        ProcessResult build = project.Build().AssertSucceeded();

        // Assert
        Assert.Contains("Stipulate: wove 1 method(s) in Consumer.dll.", build.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LibraryReference.Project, 0)]
    [InlineData(LibraryReference.Package, 0)]
    [InlineData(LibraryReference.Project, 2)]
    [InlineData(LibraryReference.Package, 2)]
    public void Build_WhenAProjectGetsThePackageOnlyThroughALibrary_ChecksItsContracts(
        LibraryReference reference,
        int librariesBetween
    )
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateOnLibrary(
            this._feed,
            reference,
            ContractLibrary,
            ProgramOnLibrary + StrongerScale,
            librariesBetween
        );

        // Act
        ProcessResult build = project.Build();

        // Assert
        Assert.Contains("error ST1001", build.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LibraryReference.Project, 0)]
    [InlineData(LibraryReference.Package, 0)]
    [InlineData(LibraryReference.Project, 2)]
    [InlineData(LibraryReference.Package, 2)]
    public void Build_WhenAProjectGetsThePackageOnlyThroughALibrary_WeavesIt(
        LibraryReference reference,
        int librariesBetween
    )
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateOnLibrary(
            this._feed,
            reference,
            ContractLibrary,
            ProgramOnLibrary,
            librariesBetween
        );
        _ = project.Build().AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal(["validated", "validated"], run.Output.Trim().Split('\n', StringSplitOptions.TrimEntries));
    }

    [Theory]
    [InlineData(LibraryReference.Project, 0)]
    [InlineData(LibraryReference.Package, 0)]
    [InlineData(LibraryReference.Project, 2)]
    [InlineData(LibraryReference.Package, 2)]
    public void Build_WhenTransitiveIsOff_LeavesAProjectThatGetsThePackageOnlyThroughALibraryAlone(
        LibraryReference reference,
        int librariesBetween
    )
    {
        // Arrange - for every project built; the library, which references the package, is still woven
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateOnLibrary(
            this._feed,
            reference,
            ContractLibrary,
            ProgramOnLibrary + StrongerScale,
            librariesBetween
        );
        ProcessResult build = project.Build("StipulateTransitive=false").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.DoesNotContain("ST1001", build.Output, StringComparison.Ordinal);
        Assert.Equal(["validated", "not validated"], run.Output.Trim().Split('\n', StringSplitOptions.TrimEntries));
    }

    /// <summary>Leaves out what every package has: its manifest, and the packaging format's own parts.</summary>
    private static bool IsContent(string entry)
        => !entry.EndsWith(".nuspec", StringComparison.Ordinal)
            && !entry.StartsWith('[')
            && !entry.StartsWith("_rels/", StringComparison.Ordinal)
            && !entry.StartsWith("package/", StringComparison.Ordinal);
}

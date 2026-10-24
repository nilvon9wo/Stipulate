namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>
/// Visual Basic and F# projects that get the Stipulate package: woven with the check in their own
/// language, as C# projects are; not enforced in Interceptors mode, a C# feature, and told so; and,
/// for F#, checked on the calls StipulateFSharpChecks guarantees, though F#'s optimizer inlines.
/// </summary>
public class OtherLanguagesTest(PackageFeed feed)
{
    /// <summary>Calls a validated method of its own, and says whether it validated.</summary>
    private const string VisualBasicProgram = """
        Imports System
        Imports System.ComponentModel.DataAnnotations

        Public Module Program
            Public Sub Main()
                Try
                    Target.Check(0)
                    Console.WriteLine("not validated")
                Catch rejection As ArgumentException
                    Console.WriteLine("validated: " & rejection.ParamName)
                End Try
            End Sub
        End Module

        Public Class Target
            Public Shared Sub Check(<Range(1, 10)> value As Integer)
            End Sub
        End Class
        """;

    /// <summary><see cref="VisualBasicProgram"/> in F#.</summary>
    private const string FSharpProgram = """
        open System
        open System.ComponentModel.DataAnnotations

        type Target =
            static member Check([<Range(1, 10)>] value: int) = ()

        try
            Target.Check(0)
            Console.WriteLine("not validated")
        with :? ArgumentException as rejection ->
            Console.WriteLine("validated: " + rejection.ParamName)
        """;

    /// <summary>PackageTest's MessageProgram in Visual Basic: prints the message of each check that fails.</summary>
    private const string VisualBasicMessageProgram = """
        Imports System
        Imports System.ComponentModel.DataAnnotations
        Imports Net.NowhereAtAll.Stipulate.Contracts

        Public Module Program
            Public Sub Main()
                Attempt(Sub() Target.Check(0))
                Attempt(Sub() Target.Name("abc"))
                Attempt(Sub() Target.Code("abc"))
            End Sub

            Private Sub Attempt(action As Action)
                Try
                    action()
                Catch rejection As ArgumentException
                    Console.WriteLine(rejection.Message)
                End Try
            End Sub
        End Module

        Public Class Target
            Public Shared Sub Check(<Range(1, 10)> value As Integer)
            End Sub

            Public Shared Sub Name(<MinLength(5)> name As String)
            End Sub

            Public Shared Sub Code(<MinLength(5), ShowValueInMessage> code As String)
            End Sub
        End Class
        """;

    /// <summary>
    /// PackageTest's MessageProgram in F#, its types in the global namespace, as the C# program's
    /// are, rather than in the module F# makes of a program's last file.
    /// </summary>
    private const string FSharpMessageProgram = """
        namespace global

        open System
        open System.ComponentModel.DataAnnotations
        open Net.NowhereAtAll.Stipulate.Contracts

        type Target =
            static member Check([<Range(1, 10)>] value: int) = ()
            static member Name([<MinLength(5)>] name: string) = ()
            static member Code([<MinLength(5); ShowValueInMessage>] code: string) = ()

        module Program =
            let attempt (action: unit -> unit) =
                try
                    action ()
                with :? ArgumentException as rejection ->
                    Console.WriteLine(rejection.Message)

            [<EntryPoint>]
            let main _ =
                attempt (fun () -> Target.Check 0)
                attempt (fun () -> Target.Name "abc")
                attempt (fun () -> Target.Code "abc")
                0
        """;

    /// <summary>A C# library with a validated method.</summary>
    private const string CSharpLibrary = """
        using System.ComponentModel.DataAnnotations;

        public static class Levels
        {
            public static void Check([Range(1, 10)] int level) { }
        }
        """;

    /// <summary>Calls <see cref="CSharpLibrary"/>'s validated method, in Visual Basic.</summary>
    private const string VisualBasicProgramOnLibrary = """
        Imports System

        Public Module Program
            Public Sub Main()
                Try
                    Levels.Check(0)
                    Console.WriteLine("not validated")
                Catch rejection As ArgumentException
                    Console.WriteLine("validated: " & rejection.ParamName)
                End Try
            End Sub
        End Module
        """;

    /// <summary>Calls <see cref="CSharpLibrary"/>'s validated method, in F#.</summary>
    private const string FSharpProgramOnLibrary = """
        open System

        try
            Levels.Check(0)
            Console.WriteLine("not validated")
        with :? ArgumentException as rejection ->
            Console.WriteLine("validated: " + rejection.ParamName)
        """;

    /// <summary>
    /// An F# library with methods small enough for F#'s optimizer to inline: one validated, one
    /// validated and marked NoInlining, and one that calls the first from within the library.
    /// </summary>
    private const string FSharpLibrary = """
        namespace Library

        open System.ComponentModel.DataAnnotations
        open System.Runtime.CompilerServices

        type Levels =
            static member Check([<Range(1, 10)>] level: int) = ()

            [<MethodImpl(MethodImplOptions.NoInlining)>]
            static member CheckMarked([<Range(1, 10)>] level: int) = ()

            static member CheckWithin(level: int) = Levels.Check level
        """;

    /// <summary>Calls <see cref="FSharpLibrary"/> each way, saying of each call whether it validated.</summary>
    private const string FSharpProgramOnFSharpLibrary = """
        open System

        let validates (call: unit -> unit) =
            try
                call ()
                "not validated"
            with :? ArgumentException ->
                "validated"

        Console.WriteLine("within: " + validates (fun () -> Library.Levels.CheckWithin 0))
        Console.WriteLine("across: " + validates (fun () -> Library.Levels.Check 0))
        Console.WriteLine("marked: " + validates (fun () -> Library.Levels.CheckMarked 0))
        """;

    private const string LanguageWarning = "ST0008";
    private const string Validated = "validated";
    private const string NotValidated = "not validated";

    /// <summary>F#'s optimizer is on in Release, and it is Release that ships: force it on in Debug too.</summary>
    private const string Optimized = "Optimize=true";

    private readonly PackageFeed _feed = feed;

    [Theory]
    [InlineData(ProjectLanguage.VisualBasic)]
    [InlineData(ProjectLanguage.FSharp)]
    public void Build_WhenTheProjectIsWoven_ThrowsTheSameMessagesAsInCSharp(ProjectLanguage language)
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            MessageProgramIn(language),
            language: language
        );
        ProcessResult build = project.Build().AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.DoesNotContain(LanguageWarning, build.Output, StringComparison.Ordinal);
        Assert.Equal(PackageTest.RejectedMessages, run.Output.Trim().Split(Environment.NewLine));
    }

    [Theory]
    [InlineData(ProjectLanguage.VisualBasic)]
    [InlineData(ProjectLanguage.FSharp)]
    public void Build_WhenTheModeIsInterceptors_BuildsWithoutEnforcingAndSaysSo(ProjectLanguage language)
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            ProgramIn(language),
            language: language
        );
        ProcessResult build = project.Build("StipulateMode=Interceptors").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Contains("warning " + LanguageWarning, build.Output, StringComparison.Ordinal);
        Assert.Equal(NotValidated, run.Output.Trim());
    }

    [Theory]
    [InlineData(ProjectLanguage.VisualBasic)]
    [InlineData(ProjectLanguage.FSharp)]
    public void Build_WhenTheModeIsNone_NeitherEnforcesNorWarns(ProjectLanguage language)
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            ProgramIn(language),
            language: language
        );
        ProcessResult build = project.Build("StipulateMode=None").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.DoesNotContain(LanguageWarning, build.Output, StringComparison.Ordinal);
        Assert.Equal(NotValidated, run.Output.Trim());
    }

    [Theory]
    [InlineData(ProjectLanguage.VisualBasic, LibraryReference.Project)]
    [InlineData(ProjectLanguage.VisualBasic, LibraryReference.Package)]
    [InlineData(ProjectLanguage.FSharp, LibraryReference.Project)]
    [InlineData(ProjectLanguage.FSharp, LibraryReference.Package)]
    public void Build_WhenTheProgramCallsAWovenCSharpLibrary_TheLibraryValidates(
        ProjectLanguage language,
        LibraryReference reference
    )
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateOnLibrary(
            this._feed,
            reference,
            CSharpLibrary,
            ProgramOnLibraryIn(language),
            language: language
        );
        _ = project.Build().AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal("validated: level", run.Output.Trim());
    }

    [Theory]
    [InlineData("EveryCall", Validated, Validated, Validated)]
    [InlineData("CallsFromOtherAssemblies", NotValidated, Validated, Validated)]
    [InlineData("NoInliningMethods", NotValidated, NotValidated, Validated)]
    public void Build_WhenAnFSharpLibraryIsOptimized_ChecksTheCallsStipulateFSharpChecksGuarantees(
        string checks,
        string within,
        string across,
        string marked
    )
    {
        // Arrange - the program doesn't get Stipulate's settings, as a caller without it wouldn't
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateOnLibrary(
            this._feed,
            LibraryReference.Project,
            FSharpLibrary,
            FSharpProgramOnFSharpLibrary,
            language: ProjectLanguage.FSharp,
            libraryLanguage: ProjectLanguage.FSharp
        );
        _ = project.Build(Optimized, "StipulateTransitive=false", $"StipulateFSharpChecks={checks}")
            .AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal(
            [$"within: {within}", $"across: {across}", $"marked: {marked}"],
            run.Output.Trim().Split('\n', StringSplitOptions.TrimEntries)
        );
    }

    [Fact]
    public void Build_WhenStipulateFSharpChecksChanges_RecompilesWithTheNewSetting()
    {
        // Arrange - a call within the program, which F#'s optimizer inlines unless told not to
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            FSharpProgram,
            language: ProjectLanguage.FSharp
        );
        _ = project.Build(Optimized, "StipulateFSharpChecks=NoInliningMethods").AssertSucceeded();
        ProcessResult beforeChange = project.RunProgram().AssertSucceeded();
        _ = project.Build(Optimized, "StipulateFSharpChecks=EveryCall").AssertSucceeded();

        // Act
        ProcessResult run = project.RunProgram().AssertSucceeded();

        // Assert
        Assert.Equal(NotValidated, beforeChange.Output.Trim());
        Assert.Equal("validated: value", run.Output.Trim());
    }

    [Fact]
    public void Build_WhenStipulateFSharpChecksIsNotOneOfItsValues_ReportsST0009()
    {
        // Arrange
        _ = this._feed.PackResult.AssertSucceeded();
        using ConsumerProject project = ConsumerProject.CreateFromPackage(
            this._feed,
            FSharpProgram,
            language: ProjectLanguage.FSharp
        );

        // Act
        ProcessResult build = project.Build("StipulateFSharpChecks=Sometimes");

        // Assert
        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains(
            "error ST0009: StipulateFSharpChecks is 'Sometimes', which isn't one of its values",
            build.Output,
            StringComparison.Ordinal
        );
    }

    private static string ProgramIn(ProjectLanguage language)
        => language == ProjectLanguage.VisualBasic
            ? VisualBasicProgram
            : FSharpProgram;

    private static string MessageProgramIn(ProjectLanguage language)
        => language == ProjectLanguage.VisualBasic
            ? VisualBasicMessageProgram
            : FSharpMessageProgram;

    private static string ProgramOnLibraryIn(ProjectLanguage language)
        => language == ProjectLanguage.VisualBasic
            ? VisualBasicProgramOnLibrary
            : FSharpProgramOnLibrary;
}

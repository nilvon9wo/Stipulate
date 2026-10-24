using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.ArgumentChecks.Test;

/// <summary>Stipulate.ArgumentChecks.vb, which Visual Basic projects compile in.</summary>
public sealed class VisualBasicArgumentChecksTest : ArgumentChecksTest
{
    private const string FileName = "Stipulate.ArgumentChecks.vb";
    private const string ChecksName = "Net.NowhereAtAll.Stipulate.Runtime.ArgumentChecks";

    protected override string AssemblyName => "Net.NowhereAtAll.Stipulate.ArgumentChecks.VisualBasic";

    [Fact]
    public void Compile_AsAConsumerDoes_ReportsNothingAndPutsTheCheckWhereTheWeaverLooks()
    {
        // Arrange - as shipped, without the measuring symbol, under the options most likely to upset it
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string source = ReadSource();
        SyntaxTree tree = VisualBasicSyntaxTree.ParseText(source, path: FileName, cancellationToken: cancellation);
        VisualBasicCompilationOptions options = new(
            OutputKind.DynamicallyLinkedLibrary,
            rootNamespace: "Consumer",
            optionStrict: OptionStrict.Off,
            optionCompareText: true
        );

        // Act
        VisualBasicCompilation compilation = VisualBasicCompilation.Create(
            "Checks",
            [tree],
            TestCompilation.References,
            options
        );

        // Assert
        Assert.Empty(compilation.GetDiagnostics(cancellation).Where(IsReported));
        Assert.NotNull(compilation.GetTypeByMetadataName(ChecksName));
    }

    private static bool IsReported(Diagnostic diagnostic) => diagnostic.Severity >= DiagnosticSeverity.Warning;

    private static string ReadSource()
    {
        using Stream stream = typeof(VisualBasicArgumentChecksTest).Assembly.GetManifestResourceStream(FileName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

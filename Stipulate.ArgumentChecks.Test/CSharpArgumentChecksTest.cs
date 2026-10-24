using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.ArgumentChecks.Test;

/// <summary>Stipulate.ArgumentChecks.cs, which C# projects compile in.</summary>
public sealed class CSharpArgumentChecksTest : ArgumentChecksTest
{
    protected override string AssemblyName => "Net.NowhereAtAll.Stipulate.ArgumentChecks.CSharp";

    [Fact]
    public void Compile_AsCSharp73_ReportsNothing()
    {
        // Arrange - the oldest language a consumer may compile it with, with every warning on
        SyntaxTree tree = TestCompilation.ParseArgumentChecks(new CSharpParseOptions(LanguageVersion.CSharp7_3));
        CSharpCompilationOptions options = new(OutputKind.DynamicallyLinkedLibrary, warningLevel: 9999);

        // Act
        CSharpCompilation compilation = CSharpCompilation.Create("Checks", [tree], TestCompilation.References, options);

        // Assert
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Net.NowhereAtAll.Stipulate.Generator.Test;

/// <summary>
/// The generator runs on every keystroke in the IDE, so a change that leaves the call sites as they
/// were must not regenerate the interceptors.
/// </summary>
public class IncrementalGenerationTest
{
    private const string Source = """
        public static class Target
        {
            public static void Check([Required, MinLength(1)] string? text) { }

            public static void Run() => Check("text");
        }
        """;

    [Fact]
    public void RunGenerators_WhenAnUnrelatedFileChanges_ReusesTheCallSites()
    {
        // Arrange
        CSharpCompilation compilation = GeneratorRunner.Compile(Source);
        GeneratorDriver driver = GeneratorRunner.CreateDriver().RunGenerators(compilation, CancellationToken);
        SyntaxTree unrelated = CSharpSyntaxTree.ParseText(
            "public static class Unrelated { }",
            (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
            cancellationToken: CancellationToken
        );

        // Act
        GeneratorRunResult result = driver
            .RunGenerators(compilation.AddSyntaxTrees(unrelated), CancellationToken)
            .GetRunResult()
            .Results
            .Single();

        // Assert
        IncrementalStepRunReason[] reasons = [.. result.TrackedOutputSteps
            .SelectMany(static step => step.Value)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)];
        Assert.NotEmpty(reasons);
        Assert.All(reasons, static reason => Assert.Equal(IncrementalStepRunReason.Cached, reason));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}

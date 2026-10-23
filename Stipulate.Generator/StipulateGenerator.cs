using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Net.NowhereAtAll.Stipulate.Generator;

/// <summary>
/// Intercepts every call to a method whose parameters are decorated with
/// <c>System.ComponentModel.DataAnnotations.ValidationAttribute</c>s and validates the
/// arguments before forwarding to the original method.
/// </summary>
/// <remarks>
/// A null argument rejected by <c>RequiredAttribute</c> throws <c>ArgumentNullException</c>;
/// any other validation failure throws <c>ArgumentException</c>.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class StipulateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<CallSite> callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                CallSiteReader.Read
            )
            .Where(static site => site is not null)
            .Select(static (site, _) => site!);
        context.RegisterSourceOutput(callSites.Collect(), Emit);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<CallSite> callSites)
    {
        if (!callSites.IsEmpty)
        {
            context.AddSource(InterceptorSourceWriter.HintName, InterceptorSourceWriter.Write(callSites));
        }
    }
}

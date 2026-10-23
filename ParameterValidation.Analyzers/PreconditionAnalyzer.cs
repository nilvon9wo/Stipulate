using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ParameterValidation.Analyzers;

/// <summary>
/// Checks that every implementation of an interface method, property or indexer, and every
/// override, accepts all the arguments and values its contract's <c>ValidationAttribute</c>s accept,
/// hands back only values they promise, and says so explicitly; and that every
/// <c>[ComparableValidation]</c> is valid.
/// </summary>
/// <remarks>
/// Preconditions may be weakened by an implementation but never strengthened, and guarantees
/// strengthened but never weakened (the Liskov substitution principle). Which checks run at run
/// time doesn't matter here: a caller through the interface is held to the interface's attributes,
/// a caller through the class to the class's.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreconditionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Set by Directory.Build.props; see <see cref="SolutionMembership"/>.</summary>
    private const string SolutionRootProperty = "build_property.ParameterValidationSolutionRoot";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Diagnostics.PreconditionStrengthened,
        Diagnostics.PreconditionDropped,
        Diagnostics.GuaranteeWeakened,
        Diagnostics.NotComparableInSolution,
        Diagnostics.NotComparableExternal,
        Diagnostics.InvalidDescription,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartCompilation);
    }

    private static void StartCompilation(CompilationStartAnalysisContext start)
    {
        _ = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
            SolutionRootProperty,
            out string? solutionRoot
        );
        ValidationAttributeDescriber describer = new(start.Compilation, solutionRoot);
        start.RegisterSymbolAction(
            context => AnalyzeMember(context, describer),
            SymbolKind.Method,
            SymbolKind.Property
        );
        start.RegisterSymbolAction(context => AnalyzeType(context, describer), SymbolKind.NamedType);
        start.RegisterSyntaxNodeAction(DescriptionAnalyzer.Analyze, SyntaxKind.Attribute);
    }

    private static void AnalyzeMember(SymbolAnalysisContext context, ValidationAttributeDescriber describer)
    {
        IEnumerable<ISymbol> contracts = ContractFinder.IsChecked(context.Symbol)
            ? ContractFinder.FindContracts(context.Symbol)
            : [];
        IEnumerable<ComparedTarget> targets = contracts
            .SelectMany(contract => ComparedTargetFactory.Create(contract, context.Symbol, inheritingType: null));
        Check(context, describer, targets);
    }

    private static void AnalyzeType(SymbolAnalysisContext context, ValidationAttributeDescriber describer)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        IEnumerable<ComparedTarget> targets = ContractFinder.FindInheritedImplementations(type)
            .SelectMany(pair => ComparedTargetFactory.Create(pair.Contract, pair.Implementation!, type));
        Check(context, describer, targets);
    }

    private static void Check(
        SymbolAnalysisContext context,
        ValidationAttributeDescriber describer,
        IEnumerable<ComparedTarget> targets
    )
    {
        foreach (Diagnostic diagnostic in targets.SelectMany(target => Compare(target, describer, context)))
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IEnumerable<Diagnostic> Compare(
        ComparedTarget target,
        ValidationAttributeDescriber describer,
        SymbolAnalysisContext context
    )
    {
        ImmutableArray<DescribedAttribute> contractAttributes =
            describer.GetValidationAttributes(target.ContractTarget, context.CancellationToken);
        ImmutableArray<DescribedAttribute> implementationAttributes =
            describer.GetValidationAttributes(target.Target, context.CancellationToken);
        ProblemReporter reporter = new(target, describer, context.CancellationToken);
        return ProblemFinder.Find(target, contractAttributes, implementationAttributes).Select(reporter.Report);
    }
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// Checks every value passed to a validated parameter that is known at compile time (a constant,
/// <c>null</c>, an array or collection written where it is passed, or a <c>static readonly</c> field set
/// to a constant or an array) against the parameter's <c>ValidationAttribute</c>s, and so does
/// every parameter's default value.
/// </summary>
/// <remarks>
/// Only a value an attribute certainly rejects is reported, and only by the attributes the analyzer
/// can describe: an attribute it can't, or a value it can't tell, is passed over. Which checks run
/// at run time doesn't matter here: the call breaks the parameter's contract either way. An
/// argument left out for its default is checked at the default instead.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Set by Directory.Build.props; see <see cref="SolutionMembership"/>.</summary>
    private const string SolutionRootProperty = "build_property.StipulateSolutionRoot";

    private const string ArgumentNoun = "The argument";
    private const string DefaultNoun = "The default value";
    private const string ThisCall = "this call";
    private const string CallsOmittingIt = "every call that leaves the argument out";
    private const string ArgumentException = "ArgumentException";
    private const string ArgumentNullException = "ArgumentNullException";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.ArgumentRejected];

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
        ArgumentValueReader reader = new(new StaticReadOnlyFields(start.Compilation));
        start.RegisterOperationAction(
            context => AnalyzeCall(context, describer, reader),
            OperationKind.Invocation,
            OperationKind.ObjectCreation
        );
        start.RegisterOperationAction(
            context => AnalyzeDefault(context, describer, reader),
            OperationKind.ParameterInitializer
        );
    }

    private static void AnalyzeCall(
        OperationAnalysisContext context,
        ValidationAttributeDescriber describer,
        ArgumentValueReader reader
    )
    {
        IInvocationOperation? invocation = context.Operation as IInvocationOperation;
        ImmutableArray<IArgumentOperation> arguments = invocation?.Arguments
            ?? ((IObjectCreationOperation)context.Operation).Arguments;
        ConcreteReceiver? receiver = invocation is null
            ? null
            : ConcreteReceiver.Find(invocation, context.CancellationToken);
        foreach (IArgumentOperation argument in arguments)
        {
            ArgumentValue? value = argument.ArgumentKind switch
            {
                ArgumentKind.Explicit => reader.Read(argument.Value, ArgumentNoun, context.CancellationToken),
                ArgumentKind.ParamArray or ArgumentKind.ParamCollection =>
                    ArgumentValueReader.ReadParams(argument.Value),
                _ => null,
            };
            Location location = argument.Value.Syntax.GetLocation();
            Check(context, describer, argument.Parameter, value, location, new Consequence(ThisCall, receiver));
        }
    }

    private static void AnalyzeDefault(
        OperationAnalysisContext context,
        ValidationAttributeDescriber describer,
        ArgumentValueReader reader
    )
    {
        IParameterInitializerOperation initializer = (IParameterInitializerOperation)context.Operation;
        ArgumentValue? value = reader.Read(initializer.Value, DefaultNoun, context.CancellationToken);
        Location location = initializer.Value.Syntax.GetLocation();
        Check(context, describer, initializer.Parameter, value, location, new Consequence(CallsOmittingIt, null));
    }

    /// <summary>Reports each of the parameter's attributes that rejects the value.</summary>
    /// <param name="context">Where to report.</param>
    /// <param name="describer">Works out what the parameter's attributes constrain.</param>
    /// <param name="parameter">
    /// The parameter, or <c>null</c> for an <c>__arglist</c>'s argument, which has none.
    /// </param>
    /// <param name="value">What is known of the value, or <c>null</c> if nothing is.</param>
    /// <param name="location">Where the value is written; the call, for <c>params</c> arguments.</param>
    /// <param name="consequence">Which calls throw because of the value, for the message.</param>
    private static void Check(
        OperationAnalysisContext context,
        ValidationAttributeDescriber describer,
        IParameterSymbol? parameter,
        ArgumentValue? value,
        Location location,
        Consequence consequence
    )
    {
        if (parameter is null || value is null)
        {
            return;
        }

        IEnumerable<Diagnostic> diagnostics = FindRejections(describer, parameter, value, context.CancellationToken)
            .Select(rejection => Diagnostic.Create(
                Diagnostics.ArgumentRejected,
                location,
                value.Description,
                parameter.Name,
                ComparedTargetFactory.Display(parameter.ContainingSymbol),
                rejection.Attribute.Display,
                rejection.Reason,
                Describe(describer, parameter, value, rejection.Attribute, consequence, context.CancellationToken)
            ));
        foreach (Diagnostic diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// What happens at run time, and how to fix it. A call on an object whose type the analyzer
    /// knows, and whose method accepts the value, throws only with interceptors: they check the
    /// method called, the weaver the method that runs.
    /// </summary>
    private static string Describe(
        ValidationAttributeDescriber describer,
        IParameterSymbol parameter,
        ArgumentValue value,
        DescribedAttribute attribute,
        Consequence consequence,
        CancellationToken cancellationToken
    )
    {
        string exception = ExceptionFor(attribute, value);
        ConcreteReceiver? accepting = consequence.Receiver is { } receiver
            && Accepts(describer, receiver.Method.Parameters[parameter.Ordinal], value, cancellationToken)
            ? receiver
            : null;
        return accepting is null
            ? $"Wherever Stipulate enforces the attribute at run time, {consequence.ThrowingCalls} throws {exception}. "
                + $"Pass a value [{attribute.Display}] accepts, or, if the value is right, correct the attribute."
            : DescribeAccepting(describer, parameter, accepting, attribute, exception, cancellationToken);
    }

    private static string DescribeAccepting(
        ValidationAttributeDescriber describer,
        IParameterSymbol parameter,
        ConcreteReceiver receiver,
        DescribedAttribute attribute,
        string exception,
        CancellationToken cancellationToken
    )
    {
        string type = ComparedTargetFactory.Display(receiver.Type);
        string method = ComparedTargetFactory.Display(receiver.Method);
        ImmutableArray<DescribedAttribute> own = describer.GetValidationAttributes(
            receiver.Method.Parameters[parameter.Ordinal].OriginalDefinition,
            cancellationToken
        );
        string attributes = own.IsEmpty
            ? "it has no validation attributes"
            : string.Join(", ", own.Select(static described => $"[{described.Display}]"));
        return $"The object here is a '{type}', whose '{method}' accepts it ({attributes}): woven, this call runs "
            + $"that method's own check and doesn't throw; with interceptors, it checks "
            + $"'{ComparedTargetFactory.Display(parameter.ContainingSymbol)}' and throws {exception}. Declare the "
            + $"variable as '{type}' to rely on what '{method}' accepts, or pass a value [{attribute.Display}] "
            + "accepts.";
    }

    /// <summary>
    /// Whether the parameter's attributes accept the value: all of them can be described, and none
    /// rejects it.
    /// </summary>
    private static bool Accepts(
        ValidationAttributeDescriber describer,
        IParameterSymbol parameter,
        ArgumentValue value,
        CancellationToken cancellationToken
    )
        => describer.GetValidationAttributes(parameter.OriginalDefinition, cancellationToken)
            .All(attribute => attribute.Constraints is not null && FindRejection(attribute, value) is null);

    /// <summary>
    /// What the enforced check throws, as the weaver and the interceptors do: a <c>null</c> rejected
    /// by <c>[Required]</c>, or an attribute derived from it, throws <c>ArgumentNullException</c>.
    /// </summary>
    private static string ExceptionFor(DescribedAttribute attribute, ArgumentValue value)
        => value.IsNull && IsRequired(attribute.AttributeClass)
            ? ArgumentNullException
            : ArgumentException;

    private static bool IsRequired(INamedTypeSymbol attributeClass)
        => TypeHierarchy.SelfAndBaseTypes(attributeClass)
            .Any(static type => type.ToDisplayString() == ValidationAttributeTypes.RequiredAttributeName);

    /// <summary>Each attribute that rejects the value, with the reason its first rejecting constraint gives.</summary>
    private static IEnumerable<(DescribedAttribute Attribute, string Reason)> FindRejections(
        ValidationAttributeDescriber describer,
        IParameterSymbol parameter,
        ArgumentValue value,
        CancellationToken cancellationToken
    )
        => describer.GetValidationAttributes(parameter.OriginalDefinition, cancellationToken)
            .Select(attribute => (attribute, Reason: FindRejection(attribute, value)))
            .Where(static rejection => rejection.Reason is not null)
            .Select(static rejection => (rejection.attribute, rejection.Reason!));

    private static string? FindRejection(DescribedAttribute attribute, ArgumentValue value)
        => (attribute.Constraints ?? [])
            .Select(constraint => constraint.FindRejection(value))
            .FirstOrDefault(static reason => reason is not null);
}


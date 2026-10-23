using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ParameterValidation.Analyzers;

/// <summary>Turns the problems with one compared target into diagnostics.</summary>
internal sealed class ProblemReporter(
    ComparedTarget target,
    ValidationAttributeDescriber describer,
    CancellationToken cancellationToken
)
{
    private readonly ComparedTarget _target = target;
    private readonly ValidationAttributeDescriber _describer = describer;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public Diagnostic Report(Problem problem)
    {
        Location location = this.Locate(problem);
        return problem.Kind switch
        {
            ProblemKind.Strengthened => this.Create(Diagnostics.PreconditionStrengthened, location, problem.Detail),
            ProblemKind.Weakened => this.Create(Diagnostics.GuaranteeWeakened, location, problem.Detail),
            ProblemKind.Dropped => this.ReportDropped(problem, location),
            _ => this.ReportNotComparable(problem, location),
        };
    }

    /// <summary>
    /// A class that inherits its implementation is reported on itself. Otherwise the implementation's
    /// own attributes are reported where they're applied, and the contract's (every guarantee
    /// problem's, and a dropped precondition's) on the parameter or property.
    /// </summary>
    private Location Locate(Problem problem)
        => this._target.InheritingType is { } inheritingType
            ? inheritingType.Locations[0]
            : this.LocateOnImplementation(problem);

    /// <remarks>
    /// The implementation is being analyzed, so it is declared in source, and so are its attributes:
    /// both have a location. (An inherited implementation may come from a package; that is why a
    /// class that inherits one is reported on itself.)
    /// </remarks>
    private Location LocateOnImplementation(Problem problem)
    {
        bool isOnImplementationAttribute = !problem.IsGuarantee && problem.Kind != ProblemKind.Dropped;
        return isOnImplementationAttribute
            ? problem.Attribute.Data.ApplicationSyntaxReference!.GetSyntax(this._cancellationToken).GetLocation()
            : this._target.Target.Locations[0];
    }

    /// <summary>The code fix can restate the attribute, unless the implementation is inherited.</summary>
    private Diagnostic ReportDropped(Problem problem, Location location)
    {
        DiagnosticDescriptor descriptor = problem.IsGuarantee
            ? Diagnostics.GuaranteeWeakened
            : Diagnostics.PreconditionDropped;
        string detail = problem.IsGuarantee
            ? $"it doesn't restate [{problem.Attribute.Display}]"
            : problem.Attribute.Display;
        ImmutableDictionary<string, string?>? properties = this._target.InheritingType is null
            ? RestatePropertiesFactory.Create(this._target, problem.Attribute)
            : null;
        return Diagnostic.Create(
            descriptor,
            location,
            properties,
            this._target.Subject,
            this._target.ContractName,
            detail
        );
    }

    private Diagnostic ReportNotComparable(Problem problem, Location location)
    {
        INamedTypeSymbol attributeClass = problem.Attribute.AttributeClass;
        string className = attributeClass.ToDisplayString();
        return this._describer.IsInSolution(attributeClass)
            ? this.Create(Diagnostics.NotComparableInSolution, location, problem.Attribute.Display, className)
            : this.Create(
                Diagnostics.NotComparableExternal,
                location,
                problem.Attribute.Display,
                className,
                attributeClass.ContainingAssembly.Name
            );
    }

    private Diagnostic Create(DiagnosticDescriptor descriptor, Location location, params object[] details)
        => Diagnostic.Create(descriptor, location, [this._target.Subject, this._target.ContractName, .. details]);
}

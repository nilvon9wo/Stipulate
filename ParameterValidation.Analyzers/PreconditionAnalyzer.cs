using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ParameterValidation.Analyzers;

/// <summary>
/// Checks that every implementation of an interface method, property or indexer, and every
/// override, accepts all the arguments and values its contract's <c>ValidationAttribute</c>s accept,
/// and says so explicitly.
/// </summary>
/// <remarks>
/// Preconditions may be weakened by an implementation but never strengthened (the Liskov
/// substitution principle). Which checks run at run time doesn't matter here: a caller through the
/// interface is held to the interface's attributes, a caller through the class to the class's.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreconditionAnalyzer : DiagnosticAnalyzer
{
	/// <summary>Set by Directory.Build.props; see <see cref="ValidationAttributeDescriber.IsInSolution"/>.</summary>
	private const string SolutionRootProperty = "build_property.ParameterValidationSolutionRoot";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[
		Diagnostics.Strengthened,
		Diagnostics.NotComparableInSolution,
		Diagnostics.Dropped,
		Diagnostics.NotComparableExternal,
		Diagnostics.InvalidDescription,
	];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			_ = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(SolutionRootProperty, out string? solutionRoot);
			ValidationAttributeDescriber describer = new(start.Compilation, solutionRoot);
			start.RegisterSymbolAction(context => AnalyzeMember(context, describer), SymbolKind.Method, SymbolKind.Property);
			start.RegisterSymbolAction(context => AnalyzeType(context, describer), SymbolKind.NamedType);
			start.RegisterSyntaxNodeAction(AnalyzeDescription, SyntaxKind.Attribute);
		});
	}

	private static void AnalyzeMember(SymbolAnalysisContext context, ValidationAttributeDescriber describer)
	{
		if (!HasPreconditions(context.Symbol))
		{
			return;
		}

		foreach (ISymbol contract in GetContracts(context.Symbol))
		{
			Check(context, describer, contract, context.Symbol, inheritingType: null);
		}
	}

	/// <summary>
	/// Whether <paramref name="member"/> takes anything validation attributes could constrain:
	/// parameters, or for a property, the value it's set to. Accessors are checked as part of their
	/// property.
	/// </summary>
	private static bool HasPreconditions(ISymbol member) => member switch
	{
		IMethodSymbol method => !method.Parameters.IsEmpty && method.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation,
		IPropertySymbol property => !property.Parameters.IsEmpty || property.SetMethod is not null,
		_ => false,
	};

	/// <summary>The members whose preconditions <paramref name="member"/> must honour.</summary>
	private static HashSet<ISymbol> GetContracts(ISymbol member)
	{
		HashSet<ISymbol> contracts = new(SymbolEqualityComparer.Default);
		ISymbol? overridden = member switch
		{
			IMethodSymbol method => method.OverriddenMethod,
			IPropertySymbol property => property.OverriddenProperty,
			_ => null,
		};
		if (overridden is not null)
		{
			_ = contracts.Add(overridden);
		}

		ImmutableArray<ISymbol> explicitImplementations = member switch
		{
			IMethodSymbol method => ImmutableArray<ISymbol>.CastUp(method.ExplicitInterfaceImplementations),
			IPropertySymbol property => ImmutableArray<ISymbol>.CastUp(property.ExplicitInterfaceImplementations),
			_ => [],
		};
		contracts.UnionWith(explicitImplementations);

		INamedTypeSymbol type = member.ContainingType;
		if (explicitImplementations.IsEmpty && type.TypeKind is TypeKind.Class or TypeKind.Struct)
		{
			foreach (INamedTypeSymbol @interface in type.AllInterfaces)
			{
				foreach (ISymbol candidate in @interface.GetMembers(member.Name).Where(candidate => candidate.Kind == member.Kind))
				{
					// An override is checked against the member it overrides, which in turn was
					// checked against the interface; checking both would report everything twice.
					if (member.IsOverride && type.BaseType?.FindImplementationForInterfaceMember(candidate) is not null)
					{
						continue;
					}

					if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(candidate), member))
					{
						_ = contracts.Add(candidate);
					}
				}
			}
		}

		return contracts;
	}

	/// <summary>
	/// Checks interface members that a class implements with a member it inherits from a base class
	/// that doesn't itself implement the interface. Nothing else would: the base class's member
	/// knows nothing of the interface.
	/// </summary>
	private static void AnalyzeType(SymbolAnalysisContext context, ValidationAttributeDescriber describer)
	{
		INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
		if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
		{
			return;
		}

		foreach (INamedTypeSymbol @interface in type.AllInterfaces)
		{
			foreach (ISymbol member in @interface.GetMembers().Where(HasPreconditions))
			{
				if (type.FindImplementationForInterfaceMember(member) is not { } implementation
					|| SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type)
					|| implementation.ContainingType.TypeKind == TypeKind.Interface
					|| type.BaseType?.FindImplementationForInterfaceMember(member) is not null)
				{
					continue;
				}

				Check(context, describer, member, implementation, type);
			}
		}
	}

	/// <param name="inheritingType">
	/// The class that uses <paramref name="implementation"/>, inherited from its base class, to
	/// implement <paramref name="contract"/>; <c>null</c> if <paramref name="implementation"/> is
	/// being analyzed itself.
	/// </param>
	private static void Check(
		SymbolAnalysisContext context,
		ValidationAttributeDescriber describer,
		ISymbol contract,
		ISymbol implementation,
		INamedTypeSymbol? inheritingType)
	{
		string contractName = contract.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
		string implementationName = implementation.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
		string inherited = inheritingType is null
			? ""
			: $" (inherited by '{inheritingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}')";

		ImmutableArray<IParameterSymbol> contractParameters = GetParameters(contract);
		ImmutableArray<IParameterSymbol> implementationParameters = GetParameters(implementation);
		for (int i = 0; i < Math.Min(contractParameters.Length, implementationParameters.Length); i++)
		{
			IParameterSymbol parameter = implementationParameters[i];
			Compare(
				context, describer, contractParameters[i], parameter, parameter.Type, parameter.NullableAnnotation,
				$"Parameter '{parameter.Name}' of '{implementationName}'{inherited}", contractName, inheritingType);
		}

		// A property's attributes limit what it may be set to, so they're preconditions of its
		// setter. A get-only contract's attributes describe what it returns instead, which an
		// implementation may narrow, so they're not compared.
		if (contract is IPropertySymbol { SetMethod: not null } && implementation is IPropertySymbol { SetMethod: not null } property)
		{
			Compare(
				context, describer, contract, property, property.Type, property.NullableAnnotation,
				$"{(property.IsIndexer ? "Indexer" : "Property")} '{implementationName}'{inherited}", contractName, inheritingType);
		}
	}

	private static ImmutableArray<IParameterSymbol> GetParameters(ISymbol member) => member switch
	{
		IMethodSymbol method => method.Parameters,
		IPropertySymbol property => property.Parameters,
		_ => [],
	};

	/// <summary>Compares the validation attributes on one parameter or property with its contract's.</summary>
	/// <param name="subject">The implementation's parameter or property, as the messages name it.</param>
	private static void Compare(
		SymbolAnalysisContext context,
		ValidationAttributeDescriber describer,
		ISymbol contractTarget,
		ISymbol target,
		ITypeSymbol type,
		NullableAnnotation nullability,
		string subject,
		string contractName,
		INamedTypeSymbol? inheritingType)
	{
		ImmutableArray<DescribedAttribute> contractAttributes = describer.GetValidationAttributes(contractTarget, context.CancellationToken);
		ImmutableArray<DescribedAttribute> implementationAttributes = describer.GetValidationAttributes(target, context.CancellationToken);
		if (contractAttributes.IsEmpty && implementationAttributes.IsEmpty)
		{
			return;
		}

		bool acceptsNull = nullability == NullableAnnotation.Annotated
			|| type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

		foreach (Problem problem in PreconditionComparer.Compare(contractAttributes, implementationAttributes, acceptsNull))
		{
			// A dropped attribute is the contract's, so it's reported on the parameter or property that lacks it.
			Location location = inheritingType?.Locations.FirstOrDefault()
				?? (problem.Kind == ProblemKind.Dropped ? null : problem.Attribute.Data.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation())
				?? target.Locations.FirstOrDefault()
				?? Location.None;
			INamedTypeSymbol attributeClass = problem.Attribute.Data.AttributeClass!;
			context.ReportDiagnostic(problem.Kind switch
			{
				ProblemKind.Strengthened => Diagnostic.Create(Diagnostics.Strengthened, location, subject, contractName, problem.Detail),
				ProblemKind.Dropped => Diagnostic.Create(Diagnostics.Dropped, location, subject, contractName, problem.Attribute.Display),
				_ when describer.IsInSolution(attributeClass) => Diagnostic.Create(
					Diagnostics.NotComparableInSolution, location, subject, contractName, problem.Attribute.Display, attributeClass.ToDisplayString()),
				_ => Diagnostic.Create(
					Diagnostics.NotComparableExternal, location, subject, contractName, problem.Attribute.Display, attributeClass.ToDisplayString(), attributeClass.ContainingAssembly.Name),
			});
		}
	}

	/// <summary>Reports a <c>[ComparableValidation]</c> that the describer would have to ignore.</summary>
	private static void AnalyzeDescription(SyntaxNodeAnalysisContext context)
	{
		AttributeSyntax syntax = (AttributeSyntax)context.Node;
		if (context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol is not IMethodSymbol constructor
			|| constructor.ContainingType.ToDisplayString() != ValidationAttributeDescriber.MarkerName
			|| syntax.Parent is not AttributeListSyntax list)
		{
			return;
		}

		bool onAssembly = list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true;
		ISymbol? owner = onAssembly
			? context.Compilation.Assembly
			: list.Parent is TypeDeclarationSyntax declaration ? context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) : null;
		AttributeData? marker = owner?.GetAttributes().FirstOrDefault(attribute =>
			attribute.ApplicationSyntaxReference is { } reference && reference.SyntaxTree == syntax.SyntaxTree && reference.Span == syntax.Span);
		if (marker is null)
		{
			return;
		}

		ITypeSymbol? target = null;
		string? error;
		if (onAssembly)
		{
			target = marker.ConstructorArguments.Length == 3 ? marker.ConstructorArguments[0].Value as ITypeSymbol : null;
			error = target is null ? "on an assembly it needs the type of the attribute it describes" : null;
		}
		else
		{
			target = owner as ITypeSymbol;
			error = marker.ConstructorArguments.Length == 3
				? "on a class it describes that class, so it takes no attribute type (that form is for [assembly: ComparableValidation(typeof(...), ...)])"
				: null;
		}

		if (error is null && target is not null)
		{
			_ = ValidationAttributeDescriber.TryReadDescriptor(marker, target, out _, out error);
		}

		if (error is not null)
		{
			context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidDescription, syntax.GetLocation(), error));
		}
	}
}

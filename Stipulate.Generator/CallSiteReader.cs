using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Net.NowhereAtAll.Stipulate.Generator;

/// <summary>Reads the call site of an invocation of a method whose parameters have validation attributes.</summary>
internal static class CallSiteReader
{
    private const string ThisParameter = "@this";
    private const string ArgumentSeparator = ", ";

    private const string ShowValueInMessageName = "Net.NowhereAtAll.Stipulate.Contracts.ShowValueInMessageAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// The method as the messages name it, as the weaver does from the metadata, which has no
    /// nullable reference annotations: <c>Scale.Set(int)</c>, <c>Greeter.Greet(string)</c>.
    /// </summary>
    private static readonly SymbolDisplayFormat MethodFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    /// <returns>The call site, or <c>null</c> if the call needn't or can't be intercepted.</returns>
    public static CallSite? Read(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol;
        return symbol is IMethodSymbol method && IsInterceptable(method, context.SemanticModel.Compilation)
            ? Read(context.SemanticModel, invocation, method, cancellationToken)
            : null;
    }

    /// <summary>
    /// Only a plain method the interceptor can call: not generic or an extension (whose signatures
    /// the interceptor would have to reproduce), with no by-reference parameters or return.
    /// </summary>
    private static bool IsInterceptable(IMethodSymbol method, Compilation compilation)
        => IsPlain(method)
            && method.Parameters.All(static parameter => parameter.RefKind == RefKind.None)
            && compilation.IsSymbolAccessibleWithin(method, compilation.Assembly);

    private static bool IsPlain(IMethodSymbol method) => method is
    {
        MethodKind: MethodKind.Ordinary,
        IsGenericMethod: false,
        IsExtensionMethod: false,
        ContainingType.IsGenericType: false,
        ReturnsByRef: false,
        ReturnsByRefReadonly: false,
    };

    private static CallSite? Read(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<ParameterCheck> checks = [.. method.Parameters.Select(ReadCheck).OfType<ParameterCheck>()];
        InterceptableLocation? location = checks.IsEmpty
            ? null
            : model.GetInterceptableLocation(invocation, cancellationToken);
        return location is null
            ? null
            : new CallSite(
                // Only local functions and anonymous or erroneous methods lack an ID; none is interceptable.
                method.GetDocumentationCommentId()!,
                method.ToDisplayString(MethodFormat),
                BuildSignature(method),
                BuildForwardingCall(method),
                method.ReturnsVoid,
                new EquatableArray<ParameterCheck>(checks),
                location.GetInterceptsLocationAttributeSyntax()
            );
    }

    private static ParameterCheck? ReadCheck(IParameterSymbol parameter)
    {
        ImmutableArray<string> constructions = [.. parameter.GetAttributes()
            .Where(static attribute => ValidationAttributeTypes.IsValidationAttribute(attribute.AttributeClass))
            .Select(BuildConstruction)];
        return constructions.IsEmpty
            ? null
            : new ParameterCheck(parameter.Name, new EquatableArray<string>(constructions), ShowsValue(parameter));
    }

    private static bool ShowsValue(IParameterSymbol parameter)
        => parameter.GetAttributes()
            .Any(static attribute => attribute.AttributeClass?.ToDisplayString() == ShowValueInMessageName);

    private static string BuildConstruction(AttributeData attribute)
    {
        string arguments = string.Join(
            ArgumentSeparator,
            attribute.ConstructorArguments.Select(ConstantWriter.Write)
        );
        string type = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string construction = $"new {type}({arguments})";
        return attribute.NamedArguments.IsEmpty
            ? construction
            : $"{construction} {{ {BuildInitializers(attribute)} }}";
    }

    private static string BuildInitializers(AttributeData attribute)
        => string.Join(
            ArgumentSeparator,
            attribute.NamedArguments.Select(static named => $"{named.Key} = {ConstantWriter.Write(named.Value)}")
        );

    private static string BuildSignature(IMethodSymbol method)
    {
        IEnumerable<string> receiver = method.IsStatic
            ? []
            : [$"this {method.ContainingType.ToDisplayString(TypeFormat)} {ThisParameter}"];
        IEnumerable<string> parameters = method.Parameters
            .Select(static parameter => $"{parameter.Type.ToDisplayString(TypeFormat)} @{parameter.Name}");
        string returnType = method.ReturnsVoid
            ? "void"
            : method.ReturnType.ToDisplayString(TypeFormat);
        return $"{returnType} {{0}}({string.Join(ArgumentSeparator, receiver.Concat(parameters))})";
    }

    private static string BuildForwardingCall(IMethodSymbol method)
    {
        string target = method.IsStatic
            ? method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : ThisParameter;
        IEnumerable<string> names = method.Parameters.Select(static parameter => "@" + parameter.Name);
        string arguments = string.Join(ArgumentSeparator, names);
        return $"{target}.@{method.Name}({arguments})";
    }
}

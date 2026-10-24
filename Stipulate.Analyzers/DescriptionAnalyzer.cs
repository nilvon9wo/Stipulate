using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>Reports a <c>[ComparableValidation]</c> that the describer would have to ignore.</summary>
internal static class DescriptionAnalyzer
{
    public static void Analyze(SyntaxNodeAnalysisContext context)
    {
        AttributeSyntax syntax = (AttributeSyntax)context.Node;
        string? error = FindError(context, syntax);
        if (error is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidDescription, syntax.GetLocation(), error));
        }
    }

    private static string? FindError(SyntaxNodeAnalysisContext context, AttributeSyntax syntax)
    {
        ISymbol? constructor = context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol;
        bool isMarker = constructor?.ContainingType.ToDisplayString() == ComparableValidationMarker.Name;
        return isMarker
            ? FindError(context, syntax, (AttributeListSyntax)syntax.Parent!)
            : null;
    }

    private static string? FindError(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax syntax,
        AttributeListSyntax list
    )
    {
        bool isOnAssembly = list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true;
        ISymbol? owner = isOnAssembly
            ? context.Compilation.Assembly
            : FindDeclaredType(context, list);
        AttributeData? marker = owner?.GetAttributes().FirstOrDefault(attribute => IsAppliedAt(attribute, syntax));
        return marker is null
            ? null
            : new MarkerUsage(marker, owner!, isOnAssembly).FindError();
    }

    private static ISymbol? FindDeclaredType(SyntaxNodeAnalysisContext context, AttributeListSyntax list)
        => list.Parent is TypeDeclarationSyntax declaration
            ? context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            : null;

    /// <remarks>
    /// The owner is declared in source, so its attributes have syntax. (Only an assembly attribute
    /// from a linked .netmodule wouldn't, which SDK-style projects don't support.)
    /// </remarks>
    private static bool IsAppliedAt(AttributeData attribute, AttributeSyntax syntax)
    {
        SyntaxReference reference = attribute.ApplicationSyntaxReference!;
        return reference.SyntaxTree == syntax.SyntaxTree && reference.Span == syntax.Span;
    }
}

/// <summary>A <c>[ComparableValidation]</c> where it is applied.</summary>
/// <param name="Owner">The assembly, or the attribute class the marker is applied to.</param>
file sealed record MarkerUsage(AttributeData Marker, ISymbol Owner, bool IsOnAssembly)
{
    private const string AssemblyFormNeedsType = "on an assembly it needs the type of the attribute it describes";
    private const string ClassFormTakesNoType =
        "on a class it describes that class, so it takes no attribute type (that form is for "
        + "[assembly: ComparableValidation(typeof(...), ...)])";

    /// <returns>Why the marker is invalid, or <c>null</c> if it is valid.</returns>
    public string? FindError()
        => this.IsOnAssembly
            ? this.FindAssemblyFormError()
            : this.FindClassFormError();

    private string? FindAssemblyFormError()
        => ComparableValidationMarker.FindDescribedType(this.Marker) is { } described
            ? DescriptorReader.Read(this.Marker, described).Error
            : AssemblyFormNeedsType;

    private string? FindClassFormError()
        => ComparableValidationMarker.IsAssemblyForm(this.Marker)
            ? ClassFormTakesNoType
            : DescriptorReader.Read(this.Marker, (ITypeSymbol)this.Owner).Error;
}

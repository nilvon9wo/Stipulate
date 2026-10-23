using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// An attribute usage as Roslyn may describe one it couldn't bind: with no class or constructor.
/// Well-formed source never produces one, so only a stand-in can exercise the guards against it.
/// </summary>
internal sealed class FakeAttributeData : AttributeData
{
    protected override INamedTypeSymbol? CommonAttributeClass => null;

    protected override IMethodSymbol? CommonAttributeConstructor => null;

    protected override SyntaxReference? CommonApplicationSyntaxReference => null;

    protected override ImmutableArray<TypedConstant> CommonConstructorArguments => [];

    protected override ImmutableArray<KeyValuePair<string, TypedConstant>> CommonNamedArguments => [];
}

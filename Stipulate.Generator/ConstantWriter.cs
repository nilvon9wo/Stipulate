using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Net.NowhereAtAll.Stipulate.Generator;

/// <summary>Writes an attribute argument as a C# expression that evaluates to the same value.</summary>
/// <remarks>
/// <see cref="TypedConstantExtensions.ToCSharpString(TypedConstant)"/> is written for attributes, not
/// expressions: it writes an array as <c>{1, 2}</c>, a <c>float</c> without its suffix (so it reads
/// as a <c>double</c>), and infinities and NaN as no valid C#. An interceptor constructs the
/// attribute itself, so it needs <c>new int[] { 1, 2 }</c>, <c>1.5F</c> and <c>double.PositiveInfinity</c>.
/// </remarks>
internal static class ConstantWriter
{
    private const string Separator = ", ";
    private const string RoundTrip = "R";

    private static readonly ImmutableDictionary<double, string> SpecialReals = new Dictionary<double, string>
    {
        [double.NaN] = nameof(double.NaN),
        [double.PositiveInfinity] = nameof(double.PositiveInfinity),
        [double.NegativeInfinity] = nameof(double.NegativeInfinity),
    }.ToImmutableDictionary();

    public static string Write(TypedConstant constant) => constant switch
    {
        { Kind: TypedConstantKind.Array, IsNull: false } => WriteArray(constant),
        { Kind: TypedConstantKind.Primitive, Value: float value } => WriteFloat(value),
        { Kind: TypedConstantKind.Primitive, Value: double value } => WriteDouble(value),
        _ => constant.ToCSharpString(),
    };

    private static string WriteArray(TypedConstant array)
    {
        string type = array.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string elements = string.Join(Separator, array.Values.Select(Write));
        return $"new {type} {{ {elements} }}";
    }

    private static string WriteFloat(float value)
        => WriteReal(value, "float", value.ToString(RoundTrip, CultureInfo.InvariantCulture) + "F");

    private static string WriteDouble(double value)
        => WriteReal(value, "double", value.ToString(RoundTrip, CultureInfo.InvariantCulture) + "D");

    private static string WriteReal(double value, string keyword, string literal)
        => SpecialReals.TryGetValue(value, out string? name)
            ? $"{keyword}.{name}"
            : literal;
}

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>Converts attribute arguments into the bounds and value lists constraints hold.</summary>
internal static class ConstantConversion
{
    /// <returns>The constant as a <see cref="double"/>, or <c>null</c> if it isn't a number.</returns>
    public static double? ToBound(TypedConstant constant)
        => AttributeArguments.ScalarValue(constant) is IConvertible number and not (bool or char or string)
            ? Convert.ToDouble(number, CultureInfo.InvariantCulture)
            : null;

    /// <returns>The array's distinct elements as C# literals, or <c>null</c> if it isn't an array.</returns>
    public static ImmutableArray<string>? ToValues(TypedConstant constant)
        => constant is { Kind: TypedConstantKind.Array, IsNull: false }
            ? [.. constant.Values.Select(static value => value.ToCSharpString()).Distinct()]
            : null;

    /// <returns>
    /// A boxed <see cref="double"/> for a bound or an <see cref="ImmutableArray{T}"/> of strings for a
    /// list of values, or <c>null</c> if the constant isn't of the kind's type.
    /// </returns>
    public static object? ToValue(TypedConstant constant, ConstraintKind kind)
        => ConstraintComparisons.Of(kind).IsBound
            ? ToBound(constant)
            : ToValues(constant);
}

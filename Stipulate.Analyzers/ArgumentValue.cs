using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// What is known at compile time about a value passed to a parameter: a constant, or the number
/// of elements of an array or collection written where it is passed.
/// </summary>
/// <param name="Description">How the messages name the value, e.g. "The argument 0".</param>
/// <param name="Type">The constant's type, as it will be boxed when it is checked.</param>
/// <param name="Length">A string constant's length, or a collection's number of elements.</param>
internal sealed record ArgumentValue(
    string Description,
    ITypeSymbol? Type,
    bool IsConstant,
    object? Constant,
    int? Length
)
{
    /// <summary>
    /// The largest integer a <see cref="double"/> holds exactly: beyond it, two different values can
    /// compare as equal.
    /// </summary>
    private const double LargestExactInteger = 9007199254740992;

    /// <summary>Whether the value is a <c>null</c> constant: a collection never is.</summary>
    public bool IsNull => this.IsConstant && this.Constant is null;

    /// <summary>
    /// The constant, if it is a number that converts to a <see cref="double"/> exactly enough to be
    /// compared with a bound; otherwise <c>null</c>. Not an enum's, a char's or a bool's, and not a
    /// collection's, which has no type here.
    /// </summary>
    public double? Number
        => IsNumeric(this.Type) && ToDouble(this.Constant!) is var number && Math.Abs(number) <= LargestExactInteger
            ? number
            : null;

    /// <summary>
    /// Whether the value is one of <paramref name="constants"/>, as <c>AllowedValues</c> and
    /// <c>DeniedValues</c> tell: <c>null</c> is <c>null</c>, and any other value only a value of
    /// the same type that it <see cref="object.Equals(object, object)"/>, once both are boxed. A
    /// collection is none of them.
    /// </summary>
    /// <returns>
    /// <c>null</c> if it can't tell: the constants are a description's own, or one is in error.
    /// </returns>
    public bool? IsAmong(ImmutableArray<TypedConstant> constants)
        => IsKnown(constants)
            ? constants.Any(this.IsSameAs)
            : null;

    /// <summary>A constant's value.</summary>
    public static ArgumentValue OfConstant(string description, ITypeSymbol? type, object? constant)
        => new(description, type, IsConstant: true, constant, (constant as string)?.Length);

    /// <summary>An array or collection whose number of elements is known.</summary>
    public static ArgumentValue OfCollection(string description, int count)
        => new(description, Type: null, IsConstant: false, Constant: null, count);

    /// <remarks>
    /// The types matter for an enum, whose constant is its underlying number. They are compared
    /// first: a value passed is never an array, so an array constant's
    /// <see cref="TypedConstant.Value"/>, which throws, is never read.
    /// </remarks>
    private bool IsSameAs(TypedConstant constant)
        => constant.IsNull
            ? this.IsNull
            : !this.IsNull
                && SymbolEqualityComparer.Default.Equals(constant.Type, this.Type)
                && Equals(constant.Value, this.Constant);

    private static bool IsKnown(ImmutableArray<TypedConstant> constants)
        => !constants.IsDefault && !constants.Any(static constant => constant.Kind == TypedConstantKind.Error);

    private static bool IsNumeric(ITypeSymbol? type)
        => type?.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;

    private static double ToDouble(object constant) => Convert.ToDouble(constant, CultureInfo.InvariantCulture);
}

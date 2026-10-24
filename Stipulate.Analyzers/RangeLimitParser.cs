using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// Reads the string limits of <c>Range(typeof(T), "minimum", "maximum")</c>; see
/// <see cref="RangeLimitText"/>.
/// </summary>
internal static class RangeLimitParser
{
    private const string InvariantCultureArgument = "ParseLimitsInInvariantCulture";

    private static readonly ImmutableDictionary<SpecialType, Func<RangeLimitText, double?>> BySpecialType =
        new Dictionary<SpecialType, Func<RangeLimitText, double?>>
        {
            [SpecialType.System_SByte] = static limit => limit.ToInteger(sbyte.MinValue, sbyte.MaxValue),
            [SpecialType.System_Byte] = static limit => limit.ToInteger(byte.MinValue, byte.MaxValue),
            [SpecialType.System_Int16] = static limit => limit.ToInteger(short.MinValue, short.MaxValue),
            [SpecialType.System_UInt16] = static limit => limit.ToInteger(ushort.MinValue, ushort.MaxValue),
            [SpecialType.System_Int32] = static limit => limit.ToInteger(int.MinValue, int.MaxValue),
            [SpecialType.System_UInt32] = static limit => limit.ToInteger(uint.MinValue, uint.MaxValue),
            [SpecialType.System_Int64] = static limit => limit.ToInteger(long.MinValue, long.MaxValue),
            [SpecialType.System_UInt64] = static limit => limit.ToInteger(ulong.MinValue, ulong.MaxValue),
            [SpecialType.System_Single] = static limit => limit.ToSingle(),
            [SpecialType.System_Double] = static limit => limit.ToDouble(),
            [SpecialType.System_Decimal] = static limit => limit.ToDecimal(),
            [SpecialType.System_DateTime] = static limit => limit.ToDateTime(),
        }.ToImmutableDictionary();

    /// <summary>The types without a <see cref="SpecialType"/>, by name.</summary>
    private static readonly ImmutableDictionary<string, Func<RangeLimitText, double?>> ByTypeName =
        new Dictionary<string, Func<RangeLimitText, double?>>
        {
            ["System.DateOnly"] = static limit => limit.ToDateOnly(),
            ["System.TimeOnly"] = static limit => limit.ToTimeOnly(),
            ["System.TimeSpan"] = static limit => limit.ToTimeSpan(),
        }.ToImmutableDictionary();

    /// <returns>
    /// The limit, or <c>null</c> if <paramref name="constant"/> isn't one of a
    /// <c>Range(typeof(T), ...)</c>'s string limits, T isn't supported, or the limit can't be
    /// compared.
    /// </returns>
    public static ParsedBound? Parse(TypedConstant constant, AttributeData attribute)
        => AttributeArguments.ScalarValue(constant) is string text && FindOperandType(attribute) is { } operandType
            ? ParseLimit(new RangeLimitText(text.Trim(), IsInvariant(attribute)), operandType)
            : null;

    /// <returns>The <c>T</c> of a <c>Range(typeof(T), ...)</c>, or <c>null</c> if the usage isn't one.</returns>
    private static ITypeSymbol? FindOperandType(AttributeData attribute)
        => IsRange(attribute)
            ? attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol
            : null;

    /// <remarks>Only a validation attribute, whose class resolved, is described.</remarks>
    private static bool IsRange(AttributeData attribute)
        => attribute.AttributeClass!.ToDisplayString() == ValidationAttributeTypes.RangeAttributeName;

    private static bool IsInvariant(AttributeData attribute)
        => AttributeArguments.Find(attribute, InvariantCultureArgument) is { Value: true };

    private static ParsedBound? ParseLimit(RangeLimitText limit, ITypeSymbol operandType)
    {
        double? value = FindParser(operandType)?.Invoke(limit);
        return value is { } bound && !double.IsNaN(bound)
            ? new ParsedBound(bound, limit.Text)
            : null;
    }

    private static Func<RangeLimitText, double?>? FindParser(ITypeSymbol operandType)
        => BySpecialType.TryGetValue(operandType.SpecialType, out Func<RangeLimitText, double?>? parser)
            ? parser
            : ByTypeName.GetValueOrDefault(operandType.ToDisplayString());
}

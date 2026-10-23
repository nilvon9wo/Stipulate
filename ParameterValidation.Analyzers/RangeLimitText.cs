using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ParameterValidation.Analyzers;

/// <summary>
/// One of <c>Range(typeof(T), "minimum", "maximum")</c>'s limits, which RangeAttribute parses with
/// T's TypeConverter in the current culture, unless <c>ParseLimitsInInvariantCulture</c> is set.
/// Each conversion returns <c>null</c> if the value might depend on the culture, or can't be
/// represented exactly enough as a <see cref="double"/> to compare.
/// </summary>
/// <param name="Text">The limit as written, trimmed.</param>
/// <param name="IsInvariant">Whether the attribute sets <c>ParseLimitsInInvariantCulture</c>.</param>
internal sealed record RangeLimitText(string Text, bool IsInvariant)
{
    /// <summary>2^53: every integer up to this is exactly representable as a <see cref="double"/>.</summary>
    private const decimal MaxExactInteger = 9007199254740992m;

    /// <summary>A <see cref="double"/> holds at least this many significant decimal digits exactly.</summary>
    private const int MaxExactSignificantDigits = 15;

    private const string DecimalPoint = ".";
    private const char Zero = '0';

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly string[] DateTimeFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"];
    private static readonly string[] DateOnlyFormats = ["yyyy-MM-dd"];
    private static readonly string[] TimeOnlyFormats = ["HH:mm", "HH:mm:ss"];

    /// <summary>[-][d.]hh:mm[:ss] is parsed the same in every culture.</summary>
    private static readonly Regex CultureInvariantTimeSpan = new(
        @"^-?([0-9]+\.)?[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?$",
        RegexOptions.CultureInvariant
    );

    /// <summary>Plain digits mean the same in every culture; a sign or a separator might not.</summary>
    private bool IsCultureSafe => this.IsInvariant || this.IsDigitsOnly;

    private bool IsDigitsOnly => this.Text.Length > 0 && this.Text.All(IsDigit);

    public double? ToInteger(decimal minimum, decimal maximum)
        => this.ParseCultureSafeDecimal(NumberStyles.Integer) is { } integer
            && integer >= minimum
            && integer <= maximum
            && Math.Abs(integer) <= MaxExactInteger
            ? (double)integer
            : null;

    public double? ToSingle()
        => this.IsCultureSafe && float.TryParse(this.Text, NumberStyles.Float, Culture, out float single)
            ? single
            : null;

    public double? ToDouble()
        => this.IsCultureSafe && double.TryParse(this.Text, NumberStyles.Float, Culture, out double number)
            ? number
            : null;

    public double? ToDecimal()
        => this.ParseCultureSafeDecimal(NumberStyles.Number) is { } money
            && CountSignificantDigits(money) <= MaxExactSignificantDigits
            ? (double)money
            : null;

    public double? ToDateTime()
        => this.IsInvariant
            ? ToExactTicks(this.ParseDateTime(DateTimeFormats)?.Ticks)
            : null;

    /// <returns>The date as a day number.</returns>
    public double? ToDateOnly()
        => this.IsInvariant && this.ParseDateTime(DateOnlyFormats) is { } date
            ? date.Ticks / TimeSpan.TicksPerDay
            : null;

    public double? ToTimeOnly()
        => this.IsInvariant
            ? ToExactTicks(this.ParseDateTime(TimeOnlyFormats)?.TimeOfDay.Ticks)
            : null;

    public double? ToTimeSpan()
        => CultureInvariantTimeSpan.IsMatch(this.Text) && TimeSpan.TryParse(this.Text, Culture, out TimeSpan span)
            ? ToExactTicks(span.Ticks)
            : null;

    private decimal? ParseCultureSafeDecimal(NumberStyles styles)
        => this.IsCultureSafe && decimal.TryParse(this.Text, styles, Culture, out decimal parsed)
            ? parsed
            : null;

    private DateTime? ParseDateTime(string[] formats)
        => DateTime.TryParseExact(this.Text, formats, Culture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : null;

    private static bool IsDigit(char character) => character is >= '0' and <= '9';

    private static double? ToExactTicks(long? ticks)
        => ticks is { } exact && (long)(double)exact == exact
            ? exact
            : null;

    private static int CountSignificantDigits(decimal number)
    {
        string digits = Math.Abs(number).ToString(Culture).Replace(DecimalPoint, string.Empty);
        return digits.TrimStart(Zero).TrimEnd(Zero).Length;
    }
}

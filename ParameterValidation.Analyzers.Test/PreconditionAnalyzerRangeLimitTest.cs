using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.ParameterValidation.Testing;

namespace Net.NowhereAtAll.ParameterValidation.Analyzers.Test;

/// <summary>
/// <c>Range(typeof(T), "minimum", "maximum")</c>, whose limits are compared when they can't depend
/// on the culture and are exact, and otherwise only with identical usages.
/// </summary>
public class PreconditionAnalyzerRangeLimitTest
{
    private const string DateTime = "System.DateTime";
    private const string DateOnly = "System.DateOnly";
    private const string TimeOnly = "System.TimeOnly";
    private const string TimeSpan = "System.TimeSpan";

    /// <summary>Contract, implementation, parameter type.</summary>
    public static TheoryData<string, string, string> NoStrongerLimits => new()
    {
        { "Range(1, 10)", Range("int", "0", "10"), "int" },
        { Range("int", "1", "10"), "Range(0, 10)", "int" },
        { Range("byte", "1", "10"), Range("byte", "0", "10"), "byte" },
        { Range("sbyte", "1", "10"), Range("sbyte", "0", "10"), "sbyte" },
        { Range("short", "1", "10"), Range("short", "0", "10"), "short" },
        { Range("ushort", "1", "10"), Range("ushort", "0", "10"), "ushort" },
        { Range("uint", "1", "10"), Range("uint", "0", "10"), "uint" },
        { Range("long", "1", "10"), Range("long", "0", "10"), "long" },
        { Range("ulong", "1", "10"), Range("ulong", "0", "10"), "ulong" },
        { Range("float", "1", "10"), Range("float", "0", "10"), "float" },
        { Invariant("double", "0.5", "10"), Invariant("double", "0.25", "10"), "double" },
        { Invariant("int", "-5", "5"), Invariant("int", "-6", "5"), "int" },
        {
            Invariant(DateTime, "2020-01-01", "2030-12-31"),
            Invariant(DateTime, "2019-01-01", "2030-12-31T12:00:00"),
            DateTime
        },
        { Invariant(TimeOnly, "09:00", "17:00"), Invariant(TimeOnly, "08:30", "17:00:00"), TimeOnly },
        { Range(TimeSpan, "00:00:01", "1.00:00"), Range(TimeSpan, "00:00:00", "2.00:00"), TimeSpan },
        { Range(DateTime, "1/1/2020", "1/1/2030"), Range(DateTime, "1/1/2020", "1/1/2030"), DateTime },
    };

    public static TheoryData<string, string, string> StrongerLimits => new()
    {
        { Range("int", "1", "10"), Range("int", "2", "10"), "int" },
        { Invariant("int", "-5", "5"), Invariant("int", "-4", "5"), "int" },
        { Invariant("decimal", "0.5", "10"), Invariant("decimal", "0.75", "10"), "decimal" },
        {
            Invariant(DateTime, "2020-01-01", "2030-12-31"),
            Invariant(DateTime, "2020-01-01", "2030-12-30 23:59:59"),
            DateTime
        },
        { Invariant(DateOnly, "2020-01-01", "2030-12-31"), Invariant(DateOnly, "2020-01-02", "2030-12-31"), DateOnly },
        { Range(TimeSpan, "00:00:01", "1.00:00"), Range(TimeSpan, "00:00:01", "12:00"), TimeSpan },
    };

    /// <remarks>
    /// A sign, a decimal point or a date depends on the culture unless the limits are parsed in the
    /// invariant one; and some values can't be held exactly enough to compare.
    /// </remarks>
    public static TheoryData<string, string, string> IncomparableLimits => new()
    {
        { Range("int", "-5", "5"), Range("int", "-4", "5"), "int" },
        { Range("double", "0.5", "10"), Range("double", "0.25", "10"), "double" },
        { Range("decimal", "0.5", "10"), Range("decimal", "0.25", "10"), "decimal" },
        { Range(DateTime, "2020-01-01", "2030-12-31"), Range(DateTime, "2019-01-01", "2030-12-31"), DateTime },
        { Range(DateOnly, "2020-01-01", "2030-12-31"), Range(DateOnly, "2019-01-01", "2030-12-31"), DateOnly },
        { Range(TimeOnly, "09:00", "17:00"), Range(TimeOnly, "08:00", "17:00"), TimeOnly },
        { Range(TimeSpan, "00:00:01", "1.00:00"), Range(TimeSpan, "00:00:01", "1 day"), TimeSpan },
        { Range("long", "0", "9007199254740992"), Range("long", "0", "9007199254740993"), "long" },
        { Invariant("decimal", "0", "2"), Invariant("decimal", "0", "1.0000000000000001"), "decimal" },
        { Range("byte", "0", "200"), Range("byte", "0", "300"), "byte" },
        { Range("char", "a", "z"), Range("char", "b", "z"), "char" },
        { Invariant("double", "0", "10"), Invariant("double", "0", "NaN"), "double" },
        { Range("int", "0", "10"), Range("int", "", "10"), "int" },
        { Invariant("int", "0", "10"), Invariant("int", "x", "10"), "int" },
        { Invariant("byte", "0", "10"), Invariant("byte", "-1", "10"), "byte" },
        { Range("float", "1", "10"), Range("float", "0.5", "10"), "float" },
        { Invariant("float", "1", "10"), Invariant("float", "x", "10"), "float" },
        { Invariant("double", "1", "10"), Invariant("double", "x", "10"), "double" },
        { Invariant(DateTime, "2020-01-01", "2030-12-31"), Invariant(DateTime, "2020-01-01", "never"), DateTime },
        {
            Invariant(DateTime, "2020-01-01", "2030-12-31"),
            Invariant(DateTime, "2020-01-01", "9999-12-31T23:59:59"),
            DateTime
        },
        { Invariant(DateOnly, "2020-01-01", "2030-12-31"), Invariant(DateOnly, "2020-13-01", "2030-12-31"), DateOnly },
        { Invariant(TimeOnly, "09:00", "17:00"), Invariant(TimeOnly, "09:00", "25:00"), TimeOnly },
        { Range(TimeSpan, "00:00:01", "1.00:00"), Range(TimeSpan, "00:00:01", "99:99"), TimeSpan },
    };

    [Theory]
    [MemberData(nameof(NoStrongerLimits))]
    public async Task Analyze_WhenTheImplementationsLimitsAreNoStronger_ReportsNothing(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, implementation, type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [MemberData(nameof(StrongerLimits))]
    public async Task Analyze_WhenTheImplementationsLimitsAreStronger_ReportsPV1001(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, $"{{|PV1001:{implementation}|}}", type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [MemberData(nameof(IncomparableLimits))]
    public async Task Analyze_WhenTheImplementationsLimitsCannotBeCompared_ReportsPV2002(
        string contract,
        string implementation,
        string type
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(ContractSource.ForParameter(contract, $"{{|PV2002:{implementation}|}}", type));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    private static string Range(string type, string minimum, string maximum)
        => $"Range(typeof({type}), \"{minimum}\", \"{maximum}\")";

    private static string Invariant(string type, string minimum, string maximum)
        => $"Range(typeof({type}), \"{minimum}\", \"{maximum}\", ParseLimitsInInvariantCulture = true)";
}

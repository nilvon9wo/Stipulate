using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Analyzers.Test;

/// <summary>
/// Which values known at compile time a validated parameter's attributes are found to reject:
/// every one certain to be rejected, and none that might not be.
/// </summary>
public class ArgumentAnalyzerTest
{
    [Theory]
    [InlineData("Range(1, 10)", "int", "0")]
    [InlineData("Range(1, 10)", "int", "11")]
    [InlineData("Range(1, 10, MinimumIsExclusive = true)", "int", "1")]
    [InlineData("Range(1, 10, MaximumIsExclusive = true)", "int", "10")]
    [InlineData("Range(1, 10)", "long", "0L")]
    [InlineData("Range(1, 10)", "double", "0.0")]
    [InlineData("Range(1.5, 10)", "double", "1.25")]
    [InlineData("Range(1, 10)", "object", "0")]
    [InlineData("Range(1, 10)", "int?", "0")]
    [InlineData("MinLength(1)", "int[]", "[]")]
    [InlineData("MinLength(1)", "int[]", "new int[0]")]
    [InlineData("MinLength(1)", "int[]", "new int[] { }")]
    [InlineData("MinLength(2)", "System.Collections.Generic.List<int>", "[1]")]
    [InlineData("MaxLength(1)", "int[]", "new[] { 1, 2 }")]
    [InlineData("MinLength(1)", "string", "\"\"")]
    [InlineData("MaxLength(3)", "string", "\"four\"")]
    [InlineData("StringLength(3)", "string", "\"four\"")]
    [InlineData("StringLength(10, MinimumLength = 2)", "string", "\"a\"")]
    [InlineData("Length(2, 3)", "string", "\"a\"")]
    [InlineData("Required", "string", "null")]
    [InlineData("Required", "string", "\"\"")]
    [InlineData("Required", "string", "\" \"")]
    [InlineData("Required", "object", "null")]
    [InlineData("Required", "int?", "null")]
    [InlineData("AllowedValues(1, 2)", "int", "3")]
    [InlineData("AllowedValues(\"a\", \"b\")", "string", "\"c\"")]
    [InlineData("AllowedValues(\"a\")", "string", "null")]
    [InlineData("AllowedValues(1, 2)", "object", "3")]
    [InlineData("AllowedValues(\"a\")", "object", "\"c\"")]
    [InlineData("AllowedValues(System.DayOfWeek.Monday)", "System.DayOfWeek", "System.DayOfWeek.Sunday")]
    [InlineData("Range(1, 10)", "object", "(object)0")]

    // Neither a type nor an array, nor any value but null, is ever Equal to a constant or an array passed.
    [InlineData("AllowedValues(typeof(int))", "object", "1")]
    [InlineData("AllowedValues(new[] { 1 })", "int", "1")]
    [InlineData("AllowedValues(1)", "object", "System.DayOfWeek.Monday")]
    [InlineData("AllowedValues(1, 2)", "int[]", "[]")]

    // A boxed long is never Equal to a boxed int, so AllowedValues(1, 2) rejects every long.
    [InlineData("AllowedValues(1, 2)", "long", "1L")]
    [InlineData("DeniedValues(0)", "int", "0")]
    [InlineData("DeniedValues(\"x\")", "string", "\"x\"")]
    [InlineData("DeniedValues(1)", "int?", "1")]
    public async Task Analyze_WhenAnAttributeRejectsTheArgument_ReportsST3001OnIt(
        string attribute,
        string type,
        string argument
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(Call(attribute, type, $"{{|ST3001:{argument}|}}"));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("Range(1, 10)", "int", "1")]
    [InlineData("Range(1, 10)", "int", "10")]
    [InlineData("Range(1, 10)", "int?", "null")]
    [InlineData("MinLength(1)", "int[]", "new int[1]")]
    [InlineData("MinLength(1)", "string", "null")]
    [InlineData("MaxLength(-1)", "string", "\"no maximum\"")]
    [InlineData("Required", "int", "0")]
    [InlineData("Required", "string", "\"a\"")]
    [InlineData("Required(AllowEmptyStrings = true)", "string", "\"\"")]
    [InlineData("AllowedValues(1, 2)", "int", "2")]
    [InlineData("AllowedValues(null, \"a\")", "string", "null")]
    [InlineData("DeniedValues(0)", "int", "1")]
    [InlineData("AllowedValues(System.DayOfWeek.Monday)", "System.DayOfWeek", "System.DayOfWeek.Monday")]
    [InlineData("Required", "int[]", "[]")]

    // A boxed long is never Equal to a boxed int, so DeniedValues(1) denies no long; nor are two enums
    // of different types Equal, though their numbers are.
    [InlineData("DeniedValues(1)", "long", "1L")]
    [InlineData("DeniedValues(System.DayOfWeek.Monday)", "object", "System.StringComparison.CurrentCultureIgnoreCase")]

    // Not known at compile time.
    [InlineData("Range(1, 10)", "int", "int.Parse(\"0\")")]
    [InlineData("Range(1, 10)", "long", "int.Parse(\"0\")")]
    [InlineData("MinLength(1)", "int[]", "System.Array.Empty<int>()")]
    [InlineData("MaxLength(1)", "int[]", "[1, .. System.Array.Empty<int>()]")]

    // A set can merge elements, so its count isn't the number written.
    [InlineData("MinLength(3)", "System.Collections.Generic.HashSet<int>", "[1, 1]")]

    // Range(int, int) rounds a fraction to an int before comparing it: 0.6 is 1.
    [InlineData("Range(1, 10)", "double", "0.6")]
    [InlineData("Range(1, 10)", "double", "10.4")]

    // A bound given as text is compared in the type's own units.
    [InlineData("Range(typeof(int), \"1\", \"10\")", "int", "0")]

    // Range converts what isn't a number at run time; and beyond 2^53, a double can't tell numbers apart.
    [InlineData("Range(1, 10)", "string", "\"5\"")]
    [InlineData("Range(1, 10)", "int[]", "[]")]
    [InlineData("Range(1, 10)", "long", "long.MaxValue")]
    public async Task Analyze_WhenTheArgumentIsAcceptedOrCanNotBeTold_ReportsNothing(
        string attribute,
        string type,
        string argument
    )
    {
        // Arrange
        DiagnosticMarkup markup = new(Call(attribute, type, argument));

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheAttributeCanNotBeDescribed_ReportsNothing()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public sealed class EvenAttribute : ValidationAttribute { }

            public static class Target { public static void Accept([Even] int value) { } }

            public static class Caller { public static void Call() => Target.Accept(1); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADescribedAttributeRejectsTheArgument_ReportsST3001()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            [ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Value = 0)]
            public sealed class NonNegativeAttribute : ValidationAttribute { }

            public static class Target { public static void Accept([NonNegative] int value) { } }

            public static class Caller { public static void Call() => Target.Accept({|ST3001:-1|}); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Theory]
    [InlineData("AllowedValues")]
    [InlineData("DeniedValues")]
    public async Task Analyze_WhenADescriptionGivesItsOwnValues_ChecksNoArgumentAgainstThem(string comparison)
    {
        // Arrange - a description's values are only their literals: what they are equal to is unknown
        DiagnosticMarkup markup = new(
            $$"""
            [ComparableValidation(ValidationDimensions.Value, ValidationComparison.{{comparison}}, Value = new[] { 1 })]
            public sealed class OneAttribute : ValidationAttribute { }

            public static class Target { public static void Accept([One] int value) { } }

            public static class Caller
            {
                public static void Call()
                {
                    Target.Accept(1);
                    Target.Accept(2);
                }
            }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenUserCodeConvertsTheArgument_ReportsNothing()
    {
        // Arrange - the value checked is the conversion's result, which isn't known
        DiagnosticMarkup markup = new(
            """
            public readonly struct Level(int value)
            {
                public int Value { get; } = value;

                public static implicit operator Level(int value) => new(value);
            }

            public static class Target { public static void Accept([Range(1, 10)] Level? level) { } }

            public static class Caller { public static void Call() => Target.Accept(0); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTooFewParamsArgumentsMakeAList_ReportsST3001OnTheCall()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public static class Target
            {
                public static void Accept([MinLength(2)] params System.Collections.Generic.List<int> values) { }
            }

            public static class Caller { public static void Call() => {|ST3001:Target.Accept(1)|}; }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenParamsArgumentsMakeAnUncountedCollection_ReportsNothing()
    {
        // Arrange - the compiler chooses the collection an IEnumerable<int> gets
        DiagnosticMarkup markup = new(
            """
            public static class Target
            {
                public static void Accept([MinLength(2)] params System.Collections.Generic.IEnumerable<int> values) { }
            }

            public static class Caller { public static void Call() => Target.Accept(1); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnArgumentIsInAnArglist_ReportsNothing()
    {
        // Arrange - an __arglist's arguments have no parameter
        DiagnosticMarkup markup = new(
            """
            public static class Target { public static void Accept([Range(1, 10)] int value, __arglist) { } }

            public static class Caller { public static void Call() => Target.Accept(1, __arglist(0)); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSeveralAttributesRejectTheArgument_ReportsEach()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public static class Target { public static void Accept([Required, MinLength(1)] string value) { } }

            public static class Caller { public static void Call() => Target.Accept(""); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        Assert.Equal(2, diagnostics.Count(static diagnostic => diagnostic.Id == "ST3001"));
    }

    [Fact]
    public async Task Analyze_WhenTheMethodIsAConstructor_ReportsST3001()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public class Target { public Target([Range(1, 10)] int value) { } }

            public class Derived : Target { public Derived() : base({|ST3001:0|}) { } }

            public static class Caller { public static Target Create() => new Target({|ST3001:11|}); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTheCallIsThroughAnInterface_UsesTheInterfacesAttributes()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public interface IScale { void Set([Range(1, 10)] int level); }

            public static class Caller { public static void Call(IScale scale) => scale.Set({|ST3001:0|}); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenADefaultValueIsRejected_ReportsST3001OnTheDefaultNotTheCalls()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public static class Target { public static void Accept([Range(1, 10)] int value = {|ST3001:0|}) { } }

            public static class Caller { public static void Call() => Target.Accept(); }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTooFewParamsArgumentsArePassed_ReportsST3001OnTheCall()
    {
        // Arrange
        DiagnosticMarkup markup = new(
            """
            public static class Target { public static void Accept([MinLength(2)] params int[] values) { } }

            public static class Caller
            {
                public static void Call()
                {
                    {|ST3001:Target.Accept()|};
                    {|ST3001:Target.Accept(1)|};
                    Target.Accept(1, 2);
                }
            }
            """
        );

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        DiagnosticAssert.MatchesMarkup(markup, diagnostics);
    }

    public static TheoryData<string, string> Messages => new()
    {
        {
            Call("Range(1, 10)", "int", "0"),
            "The argument 0 for parameter 'value' of 'Target.Accept(int)' is rejected by [Range(1, 10)], which "
            + "requires Value >= 1, but Value is 0. Wherever Stipulate enforces the attribute at run time, this "
            + "call throws ArgumentException. Pass a value [Range(1, 10)] accepts, or, if the value is right, "
            + "correct the attribute. If the call is meant to throw, as in a test of the check, suppress ST3001 "
            + "there."
        },
        {
            "public static class Values { public const int Zero = 0; }\n" + Call("Range(1, 10)", "int", "Values.Zero"),
            "The argument Values.Zero (0) for parameter 'value' of 'Target.Accept(int)' is rejected by "
            + "[Range(1, 10)], which requires Value >= 1, but Value is 0" + ThrowsAndFix("Range(1, 10)")
        },
        {
            Call("MinLength(1)", "int[]", "[]"),
            "The argument [] for parameter 'value' of 'Target.Accept(int[])' is rejected by [MinLength(1)], which "
            + "requires Length >= 1, but Length is 0" + ThrowsAndFix("MinLength(1)")
        },
        {
            Call("Required", "string", "null"),
            "The argument null for parameter 'value' of 'Target.Accept(string)' is rejected by [Required], which "
            + "rejects null" + ThrowsAndFix("Required", exception: "ArgumentNullException")
        },
        {
            Call("Required", "string", "\"\""),
            "The argument \"\" for parameter 'value' of 'Target.Accept(string)' is rejected by [Required], which "
            + "rejects empty and whitespace-only strings" + ThrowsAndFix("Required")
        },
        {
            Call("AllowedValues(\"a\")", "string", "null"),
            "The argument null for parameter 'value' of 'Target.Accept(string)' is rejected by "
            + "[AllowedValues(\"a\")], which accepts only \"a\"" + ThrowsAndFix("AllowedValues(\"a\")")
        },
        {
            Call("AllowedValues(1, 2)", "int", "3"),
            "The argument 3 for parameter 'value' of 'Target.Accept(int)' is rejected by [AllowedValues(1, 2)], "
            + "which accepts only 1, 2" + ThrowsAndFix("AllowedValues(1, 2)")
        },
        {
            Call("DeniedValues(0)", "int", "0"),
            "The argument 0 for parameter 'value' of 'Target.Accept(int)' is rejected by [DeniedValues(0)], "
            + "which lists it among the values it denies" + ThrowsAndFix("DeniedValues(0)")
        },
        {
            "public static class Target { public static void Accept([Range(1, 10)] int value = 0) { } }",
            "The default value 0 for parameter 'value' of 'Target.Accept(int)' is rejected by [Range(1, 10)], "
            + "which requires Value >= 1, but Value is 0"
            + ThrowsAndFix("Range(1, 10)", calls: "every call that leaves the argument out")
        },
        {
            """
            public static class Target { public static void Accept([MinLength(1)] params int[] values) { } }

            public static class Caller { public static void Call() => Target.Accept(); }
            """,
            "The params array of 0 arguments for parameter 'values' of 'Target.Accept(params int[])' is rejected "
            + "by [MinLength(1)], which requires Length >= 1, but Length is 0" + ThrowsAndFix("MinLength(1)")
        },
        {
            """
            public static class Target
            {
                public static void Accept([MinLength(2)] params System.Collections.Generic.List<int> values) { }
            }

            public static class Caller { public static void Call() => Target.Accept(1); }
            """,
            "The params collection of 1 argument for parameter 'values' of 'Target.Accept(params List<int>)' is "
            + "rejected by [MinLength(2)], which requires Length >= 2, but Length is 1" + ThrowsAndFix("MinLength(2)")
        },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task Analyze_WhenAnArgumentIsRejected_SaysWhatRejectsWhatAndWhy(string source, string message)
    {
        // Arrange
        DiagnosticMarkup markup = new(source);

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerRunner.RunAsync(markup);

        // Assert
        Assert.Equal(message, Assert.Single(diagnostics).GetMessage());
    }

    /// <summary>The end of every message: what the value makes throw, and what to do about it.</summary>
    private static string ThrowsAndFix(
        string attribute,
        string calls = "this call",
        string exception = "ArgumentException"
    )
        => $". Wherever Stipulate enforces the attribute at run time, {calls} throws {exception}. Pass a value "
            + $"[{attribute}] accepts, or, if the value is right, correct the attribute. If the call is meant to "
            + "throw, as in a test of the check, suppress ST3001 there.";

    /// <summary>A method with one validated parameter, and a call that passes it <paramref name="argument"/>.</summary>
    private static string Call(string attribute, string type, string argument)
        => $$"""
            public static class Target { public static void Accept([{{attribute}}] {{type}} value) { } }

            public static class Caller { public static void Call() => Target.Accept({{argument}}); }
            """;
}

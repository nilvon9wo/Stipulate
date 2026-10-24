using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net.NowhereAtAll.Stipulate.Testing;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// The run-time check the woven methods and the interceptors call, Stipulate.ArgumentChecks.cs,
/// which Stipulate.targets compiles into every project that enforces the attributes: what its
/// messages say, and that it compiles in any of those projects.
/// </summary>
public class ArgumentChecksTest
{
    private const string Method = "Scale.Set(int)";
    private const string Parameter = "level";

    private static readonly Lazy<MethodInfo> Check = new(LoadCheck);

    [Fact]
    public void Compile_AsCSharp73_ReportsNothing()
    {
        // Arrange - the oldest language a consumer may compile it with, with every warning on
        SyntaxTree tree = TestCompilation.ParseArgumentChecks(new CSharpParseOptions(LanguageVersion.CSharp7_3));
        CSharpCompilationOptions options = new(OutputKind.DynamicallyLinkedLibrary, warningLevel: 9999);

        // Act
        CSharpCompilation compilation = CSharpCompilation.Create("Checks", [tree], TestCompilation.References, options);

        // Assert
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Check_WhenTheAttributeAcceptsTheValue_DoesNotThrow()
    {
        // Arrange
        RangeAttribute range = new(1, 10);

        // Act
        Exception? exception = Record.Exception(() => Invoke(range, 5, showsValue: false));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Check_WhenTheAttributeRejectsTheValue_NamesTheMethodTheParameterAndTheAttribute()
    {
        // Arrange
        RangeAttribute range = new(1, 10);

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Invoke(range, 0, showsValue: false));

        // Assert
        Assert.Equal(Parameter, exception.ParamName);
        Assert.Equal(
            "Scale.Set(int) rejected argument 'level' by [Range]: The field level must be between 1 and 10. "
            + "The argument was 0. (Parameter 'level')",
            exception.Message
        );
    }

    [Fact]
    public void Check_WhenRequiredRejectsNull_ThrowsArgumentNullException()
    {
        // Arrange
        RequiredAttribute required = new();

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Invoke(required, null, showsValue: false)
        );

        // Assert
        Assert.Equal(Parameter, exception.ParamName);
        Assert.Contains("The argument was null.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WhenAnAttributeOtherThanRequiredRejectsNull_ThrowsArgumentException()
    {
        // Arrange
        RejectAllAttribute rejectAll = new();

        // Act
        Exception exception = Assert.ThrowsAny<ArgumentException>(() => Invoke(rejectAll, null, showsValue: false));

        // Assert
        _ = Assert.IsType<ArgumentException>(exception);
    }

    [Theory]
    [InlineData("Out of range", "Out of range.")]
    [InlineData("Out of range.  ", "Out of range.")]
    [InlineData("Out of range!", "Out of range!")]
    [InlineData("Out of range?", "Out of range?")]
    public void Check_WhenTheAttributesMessageIsNotASentence_EndsItAsOne(string errorMessage, string expected)
    {
        // Arrange
        RejectAllAttribute rejectAll = new() { ErrorMessage = errorMessage };

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Invoke(rejectAll, 0, showsValue: false));

        // Assert
        Assert.Contains($"[RejectAll]: {expected} The argument was", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WhenTheAttributesNameDoesNotEndInAttribute_ShowsItWhole()
    {
        // Arrange
        Strict strict = new();

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Invoke(strict, 0, showsValue: false));

        // Assert
        Assert.Contains("by [Strict]:", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Described), DisableDiscoveryEnumeration = true)]
    public void Check_WhenTheValueIsRejected_DescribesIt(object? value, bool showsValue, string expected)
    {
        // Arrange
        RejectAllAttribute rejectAll = new();

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Invoke(rejectAll, value, showsValue));

        // Assert
        Assert.Contains($" The argument was {expected}. (Parameter", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without [ShowValueInMessage], only what can't be a secret: a string's length, but a number's value.
    /// </summary>
    public static TheoryData<object?, bool, string> Described => new()
    {
        { null, false, "null" },
        { string.Empty, false, "an empty string" },
        { "   ", false, "a string of 3 whitespace characters" },
        { "a", false, "a string of 1 character" },
        { "secret", false, "a string of 6 characters" },
        { "secret", true, "\"secret\"" },
        { true, false, "true" },
        { false, false, "false" },
        { 0, false, "0" },
        { 1.5, false, "1.5" },
        { DayOfWeek.Monday, false, "Monday" },
        { new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), false, "01/02/2026 00:00:00" },
        { Array.Empty<int>(), false, "a collection of 0 elements" },
        { new List<int> { 1 }, false, "a collection of 1 element" },
        { 'x', false, "x" },
        { new KeyValuePair<string, int>("a", 1), false, "a value of type KeyValuePair<String, Int32>" },
        { new Uri("https://example.com/"), false, "a value of type Uri" },
        { new Uri("https://example.com/"), true, "https://example.com/" },
    };

    private static void Invoke(ValidationAttribute attribute, object? value, bool showsValue)
        => Check.Value.Invoke(
            null,
            BindingFlags.DoNotWrapExceptions,
            binder: null,
            [attribute, value, Parameter, Method, showsValue],
            culture: null
        );

    private static MethodInfo LoadCheck()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Checks",
            [TestCompilation.ArgumentChecks],
            TestCompilation.References,
            TestCompilation.CompilationOptions
        );
        using MemoryStream image = new([.. TestCompilation.Emit(compilation)]);
        Assembly assembly = new AssemblyLoadContext(nameof(ArgumentChecksTest), isCollectible: true)
            .LoadFromStream(image);
        return assembly.GetType("Net.NowhereAtAll.Stipulate.Runtime.ArgumentChecks", throwOnError: true)!
            .GetMethod("Check", BindingFlags.Public | BindingFlags.Static)!;
    }

    private sealed class RejectAllAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value) => false;
    }

    /// <summary>A validation attribute whose name doesn't end in "Attribute".</summary>
    private sealed class Strict : ValidationAttribute
    {
        public override bool IsValid(object? value) => false;
    }
}

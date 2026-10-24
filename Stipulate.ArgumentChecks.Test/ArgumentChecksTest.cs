using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.ArgumentChecks.Test;

/// <summary>
/// The run-time check the woven methods and the interceptors call, which Stipulate.targets compiles
/// into every project that enforces the attributes, in the project's language: Stipulate.ArgumentChecks.cs,
/// .vb or .fs. Each language's copy has a subclass, which runs every test here against it, so that
/// the copies can't drift apart; and every copy must be covered in full, so that a branch one of
/// them has is a branch these tests reach in all three.
/// </summary>
public abstract class ArgumentChecksTest
{
    private const string Method = "Scale.Set(int)";
    private const string Parameter = "level";
    private const string ChecksName = "Net.NowhereAtAll.Stipulate.Runtime.ArgumentChecks";
    private const string CheckName = "Check";

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
        { new List<int>().GetEnumerator(), false, "a value of type Enumerator<Int32>" },
        { new Version(1, 2), false, "a value of type Version" },
        { new Uri("https://example.com/"), false, "a value of type Uri" },
        { new Uri("https://example.com/"), true, "https://example.com/" },
        { new Unprintable(), true, string.Empty },
    };

    /// <summary>The assembly that compiles this language's copy of the check.</summary>
    protected abstract string AssemblyName { get; }

    private Type Checks => Assembly.Load(this.AssemblyName).GetType(ChecksName, throwOnError: true)!;

    [Fact]
    public void Check_TakesWhatTheWeaverAndTheInterceptorsPass()
    {
        // Arrange
        MethodInfo check = this.FindCheck();

        // Act
        Type[] parameters = [.. check.GetParameters().Select(static parameter => parameter.ParameterType)];

        // Assert
        Type[] passed = [typeof(ValidationAttribute), typeof(object), typeof(string), typeof(string), typeof(bool)];
        Assert.Equal(passed, parameters);
        Assert.Equal(typeof(void), check.ReturnType);
    }

    [Fact]
    public void Checks_IsHiddenFromTheProjectItIsCompiledInto()
    {
        // Arrange
        Type checks = this.Checks;

        // Act
        EditorBrowsableAttribute? browsable = checks.GetCustomAttribute<EditorBrowsableAttribute>();

        // Assert
        Assert.False(checks.IsPublic);
        Assert.True(checks.IsSealed);
        Assert.Equal(EditorBrowsableState.Never, browsable?.State);
        Assert.NotNull(checks.GetCustomAttribute<DebuggerNonUserCodeAttribute>());
    }

    [Fact]
    public void Check_WhenTheAttributeAcceptsTheValue_DoesNotThrow()
    {
        // Arrange
        RangeAttribute range = new(1, 10);

        // Act
        Exception? exception = Record.Exception(() => this.Invoke(range, 5, showsValue: false));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Check_WhenTheAttributeRejectsTheValue_NamesTheMethodTheParameterAndTheAttribute()
    {
        // Arrange
        RangeAttribute range = new(1, 10);

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => this.Invoke(range, 0, showsValue: false));

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
            () => this.Invoke(required, null, showsValue: false)
        );

        // Assert
        Assert.Equal(Parameter, exception.ParamName);
        Assert.Contains("The argument was null.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WhenRequiredRejectsAnEmptyString_ThrowsArgumentException()
    {
        // Arrange
        RequiredAttribute required = new();

        // Act
        Exception exception = Assert.ThrowsAny<ArgumentException>(
            () => this.Invoke(required, string.Empty, showsValue: false)
        );

        // Assert
        _ = Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Check_WhenAnAttributeOtherThanRequiredRejectsNull_ThrowsArgumentException()
    {
        // Arrange
        RejectAllAttribute rejectAll = new();

        // Act
        Exception exception = Assert.ThrowsAny<ArgumentException>(
            () => this.Invoke(rejectAll, null, showsValue: false)
        );

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
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => this.Invoke(rejectAll, 0, showsValue: false)
        );

        // Assert
        Assert.Contains($"[RejectAll]: {expected} The argument was", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WhenTheAttributesMessageIsNull_EndsTheEmptyMessageAsASentence()
    {
        // Arrange
        SilentAttribute silent = new();

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => this.Invoke(silent, 0, showsValue: false));

        // Assert
        Assert.Contains("by [Silent]: . The argument was", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(Strict), "Strict")]
    [InlineData(typeof(Named.Attribute), "Attribute")]
    public void Check_WhenTheAttributesNameDoesNotEndInAttribute_ShowsItWhole(Type attributeType, string expected)
    {
        // Arrange
        ValidationAttribute attribute = (ValidationAttribute)Activator.CreateInstance(attributeType)!;

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => this.Invoke(attribute, 0, showsValue: false)
        );

        // Assert
        Assert.Contains($"by [{expected}]:", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Described), DisableDiscoveryEnumeration = true)]
    public void Check_WhenTheValueIsRejected_DescribesIt(object? value, bool showsValue, string expected)
    {
        // Arrange
        RejectAllAttribute rejectAll = new();

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => this.Invoke(rejectAll, value, showsValue)
        );

        // Assert
        Assert.Contains($" The argument was {expected}. (Parameter", exception.Message, StringComparison.Ordinal);
    }

    private void Invoke(ValidationAttribute attribute, object? value, bool showsValue)
        => this.FindCheck().Invoke(
            null,
            BindingFlags.DoNotWrapExceptions,
            binder: null,
            [attribute, value, Parameter, Method, showsValue],
            culture: null
        );

    /// <summary>
    /// Public or not: F# makes an internal type's members internal, and every caller is in the same assembly.
    /// </summary>
    private MethodInfo FindCheck()
        => this.Checks.GetMethod(CheckName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
}

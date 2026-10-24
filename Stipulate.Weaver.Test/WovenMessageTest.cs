using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// What a woven check's message says: which method rejected the argument, named as C# writes it
/// (as the interceptors name it), and the value itself only where the parameter has
/// <c>[ShowValueInMessage]</c>.
/// </summary>
public class WovenMessageTest
{
    private const string Target = "Target";
    private const BindingFlags Unwrapped = BindingFlags.DoNotWrapExceptions;

    private const string Source = """
        public class Box<T>
        {
            public class Inner<U>
            {
                public static void Put([Range(1, 10)] int level, T first, U second) { }
            }
        }

        public class Target
        {
            public Target() { }

            public Target([Range(1, 10)] int size) { }

            public int Level { get; [param: Range(1, 10)] set; }

            public int Initial { get; [param: Range(1, 10)] init; }

            public int this[int index] { get => index; [param: Range(1, 10)] set { } }

            public static void Many(
                [Range(1, 10)] int level,
                string text,
                int[,] grid,
                (int, string) pair,
                int? maybe,
                System.Collections.Generic.List<int> list,
                object anything,
                ref int counted) { }

            public virtual void Read([Range(1, 10)] int level, in int read) { }

            public static void Generic<TValue>([Range(1, 10)] int level, TValue value) { }

            public static void Hidden([MinLength(5)] string text) { }

            public static void Shown([MinLength(5), ShowValueInMessage] string text) { }
        }
        """;

    private static readonly Lazy<Assembly> Woven = new(static () => WeavingDirectory.WeaveAndLoad(Source));

    [Fact]
    public void Invoke_WhenAConstructorRejectsAnArgument_NamesTheConstructor()
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Construct(Woven.Value, Target, 0)
        );

        // Assert
        AssertNamed("Target.Target(int)", exception);
    }

    [Theory]
    [InlineData("Level", "Target.Level.set")]
    [InlineData("Initial", "Target.Initial.init")]
    public void Invoke_WhenASetterRejectsAValue_NamesTheProperty(string property, string expected)
    {
        // Arrange
        object target = WovenCode.Construct(Woven.Value, Target);
        PropertyInfo info = target.GetType().GetProperty(property)!;

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => info.SetValue(target, 0, Unwrapped, binder: null, index: null, culture: null)
        );

        // Assert
        AssertNamed(expected, exception);
    }

    [Fact]
    public void Invoke_WhenAnIndexerRejectsAValue_NamesTheIndexer()
    {
        // Arrange
        object target = WovenCode.Construct(Woven.Value, Target);
        PropertyInfo indexer = target.GetType().GetProperty("Item")!;

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => indexer.SetValue(target, 0, Unwrapped, binder: null, index: [1], culture: null)
        );

        // Assert
        AssertNamed("Target.this[int].set", exception);
    }

    [Fact]
    public void Invoke_WhenAMethodRejectsAnArgument_NamesItsParameterTypesAsCSharpDoes()
    {
        // Arrange
        object?[] arguments = [0, "text", new int[1, 1], (1, "one"), null, new List<int>(), new(), 0];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Many", arguments)
        );

        // Assert
        AssertNamed("Target.Many(int, string, int[,], (int, string), int?, List<int>, object, int)", exception);
    }

    [Fact]
    public void Invoke_WhenAVirtualMethodWithAnInParameterRejectsAnArgument_NamesItsTypeWithoutModifiers()
    {
        // Arrange - a virtual method's in parameter carries a required modifier
        object?[] arguments = [0, 1];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, "Read", arguments)
        );

        // Assert
        AssertNamed("Target.Read(int, int)", exception);
    }

    [Fact]
    public void Invoke_WhenAGenericMethodRejectsAnArgument_NamesItsTypeParameters()
    {
        // Arrange
        MethodInfo generic = Woven.Value.GetType(Target)!.GetMethod("Generic")!.MakeGenericMethod(typeof(string));

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => generic.Invoke(null, Unwrapped, binder: null, [0, "value"], culture: null)
        );

        // Assert
        AssertNamed("Target.Generic<TValue>(int, TValue)", exception);
    }

    [Fact]
    public void Invoke_WhenANestedGenericTypesMethodRejectsAnArgument_NamesEachTypesParameters()
    {
        // Arrange
        Type inner = Woven.Value.GetType("Box`1+Inner`1")!.MakeGenericType(typeof(string), typeof(int));
        MethodInfo put = inner.GetMethod("Put")!;

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => put.Invoke(null, Unwrapped, binder: null, [0, "first", 2], culture: null)
        );

        // Assert
        AssertNamed("Box<T>.Inner<U>.Put(int, T, U)", exception);
    }

    [Theory]
    [InlineData("Hidden", "a string of 3 characters")]
    [InlineData("Shown", "\"abc\"")]
    public void Invoke_WhenAStringIsRejected_ShowsItOnlyIfTheParameterSaysSo(string method, string expected)
    {
        // Arrange - nothing beyond the arguments

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => WovenCode.Invoke(Woven.Value, Target, method, "abc")
        );

        // Assert
        Assert.Contains($"The argument was {expected}.", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertNamed(string method, ArgumentException exception)
        => Assert.StartsWith($"{method} rejected argument ", exception.Message, StringComparison.Ordinal);
}

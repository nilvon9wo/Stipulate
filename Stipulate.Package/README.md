# Stipulate

Makes the `System.ComponentModel.DataAnnotations` validation attributes on method parameters
mean something.

**It enforces them on every call.** With Stipulate referenced, this method rejects an empty array
with an `ArgumentException`, and `null` with an `ArgumentNullException`, whose message names the
method, the attribute and its own message, and what the argument was:

```csharp
public static class Multiplier
{
    public static int[] Multiply([Required, MinLength(1)] int[] array) => array;
}
```

**It checks your contracts at compile time.** An implementation must accept every argument its
interface or base class accepts, and hand back only what it promises:

```csharp
public interface IMultiplier
{
    int[] Multiply([MinLength(1)] int[] array);
}

public class Multiplier : IMultiplier
{
    // error ST1001: [MinLength(2)] requires Length >= 2, but the contract accepts Length >= 1
    public int[] Multiply([MinLength(2)] int[] array) => array;
}
```

**It checks arguments known at compile time.** Passing a constant, `null`, or a collection
written in place that an attribute rejects is an error where it is written:
`Multiplier.Multiply([])` above is error ST3001.

## Getting started

```shell
dotnet add package Stipulate
```

That's all: the analyzer runs in the IDE and the build, and the build weaves the checks into
your assembly. To intercept call sites at compile time instead, or only check contracts, set the
`StipulateMode` MSBuild property to `Interceptors` or `None`. Projects that reference yours get
Stipulate too, and are held to your contracts.

Stipulate is for C# projects. Visual Basic and F# projects build with it, with a warning (ST0008),
but aren't checked; their calls into C# that Stipulate wove are.

Documentation: <https://github.com/nilvon9wo/Stipulate/tree/main/docs>, including every rule,
the enforcement modes and their messages (with `[ShowValueInMessage]` to show a value), and
describing your own validation attributes with `[ComparableValidation]`.

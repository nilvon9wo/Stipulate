# Stipulate

Makes the `System.ComponentModel.DataAnnotations` validation attributes on method parameters
mean something.

**It enforces them on every call.** With Stipulate referenced, this method rejects an empty array
with an `ArgumentException`, and `null` with an `ArgumentNullException`, carrying the attribute's
own message:

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

## Getting started

```shell
dotnet add package Stipulate
```

That's all: the analyzer runs in the IDE and the build, and the build weaves the checks into
your assembly. To intercept call sites at compile time instead, or only check contracts, set the
`StipulateMode` MSBuild property to `Interceptors` or `None`.

Documentation: <https://github.com/nilvon9wo/Stipulate/tree/main/docs>, including every rule,
the enforcement modes, and describing your own validation attributes with `[ComparableValidation]`.

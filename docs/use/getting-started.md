# Getting started

## What it does

Put validation attributes on a method's parameters:

```csharp
public static class Multiplier
{
    public static int[] Multiply([Required, MinLength(1)] int[] array) => array;
}
```

ParameterValidation then does two things.

**It enforces them when the method is called.** `Multiplier.Multiply([])` throws an
`ArgumentException` whose message is the attribute's own ("The field array must be a string or
array type with a minimum length of '1'.") and whose `ParamName` is `array`.
`Multiplier.Multiply(null)` throws an `ArgumentNullException`, because `[Required]` rejects
`null`. How the check gets into your code is up to you: see
[enforcement modes](enforcement-modes.md).

**It checks your contracts at compile time.** An implementation must accept every argument its
interface or base class accepts, so it may not demand more:

```csharp
public interface IMultiplier
{
    int[] Multiply([MinLength(1)] int[] array);
}

public class Multiplier : IMultiplier
{
    // error PV1001: Parameter 'array' of 'Multiplier.Multiply(int[])' strengthens the
    // precondition of 'IMultiplier.Multiply(int[])': [MinLength(2)] requires Length >= 2,
    // but the contract accepts Length >= 1
    public int[] Multiply([MinLength(2)] int[] array) => array;
}
```

It may accept more, but must say so, so that its signature shows what it accepts:

```csharp
public interface IMultiplier
{
    int[] Multiply([MinLength(1)] int[] array);
}

public class Multiplier : IMultiplier
{
    // Accepts an empty array too, on purpose.
    public int[] Multiply([MinLength(0)] int[] array) => array;
}
```

See [contracts](contracts.md) for everything the analyzer checks.

## Setting it up

NuGet packages are planned. Until then, ParameterValidation is wired in by this repository's
`Directory.Build.props` and `Directory.Build.targets`: every project under the repository's
folder (as `ArrayFoo` is) gets

- the analyzer and its code fix, live in the IDE and in the build;
- `ParameterValidation.Contracts`, for [describing custom attributes](custom-attributes.md);
- run-time enforcement, by default by weaving (see [enforcement modes](enforcement-modes.md)).

Requires the .NET 10 SDK. After rebuilding the analyzer or the code fix, restart Visual Studio:
it keeps the analyzer it first loaded.

## Which attributes it understands

The analyzer compares these built-in attributes by what they constrain:
`[Required]`, `[MinLength]`, `[MaxLength]`, `[Length]`, `[StringLength]`, `[Range]`,
`[AllowedValues]` and `[DeniedValues]`. Any other validation attribute is compared only with
identical usages of itself, until you [describe it](custom-attributes.md).

Run-time enforcement works with every `ValidationAttribute`, described or not: it calls the
attribute's own `IsValid`.

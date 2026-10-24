# Getting started

## What it does

Put validation attributes on a method's parameters:

```csharp
public static class Multiplier
{
    public static int[] Multiply([Required, MinLength(1)] int[] array) => array;
}
```

Stipulate then does two things.

**It enforces them when the method is called.** `Multiplier.Multiply([])` throws an
`ArgumentException` whose `ParamName` is `array`, and whose message names the method, the
attribute and what it measured, after the attribute's own message: "Multiplier.Multiply(int[])
rejected argument 'array' by [MinLength]: The field array must be a string or array type with a
minimum length of '1'. The argument was a collection of 0 elements."
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
    // error ST1001: Parameter 'array' of 'Multiplier.Multiply(int[])' strengthens the
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

**It checks arguments known at compile time.** A call that passes a constant, `null`, or a
collection written in place that an attribute rejects can only throw, so it is reported as it is
written ([ST3001](../rules/ST3001.md)):

```csharp
public static class Multiplier
{
    public static int[] Multiply([Required, MinLength(1)] int[] array) => array;
}

public static class Caller
{
    // error ST3001: the argument [] is rejected by [MinLength(1)]: Length is 0
    public static int[] Call() => Multiplier.Multiply([]);
}
```

## Setting it up

Reference the `Stipulate` package:

```shell
dotnet add package Stipulate
```

The project then gets

- the analyzer and its code fix, live in the IDE and in the build;
- `Stipulate.Contracts`, for [describing custom attributes](custom-attributes.md);
- run-time enforcement, by default by weaving (see [enforcement modes](enforcement-modes.md)).

It needs the .NET SDK 9.0.300 or later, or Visual Studio 2022 17.14 or later: the analyzer is
built for that version of the compiler. Weaving works for any target framework, the .NET
Framework and netstandard2.0 included; the weaver itself runs on .NET 8 or later.

A project that gets Stipulate through another, by referencing a library that uses it, gets all
of it too: a class there that implements one of the library's interfaces is held to its
contracts, and its methods are woven. To leave such projects out, see
[enforcement modes](enforcement-modes.md#projects-that-get-stipulate-through-another).

Every project in this repository uses Stipulate the same way, wired to the repository's own
projects instead of the package (see [packaging](../contribute/packaging.md)). After rebuilding
the analyzer or the code fix there, restart Visual Studio: it keeps the analyzer it first loaded.

## Which attributes it understands

The analyzer compares these built-in attributes by what they constrain:
`[Required]`, `[MinLength]`, `[MaxLength]`, `[Length]`, `[StringLength]`, `[Range]`,
`[AllowedValues]` and `[DeniedValues]`. Any other validation attribute is compared only with
identical usages of itself, until you [describe it](custom-attributes.md).

Run-time enforcement works with every `ValidationAttribute`, described or not: it calls the
attribute's own `IsValid`.

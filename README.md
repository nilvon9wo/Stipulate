# Stipulate

[![CI](https://github.com/nilvon9wo/Stipulate/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nilvon9wo/Stipulate/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Stipulate)](https://www.nuget.org/packages/Stipulate)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Make `System.ComponentModel.DataAnnotations` validation attributes on method parameters mean
something: enforce them at run time, and check at compile time that implementations and overrides
honour the attributes of the interfaces and base classes they implement.

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

## Projects

| Project | What it does |
|---|---|
| `Stipulate.Weaver` | Rewrites each compiled assembly's IL so attributed methods validate their own arguments (the default). |
| `Stipulate.Generator` | Alternatively, intercepts call sites at compile time so callers validate the arguments they pass. |
| `Stipulate.Analyzers` | Checks implementations and overrides against their contracts, live in the IDE and in the build. |
| `Stipulate.CodeFixes` | IDE quick fix that restates a contract's missing attribute. |
| `Stipulate.Contracts` | `[ComparableValidation]`, which describes a custom validation attribute so the analyzer can compare it, and `[ShowValueInMessage]`, which shows a parameter's value when a check rejects it. |
| `Stipulate.Package` | Packs all of the above, and the build integration, as the `Stipulate` NuGet package. |

Install it with `dotnet add package Stipulate`; see [getting started](docs/use/getting-started.md).

`ArrayFoo` and `ArrayFooTest` are the sample that inspired the project.

Choose the run-time enforcement with the `StipulateMode` MSBuild property: `Weaving`
(default), `Interceptors` or `None`. See [enforcement modes](docs/use/enforcement-modes.md).

## The rules

An implementation must accept every argument its contract accepts, and hand back only values its
contract promises (the Liskov substitution principle).

| ID | Severity | Meaning |
|---|---|---|
| [ST1001](docs/rules/ST1001.md) | Error | The implementation's attribute rejects something the contract accepts. |
| [ST1002](docs/rules/ST1002.md) | Warning | The implementation doesn't restate one of the contract's attributes. Restate it, or state a weaker constraint for each of its bounds (e.g. `[MinLength(0)]`, `[MaxLength]`). |
| [ST1003](docs/rules/ST1003.md) | Error | The implementation weakens a guarantee: what a getter, `out` or `ref` parameter hands back. |
| [ST2001](docs/rules/ST2001.md) | Error | An attribute declared in this solution can't be compared; describe it with `[ComparableValidation]`. |
| [ST2002](docs/rules/ST2002.md) | Error | An attribute from outside the solution can't be compared; repeat it exactly, or describe it from your assembly. |
| [ST2003](docs/rules/ST2003.md) | Error | A `[ComparableValidation]` description is invalid. |
| [ST3001](docs/rules/ST3001.md) | Error | A value known at compile time, such as a constant, `null` or `[]`, is passed to a parameter whose attribute rejects it. |

Parameters, properties and indexers are checked, for interface implementations (implicit, explicit,
inherited and `static abstract`) and overrides. A property's attributes are preconditions of its
setter and guarantees of its getter, so a get/set property must match its contract. See
[contracts](docs/use/contracts.md), and [custom validation attributes](docs/use/custom-attributes.md)
for describing your own.

## Documentation

[docs/](docs/README.md): using Stipulate, every rule with an example and its fix, and
contributing. Every C# example in it, and in this README, is compiled by a test that checks the
analyzer reports exactly what the example shows, in the words it shows.

## Building

Requires the .NET 10 SDK. `dotnet build` and `dotnet test`; see
[local development](docs/contribute/local-development.md) for every gate CI runs.

## License

[MIT](LICENSE)

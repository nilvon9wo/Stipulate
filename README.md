# ParameterValidation

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
    // error PV1001: Parameter 'array' of 'Multiplier.Multiply(int[])' strengthens the
    // precondition of 'IMultiplier.Multiply(int[])': [MinLength(2)] requires Length >= 2,
    // but the contract accepts Length >= 1
    public int[] Multiply([MinLength(2)] int[] array) => array;
}
```

## Projects

| Project | What it does |
|---|---|
| `ParameterValidation.Weaver` | Rewrites each compiled assembly's IL so attributed methods validate their own arguments (the default). |
| `ParameterValidation.Generator` | Alternatively, intercepts call sites at compile time so callers validate the arguments they pass. |
| `ParameterValidation.Analyzers` | Checks implementations and overrides against their contracts, live in the IDE and in the build. |
| `ParameterValidation.CodeFixes` | IDE quick fix that restates a contract's missing attribute. |
| `ParameterValidation.Contracts` | `[ComparableValidation]`, which describes a custom validation attribute so the analyzer can compare it. |

`ArrayFoo` and `ArrayFooTest` are the sample that inspired the project.

Choose the run-time enforcement with the `ParameterValidationMode` MSBuild property: `Weaving`
(default), `Interceptors` or `None`. See `Directory.Build.props`.

## The rules

An implementation must accept every argument its contract accepts, and hand back only values its
contract promises (the Liskov substitution principle).

| ID | Severity | Meaning |
|---|---|---|
| PV1001 | Error | The implementation's attribute rejects something the contract accepts. |
| PV1002 | Warning | The implementation doesn't restate one of the contract's attributes. Restate it, or state a weaker constraint for each of its bounds (e.g. `[MinLength(0)]`, `[MaxLength]`). |
| PV1003 | Error | The implementation weakens a guarantee: what a getter, `out` or `ref` parameter hands back. |
| PV2001 | Error | An attribute declared in this solution can't be compared; describe it with `[ComparableValidation]`. |
| PV2002 | Error | An attribute from outside the solution can't be compared; repeat it exactly, or describe it from your assembly. |
| PV2003 | Error | A `[ComparableValidation]` description is invalid. |

Parameters, properties and indexers are checked, for interface implementations (implicit, explicit,
inherited and `static abstract`) and overrides. A property's attributes are preconditions of its
setter and guarantees of its getter, so a get/set property must match its contract.

## Building

Requires the .NET 10 SDK. `dotnet build` and `dotnet test`.

## License

[MIT](LICENSE)

# Contracts

An interface method, or a virtual or abstract method, is a contract: its callers rely on what its
attributes say it accepts and what it hands back. The analyzer checks that every implementation
and override honours that contract (the Liskov substitution principle), live in the IDE and in the
build.

## Preconditions: what it accepts

A parameter's attributes are preconditions. An implementation must accept every argument its
contract accepts. It may accept more, never less:

```csharp
public interface IScale
{
    void Set([Range(1, 10)] int level);
}

public class WideScale : IScale
{
    public void Set([Range(0, 100)] int level) { }
}

public class NarrowScale : IScale
{
    // error PV1001: [Range(2, 10)] rejects 1, which the contract accepts
    public void Set([Range(2, 10)] int level) { }
}
```

Dropping an attribute accepts more too, which is allowed but must be explicit, so that the
implementation's signature shows what it accepts. Restate the attribute, or state a weaker
constraint for each of its bounds:

```csharp
public interface IGreeter
{
    void Greet([Required, MinLength(2), MaxLength(20)] string name);
}

public class SilentGreeter : IGreeter
{
    // warning PV1002: doesn't restate [MinLength(2)]
    // warning PV1002: doesn't restate [MaxLength(20)]
    public void Greet([Required] string name) { }
}

public class OpenGreeter : IGreeter
{
    // No lower bound on the length, and no upper one; a nullable parameter accepts null.
    public void Greet([MinLength(0), MaxLength] string? name) { }
}
```

`[Required]` is weakened by making the parameter nullable. The code fix "Restate [...] from the
contract" restates a missing attribute exactly, one at a time or all at once.

## Guarantees: what it hands back

What an implementation hands back must satisfy everything its contract promises. A property's
attributes are guarantees of its getter, and an `out` or `ref` parameter's are guarantees of what
it holds when the method returns. An implementation may promise more, never less:

```csharp
public interface IInventory
{
    [Range(0, 1000)]
    int Count { get; }
}

public class SmallInventory : IInventory
{
    [Range(0, 10)]
    public int Count => 3;
}

public class BottomlessInventory : IInventory
{
    // error PV1003: [Range(0, 2000)] allows 2000, which the contract rules out
    [Range(0, 2000)]
    public int Count => 3;
}
```

A property with both a getter and a setter, and a `ref` parameter, both take and hand back
values, so their attributes must match the contract's exactly.

## What is checked

- Parameters, properties and indexers.
- Interface implementations, whether implicit, explicit, inherited from a base class, or
  `static abstract`; and overrides.
- Every constraint is compared with the contract's constraint on the same *dimension*: `[Length]`,
  `[MinLength]`, `[MaxLength]` and `[StringLength]` all bound the length, and `[Range]`,
  `[AllowedValues]` and `[DeniedValues]` all constrain the value. So `[Length(1, 10)]` can replace
  `[MinLength(1), MaxLength(10)]`:

```csharp
public interface ITitled
{
    void Rename([MinLength(1), MaxLength(10)] string title);
}

public class Book : ITitled
{
    public void Rename([Length(1, 10)] string title) { }
}
```

- `[Range(typeof(T), "minimum", "maximum")]` is compared by parsing its limits as `T`: numbers,
  dates and times, with the culture the attribute declares.
- A list of `[AllowedValues]` may grow; a list of `[DeniedValues]` may shrink.

Any other validation attribute is compared only with identical usages of itself, and reported as
[PV2001](../rules/PV2001.md) or [PV2002](../rules/PV2002.md) otherwise. Describe it with
`[ComparableValidation]` to compare it like the built-in ones: see
[custom validation attributes](custom-attributes.md).

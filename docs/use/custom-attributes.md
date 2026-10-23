# Custom validation attributes

The analyzer knows what the built-in attributes constrain. It can't know what your own
`ValidationAttribute` does: its `IsValid` is code. So until you describe it, it compares a
custom attribute only with identical usages of itself. Is `[AtLeast(0)]` weaker than
`[AtLeast(5)]`? Only the attribute's author knows:

```csharp
public sealed class AtLeastAttribute(int minimum) : ValidationAttribute
{
    public int Minimum => minimum;

    public override bool IsValid(object? value) => value is not int number || number >= minimum;
}

public interface IThermostat { void Set([AtLeast(5)] int degrees); }

public class Thermostat : IThermostat
{
    public void Set([AtLeast(5)] int degrees) { }
}

public class OpenThermostat : IThermostat
{
    // error ST2001: [AtLeast(0)] can't be compared with the attributes of 'IThermostat.Set(int)'
    public void Set([AtLeast(0)] int degrees) { }
}
```

`[ComparableValidation]`, from `Stipulate.Contracts`, describes what an attribute
constrains, so the analyzer can compare it with others.

## Describing your own attribute

Apply `[ComparableValidation]` to the attribute class, once for each thing it constrains:

```csharp
[ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string text || text.Length > 0;
}

public interface ITagger { void Tag([NotEmpty] string tag); }

public class Tagger : ITagger
{
    // Length >= 1 either way.
    public void Tag([MinLength(1)] string tag) { }
}

public class StrictTagger : ITagger
{
    // error ST1001: [MinLength(3)] rejects lengths 1 and 2, which the contract accepts
    public void Tag([MinLength(3)] string tag) { }
}
```

A description has three parts.

**The dimension**: what is constrained. Use one of the `ValidationDimensions` where it means the
same thing, so the attribute can be compared with the built-in ones:

| Dimension | Constrains | Built-in attributes |
|---|---|---|
| `NotNull` | that the value isn't `null` | `[Required]` |
| `NotBlank` | that a string isn't empty or whitespace | `[Required]` |
| `Length` | a string's length or a collection's count | `[MinLength]`, `[MaxLength]`, `[Length]`, `[StringLength]` |
| `Value` | the value itself | `[Range]`, `[AllowedValues]`, `[DeniedValues]` |

Or use a name of your own, such as `"Parity"`: it is only compared with other descriptions on the
same name.

**The comparison**: how the dimension is constrained.

| Comparison | Accepts | Stronger when |
|---|---|---|
| `Flag` | what has the property; the attribute's presence is the constraint | present |
| `LowerBound` | at least the bound (above it, if exclusive) | the bound is higher |
| `UpperBound` | at most the bound (below it, if exclusive) | the bound is lower |
| `AllowedValues` | only the listed values | the list is shorter |
| `DeniedValues` | anything but the listed values | the list is longer |

**Where the bound or the values come from**: `Value` when the class fixes them, as `NotEmpty`
does, or `Argument`, naming the constructor parameter or property each usage sets:

```csharp
[ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Argument = "minimum")]
public sealed class AtLeastAttribute(int minimum) : ValidationAttribute
{
    public int Minimum => minimum;

    public override bool IsValid(object? value) => value is not int number || number >= minimum;
}

public interface IThermostat { void Set([AtLeast(5)] int degrees); }

public class OpenThermostat : IThermostat
{
    // Described, [AtLeast(0)] is known to be weaker than [AtLeast(5)].
    public void Set([AtLeast(0)] int degrees) { }
}

public class BoundedThermostat : IThermostat
{
    // The same lower bound, but [Range(5, 30)] adds an upper one, which the contract doesn't have.
    // error ST1001: [Range(5, 30)] rejects values above 30, which the contract accepts
    public void Set([Range(5, 30)] int degrees) { }
}
```

When both are given, `Value` is the default for a usage that doesn't set `Argument`.

A bound is inclusive unless `IsExclusive = true` says otherwise, or `ExclusiveArgument` names the
`bool` constructor parameter or property each usage sets, as `[Range]`'s `MinimumIsExclusive`
does:

```csharp
[ComparableValidation(
    ValidationDimensions.Value,
    ValidationComparison.UpperBound,
    Argument = "maximum",
    ExclusiveArgument = "IsExclusive")]
public sealed class AtMostAttribute(int maximum) : ValidationAttribute
{
    public int Maximum => maximum;

    public bool IsExclusive { get; set; }

    public override bool IsValid(object? value)
        => value is not int number || (this.IsExclusive ? number < maximum : number <= maximum);
}

public interface ILimiter { void Limit([AtMost(10)] int count); }

public class Limiter : ILimiter
{
    // error ST1001: below 10 rejects 10, which the contract accepts
    public void Limit([AtMost(10, IsExclusive = true)] int count) { }
}
```

An attribute that constrains several things gets one description for each. A `[Required]`-like
attribute that also bounds the length, for example, describes `NotNull` as a `Flag` and `Length`
as a `LowerBound`. Without a `NotNull` description, the analyzer assumes the attribute accepts
`null`, as every built-in attribute but `[Required]` does.

## Describing someone else's attribute

You can't put `[ComparableValidation]` on a class from a package. Describe it from your own
assembly instead, naming the class with `typeof`:

<!-- reference: Vendor -->
```csharp
namespace Vendor;

public sealed class NonEmptyAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string text || text.Length > 0;
}

public sealed class AtLeastAttribute(int minimum) : ValidationAttribute
{
    public int Minimum => minimum;

    public override bool IsValid(object? value) => value is not int number || number >= minimum;
}
```

```csharp
[assembly: ComparableValidation(
    typeof(Vendor.NonEmptyAttribute),
    ValidationDimensions.Length,
    ValidationComparison.LowerBound,
    Value = 1)]

public interface ILabeller { void Label([Vendor.NonEmpty] string label); }

public class Labeller : ILabeller
{
    public void Label([MinLength(1)] string label) { }
}
```

An undescribed attribute from outside the solution is reported as [ST2002](../rules/ST2002.md)
rather than [ST2001](../rules/ST2001.md), because the fix differs: the class isn't yours to
describe directly.

```csharp
public interface IThermostat { void Set([Vendor.AtLeast(5)] int degrees); }

public class OpenThermostat : IThermostat
{
    // error ST2002: [Vendor.AtLeast(0)] can't be compared with the attributes of
    // 'IThermostat.Set(int)', because 'Vendor.AtLeastAttribute' comes from 'Vendor'
    public void Set([Vendor.AtLeast(0)] int degrees) { }
}
```

## What the analyzer trusts

The analyzer believes the description; it doesn't check it against `IsValid`. A description
that says less than the attribute does lets implementations through that shouldn't pass, and
one that says more reports problems that aren't there.

Descriptions aren't inherited: a class derived from a described attribute can change what it
accepts, so it must describe itself. The same goes for classes derived from the built-in
attributes.

A description the analyzer can't use, such as a `LowerBound` with neither a `Value` nor an
`Argument`, is reported as [ST2003](../rules/ST2003.md), and the attribute is treated as
undescribed.

# Enforcement modes

The `StipulateMode` MSBuild property chooses how the validation attributes are enforced
at run time:

| Mode | How | Validates |
|---|---|---|
| `Weaving` (default) | After each project compiles, `Stipulate.Weaver` rewrites its assembly so that every method with validated parameters checks them on entry. | Every call, from anywhere: other assemblies, reflection, delegates. |
| `Interceptors` | `Stipulate.Generator` intercepts each call to such a method at compile time, checking the arguments before forwarding them. | Only calls compiled in a project that uses the generator. |
| `None` | Nothing at run time. | Nothing; the analyzer still checks contracts. |

Set it in the project file, or for every project in a `Directory.Build.props`:

```xml
<PropertyGroup>
  <StipulateMode>Interceptors</StipulateMode>
</PropertyGroup>
```

or for one build:

```shell
dotnet build -p:StipulateMode=Interceptors
```

Changing the mode recompiles the project, so nothing of the previous mode's checks is left behind.

Whatever the mode, a check fails the same way:

- an argument that `[Required]` (or an attribute derived from it) rejects because it is `null`
  throws `ArgumentNullException`;
- any other rejected argument throws `ArgumentException`.

`ParamName` is the parameter's name, and the message is the same in every mode:

```text
Multiplier.Multiply(int[]) rejected argument 'array' by [MinLength]: The field array must be a
string or array type with a minimum length of '1'. The argument was a collection of 0 elements.
(Parameter 'array')
```

It says, in order:

- the method, as C# writes it: `Scale.Set(int)`, `Box<T>.Put(T)`, a constructor as
  `Scale.Scale(int)`, a setter as `Scale.Level.set` (`init` for an init accessor), an indexer's as
  `Scale.this[int].set`. A call through an interface, intercepted, names the interface's method,
  whose attributes it checks;
- the attribute that rejected the argument;
- the attribute's own `FormatErrorMessage(parameterName)`, so `ErrorMessage` and resources work as
  usual;
- what the argument was;
- the parameter, as .NET adds it.

When a parameter has several attributes, they are checked in the order they are declared, and the
first to fail throws.

### What the message shows of the argument

Exception messages end up in logs, telemetry and error responses, so the message shows the
argument itself only where it can't be a secret, and otherwise what the attributes measure of it:

| The argument | Shown as |
|---|---|
| `null` | `null` |
| A number, an enum, a date or a `bool` | Itself, formatted with the invariant culture: `0`, `Monday`, `true` (and a `char` too, on .NET 7 or later) |
| A string | Its length: `an empty string`, `a string of 3 whitespace characters`, `a string of 12 characters` |
| An array or collection | Its number of elements: `a collection of 0 elements` |
| Anything else | Its type: `a value of type Order` |

To show a parameter's value itself, a string as `"ABC-12"` and any other object by its
`ToString()`, mark it with `[ShowValueInMessage]`, from `Stipulate.Contracts`:

```csharp
public static class Catalog
{
    public static void Order([RegularExpression("^[A-Z]{3}-[0-9]{4}$"), ShowValueInMessage] string sku) { }
}
```

`Catalog.Order("ABC-12")` then throws "Catalog.Order(string) rejected argument 'sku' by
[RegularExpression]: The field sku must match the regular expression '^[A-Z]{3}-[0-9]{4}$'. The
argument was "ABC-12"." Mark only parameters whose values are safe to show. The attribute changes
the message, not what the method accepts, so the analyzer doesn't compare it with a contract's.

### What the check adds to a project

The check is source, not a library: Stipulate compiles `Stipulate.ArgumentChecks.cs`, an internal
class in the `Net.NowhereAtAll.Stipulate.Runtime` namespace, into each project whose attributes it
enforces. So enforcing them adds no dependency at run time, and the file compiles as C# 7.3, for
any target framework.

## Weaving

The weaver inserts the checks at the start of each method's body, before its own code: a
constructor's, an instance or static method's, a property setter's. So a method validates its
arguments however it is called.

- **By-reference parameters** (`ref`, `in`, `out`) aren't checked: what they refer to can change
  after the check.
- **Methods without a body** (abstract and interface methods) have nothing to weave; their
  implementations are checked when they are called, against their own attributes. The analyzer
  makes sure those restate the contract's (see [contracts](contracts.md)).
- **`async` methods and iterators** check their arguments when they are called, before they
  return a task or an enumerable: an invalid argument throws at the call, not when the task is
  awaited.
- **Incremental builds**: an assembly is woven once. A build that skips compiling leaves the
  woven assembly in place, and the weaver recognises it ("is already woven").
- **Debugging**: the weaver rewrites the symbols (`.pdb`) along with the assembly. If they don't
  match, it leaves them alone and warns (ST0006).

If weaving fails, the build fails with one of the [build errors](../rules/build-errors.md).

## Interceptors

The generator uses C# interceptors: for each call to a method with validated parameters, it
generates a method with the same signature that validates the arguments and then calls the
original. Calls it can't intercept are left as they are, unvalidated:

| Not intercepted | Why |
|---|---|
| Calls compiled in projects that don't use the generator, reflection, delegates | Only call sites in the compilation are intercepted. |
| Generic methods, methods of generic types, extension methods | The interceptor would have to reproduce their signatures. |
| Methods with `ref`, `in` or `out` parameters, or `ref` returns | As with weaving. |
| Methods the generated code can't call, such as `private` ones | The interceptor lives in a class of its own. |

All calls to one method share one interceptor, and the attributes are constructed once, when it
is first used.

Choose Interceptors when you can't, or don't want to, rewrite assemblies after compiling; choose
Weaving when every call must be validated.

## Projects that get Stipulate through another

Stipulate applies to every project that gets the package, whether it references it or only
references a project or package that does. Each such project has its own `StipulateMode`, and the
analyzer checks its contracts: an implementation of a library's interface can no more strengthen
its preconditions than one in the library itself.

To leave those projects out, set `StipulateTransitive` to `false`, for example for every project
in a `Directory.Build.props`:

```xml
<PropertyGroup>
  <StipulateTransitive>false</StipulateTransitive>
</PropertyGroup>
```

A project that doesn't reference Stipulate itself then gets only `Stipulate.Contracts`, as an
ordinary library: no analyzer, and no enforcement of its own methods. The projects that do
reference it are unaffected: woven, their methods still check their arguments when the others
call them.

# Enforcement modes

The `StipulateMode` MSBuild property chooses how the validation attributes are enforced
at run time:

| Mode | How | Validates |
|---|---|---|
| `Weaving` (default) | After each project compiles, `Stipulate.Weaver` rewrites its assembly so that every method with validated parameters checks them on entry. | Every call, from anywhere: other assemblies, reflection, delegates. |
| `Interceptors` | `Stipulate.Generator` intercepts each call to such a method at compile time, checking the arguments before forwarding them. | Only calls compiled in a project that uses the generator. |
| `None` | Nothing at run time. | Nothing; the analyzer still checks contracts. |

Set it in the project file, for every project in a `Directory.Build.props`, or for one build:

```shell
dotnet build -p:StipulateMode=Interceptors
```

Changing the mode recompiles the project, so nothing of the previous mode's checks is left behind.

Whatever the mode, a check fails the same way:

- an argument that `[Required]` (or an attribute derived from it) rejects because it is `null`
  throws `ArgumentNullException`;
- any other rejected argument throws `ArgumentException`.

Either way, the message is the attribute's own `FormatErrorMessage(parameterName)`, so
`ErrorMessage` and resources work as usual, and `ParamName` is the parameter's name. When a
parameter has several attributes, they are checked in the order they are declared, and the first
to fail throws.

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

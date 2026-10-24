# Other .NET languages

Stipulate is for C#. A Visual Basic or F# project can reference it, directly or through a
library that uses it, and builds as usual, but nothing in it is checked: neither its methods'
arguments at run time nor its implementations against their contracts at compile time.

| | C# | Visual Basic | F# |
|---|---|---|---|
| Contracts checked at compile time (ST1xxx, ST2xxx) and the code fix | Yes | No | No |
| Arguments known at compile time checked (ST3001) | Yes | No | No |
| Its own methods' arguments checked, by weaving | Yes | No | No |
| Its own calls' arguments checked, by interceptors | Yes | No | No |
| Its calls into a C# assembly that Stipulate wove checked | Yes | Yes | Yes |

A Visual Basic or F# project that gets Stipulate is told so by a warning,
[ST0008](../rules/build-errors.md), unless its `StipulateMode` is `None`.

## Why

Stipulate has three parts, and each depends on something different:

- **Weaving** rewrites compiled IL, which is the same whatever the language, and the weaver works
  on a Visual Basic or F# assembly. What stops it is the check the woven code calls,
  `Stipulate.ArgumentChecks.cs` (see [what the check adds](enforcement-modes.md#what-the-check-adds-to-a-project)),
  which is compiled into each project as C# source, so that enforcing the attributes adds no
  dependency at run time. A Visual Basic or F# compiler can't compile it, so these projects
  aren't woven.
- **The analyzer and the code fix** run inside Roslyn, the compiler platform that C# and Visual
  Basic share. They are written against C#'s syntax, and NuGet gives them only to C# projects;
  supporting Visual Basic would mean porting those parts, and testing every rule again in Visual
  Basic. The F# compiler isn't built on Roslyn and doesn't run Roslyn analyzers at all; F# would
  need an analyzer of its own.
- **Interceptors** are a feature of the C# compiler, which Visual Basic and F# don't have.

## Mixing languages

Run-time checks woven into a C# assembly are in its IL, so every caller gets them, whatever its
language. In a solution that mixes languages, declare the interfaces, base classes and methods
whose arguments are validated in C# projects, and call them from the others. Remember that:

- a Visual Basic or F# class that implements a C# interface isn't checked against its contract,
  and the attributes on its own parameters aren't enforced;
- a C# library built in `Interceptors` mode checks only the calls compiled in projects that use
  the generator, so calls from Visual Basic or F# aren't checked (as with any other caller
  outside it; see [interceptors](enforcement-modes.md#interceptors)).

To acknowledge that a project's attributes aren't enforced, and so not be warned, set its
`StipulateMode` to `None`. For every project in a `Directory.Build.props` that isn't C#:

```xml
<PropertyGroup Condition="'$(MSBuildProjectExtension)' != '.csproj'">
  <StipulateMode>None</StipulateMode>
</PropertyGroup>
```

A project that gets Stipulate only through a library isn't warned either if
[`StipulateTransitive`](enforcement-modes.md#projects-that-get-stipulate-through-another) is
`false`.

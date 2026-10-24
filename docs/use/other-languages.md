# Other .NET languages

Stipulate is for C# above all, but it weaves Visual Basic and F# projects too: their methods check
their arguments when they are called, with the same exceptions and the same messages as in C#.
What they don't get is the analyzer.

| | C# | Visual Basic | F# |
|---|---|---|---|
| Its own methods' arguments checked, by weaving | Yes | Yes | Yes, on the calls [`StipulateFSharpChecks`](#f-and-its-optimizer) guarantees |
| Its own calls' arguments checked, by interceptors | Yes | No | No |
| Contracts checked at compile time (ST1xxx, ST2xxx) and the code fix | Yes | No | No |
| Arguments known at compile time checked (ST3001) | Yes | No | No |
| Its calls into an assembly Stipulate wove checked | Yes | Yes | Yes |

A Visual Basic or F# project in `Interceptors` mode isn't enforced at all, and is told so by a
warning, [ST0008](../rules/build-errors.md); so is a project in any other language, unless its
`StipulateMode` is `None`.

## Why

Stipulate has three parts, and each depends on something different:

- **Weaving** rewrites compiled IL, which is the same whatever the language. The check the woven
  code calls is compiled into each project as source, so that enforcing the attributes adds no
  dependency at run time (see [what the check adds](enforcement-modes.md#what-the-check-adds-to-a-project)),
  and so the package has it in each language: `Stipulate.ArgumentChecks.cs`, `.vb` and `.fs`.
  The three copies are tested to behave identically, down to every word of their messages.
- **The analyzer and the code fix** run inside Roslyn, the compiler platform that C# and Visual
  Basic share. They are written against C#'s syntax, and NuGet gives them only to C# projects;
  supporting Visual Basic would mean porting those parts, and testing every rule again in Visual
  Basic. The F# compiler isn't built on Roslyn and doesn't run Roslyn analyzers at all; F# would
  need an analyzer of its own.
- **Interceptors** are a feature of the C# compiler, which Visual Basic and F# don't have.

Messages name methods as C# writes them (`Scale.Set(int)`, not `Scale.Set(Integer)`), whatever
the language they were written in.

## F# and its optimizer

F#'s compiler, unlike C#'s and Visual Basic's, has an optimizer of its own, which copies a small
method's body into the code that calls it, and offers it to other assemblies to do the same. That
happens when the project compiles, before the weaver adds the checks to the method, so a call
that was inlined runs the method's body without them. The `StipulateFSharpChecks` MSBuild
property chooses what to give up:

| `StipulateFSharpChecks` | Calls checked | What it costs |
|---|---|---|
| `EveryCall` (default) | Every call. | F#'s optimizer inlines nothing in the project (the .NET runtime's JIT still optimizes it), and other assemblies can't inline its small methods. |
| `CallsFromOtherAssemblies` | Every call from another assembly, F# or not. Within the project, a call to a small method may be inlined, and isn't checked. | Other assemblies can't inline the project's small methods. |
| `NoInliningMethods` | Only calls to the methods you mark. | Nothing: F#'s optimizer works as usual. |

Whatever the setting, a method marked `[<MethodImpl(MethodImplOptions.NoInlining)>]` is checked
on every call, because F# never inlines it. So a project can keep F#'s optimizer, with
`CallsFromOtherAssemblies` or `NoInliningMethods`, and still be sure of the methods that matter
most:

```fsharp
open System.ComponentModel.DataAnnotations
open System.Runtime.CompilerServices

type Account =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member Withdraw([<Range(1, 1000)>] amount: int) = ()
```

Set it like `StipulateMode`: in the project file, for every F# project in a
`Directory.Build.props`, or only for some builds:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <StipulateFSharpChecks>CallsFromOtherAssemblies</StipulateFSharpChecks>
</PropertyGroup>
```

Changing it recompiles the project. A value that isn't one of the three fails the build
([ST0009](../rules/build-errors.md)).

Two things no setting changes:

- **An F# `inline` member** is never checked when F# code calls it: F# always copies it into its
  callers. C# and Visual Basic code calling it does call the woven method, and is checked.
- **`EveryCall` relies on a compiler switch F# doesn't document**, `--localoptimize-`, with the
  documented `--nooptimizationdata`. If a version of F# drops it, F# rejects it and the build
  fails, rather than leaving calls silently unchecked (see
  [if F# rejects a switch](#if-f-rejects-a-switch)).

### If F# rejects a switch

A version of F# that no longer knows one of the switches fails the build with an error naming it:

```text
FSC : error FS0243: Unrecognized option: '--localoptimize-'. Use '--help' to learn about
recognized command line options.
```

The switches each setting passes are MSBuild properties of their own, so a project needn't wait
for a release of Stipulate to pass others:

| Property | Default |
|---|---|
| `StipulateFSharpEveryCallSwitches` | `--localoptimize- --nooptimizationdata` |
| `StipulateFSharpCallsFromOtherAssembliesSwitches` | `--nooptimizationdata` |

`--optimize-`, a documented switch, turns F#'s optimizer off altogether, so it inlines nothing
either, at the cost of its other optimizations:

```xml
<PropertyGroup>
  <StipulateFSharpEveryCallSwitches>--optimize- --nooptimizationdata</StipulateFSharpEveryCallSwitches>
</PropertyGroup>
```

Stipulate's own tests build with that fallback as well as with the defaults, and a scheduled
build tries the defaults on the newest preview of the .NET SDK, so a switch F# is about to drop
should be noticed before its release. Changing a property recompiles the project, as changing
`StipulateFSharpChecks` does.

## Mixing languages

Run-time checks woven into an assembly are in its IL, so every caller gets them, whatever its
language. Only C# projects get the analyzer, though, so in a solution that mixes languages,
declare the interfaces and base classes whose contracts matter in C# projects:

- a Visual Basic or F# class that implements a C# interface isn't checked against its contract,
  though the attributes on its own parameters are enforced;
- a C# library built in `Interceptors` mode checks only the calls compiled in projects that use
  the generator, so calls from Visual Basic or F# aren't checked (as with any other caller
  outside it; see [interceptors](enforcement-modes.md#interceptors)).

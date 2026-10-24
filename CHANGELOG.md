# Changelog

All notable changes are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Cutting a release is adding a `## [x.y.z] - <date>` heading above the notes it releases: merging
that to `main` publishes the `Stipulate` package at that version (see
[packaging](docs/contribute/packaging.md)).

## [Unreleased]

## [0.2.0-preview.1] - 2026-10-24

### Added

- **Weaving for Visual Basic and F# projects**, with the run-time check in their own language
  (`Stipulate.ArgumentChecks.vb` and `.fs`, beside the `.cs`), throwing the same exceptions with
  the same messages. One set of tests runs against all three copies, and each must be covered in
  full, so they can't drift apart. The analyzer, the code fix and interceptors remain C# only.
- **`StipulateFSharpChecks`**, for F# projects, whose optimizer can inline a small method before
  it is woven: `EveryCall` (the default; F#'s optimizer inlines nothing), `CallsFromOtherAssemblies`
  or `NoInliningMethods`. A method marked `NoInlining` is checked on every call in any setting;
  any other value is error ST0009. The compiler switches each setting passes are properties of
  their own, `StipulateFSharpEveryCallSwitches` and
  `StipulateFSharpCallsFromOtherAssembliesSwitches`, so a project can pass others should a
  version of F# drop one (see
  [if F# rejects a switch](docs/use/other-languages.md#if-f-rejects-a-switch)).

### Fixed

- **Visual Basic and F# projects that get Stipulate**, directly or through a library that uses
  it, no longer fail to build: they were given the C# source of the run-time check to compile.
  A project Stipulate can't enforce anything in, a Visual Basic or F# one in `Interceptors` mode
  or one in another language, is warned (ST0008) unless its `StipulateMode` is `None`. See
  [other .NET languages](docs/use/other-languages.md).

## [0.1.0-preview.1] - 2026-10-24

The first release of **Stipulate**, which makes the DataAnnotations validation attributes on
method parameters mean something.

### Added

- **Run-time enforcement**, chosen with the `StipulateMode` MSBuild property:
  - `Weaving` (default): the weaver rewrites each compiled assembly so every method with
    validated parameters checks them on entry, however it is called. Works for any target
    framework, including netstandard2.0 and the .NET Framework; the weaver itself runs on
    .NET 8 or later.
  - `Interceptors`: a source generator intercepts each call to such a method at compile time.
  - `None`: no enforcement.

  A rejected `null` throws `ArgumentNullException` for `[Required]`; anything else rejected
  throws `ArgumentException`. In every mode, the message names the method, the attribute and the
  parameter, and gives the attribute's own message and what the argument was: a number, an enum
  or a `bool` as it is, a string by its length, a collection by its number of elements.
  `[ShowValueInMessage]` shows a parameter's value itself. The check is compiled into each
  project as source, so enforcement adds no dependency at run time.
- **The contract analyzer**: implementations and overrides may not strengthen their contract's
  preconditions (ST1001) or weaken its guarantees (ST1003), and must restate, or explicitly
  weaken, every attribute they drop (ST1002). Parameters, properties and indexers; interface
  implementations of every kind, and overrides.
- **Every project that gets the package**, directly or through another project or package, is
  checked and enforced; `StipulateTransitive=false` leaves out the projects that don't reference
  it themselves.
- **Argument checks** at compile time (ST3001): a constant, `null`, a collection written where it
  is passed, a `static readonly` field initialized with one of these, the arguments of a `params`
  parameter, or a parameter's default value, that one of the parameter's attributes certainly
  rejects. The message says what will throw, and how to fix it; for a call through an interface
  or a virtual method on an object whose type is certain, whether that type's own check accepts
  the value.
- **A code fix** that restates a missing attribute from the contract, one at a time or all at once.
- **`[ComparableValidation]`**, which describes a custom validation attribute, your own or a
  package's, so the analyzer can compare it with the built-in ones (ST2001-ST2003).
- **Documentation**, in which every C# example is compiled and checked by a test: the diagnostics
  its comments show, and the words they quote from each message.

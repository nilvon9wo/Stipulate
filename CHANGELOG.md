# Changelog

All notable changes are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Cutting a release is adding a `## [x.y.z] - <date>` heading above the notes it releases: merging
that to `main` publishes the `Stipulate` package at that version (see
[packaging](docs/contribute/packaging.md)).

## [Unreleased]

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
  throws `ArgumentException`, both with the attribute's own message and the parameter's name.
- **The contract analyzer**: implementations and overrides may not strengthen their contract's
  preconditions (ST1001) or weaken its guarantees (ST1003), and must restate, or explicitly
  weaken, every attribute they drop (ST1002). Parameters, properties and indexers; interface
  implementations of every kind, and overrides.
- **Every project that gets the package**, directly or through another project or package, is
  checked and enforced; `StipulateTransitive=false` leaves out the projects that don't reference
  it themselves.
- **Argument checks** at compile time (ST3001): a constant, `null`, a collection written where it
  is passed, the arguments of a `params` parameter, or a parameter's default value, that one of
  the parameter's attributes certainly rejects. The message says what will throw, and how to fix it.
- **A code fix** that restates a missing attribute from the contract, one at a time or all at once.
- **`[ComparableValidation]`**, which describes a custom validation attribute, your own or a
  package's, so the analyzer can compare it with the built-in ones (ST2001-ST2003).
- **Documentation**, in which every C# example is compiled and checked by a test.

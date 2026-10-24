# Stipulate documentation

Stipulate makes the `System.ComponentModel.DataAnnotations` validation attributes on
method parameters mean something: it enforces them when the method is called, and checks at
compile time that implementations and overrides honour the attributes of the contracts they
implement.

## Using it

- [Getting started](use/getting-started.md): what it does, and a first example.
- [Enforcement modes](use/enforcement-modes.md): Weaving, Interceptors or None, and what each
  one validates.
- [Contracts](use/contracts.md): what the analyzer checks, and how to satisfy it.
- [Custom validation attributes](use/custom-attributes.md): describing your own attributes, or
  someone else's, with `[ComparableValidation]` so the analyzer can compare them.

## Reference

- [Rules](rules/README.md): every diagnostic, with an example and its fix.

## Contributing

- [Contributing](contribute/README.md): coding standards, coverage, local development, CI.

Every C# example in these pages is compiled by a test, which checks that the analyzer reports
exactly the diagnostics the example's comments show (see
[test suites](contribute/test-suites.md#documentation-examples)).

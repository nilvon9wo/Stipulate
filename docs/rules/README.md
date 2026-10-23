# Rules

The analyzer's diagnostics. Each one's help link opens its page here.

| ID | Severity | Meaning |
|---|---|---|
| [ST1001](ST1001.md) | Error | An implementation's attribute rejects something its contract accepts. |
| [ST1002](ST1002.md) | Warning | An implementation doesn't restate one of its contract's attributes. |
| [ST1003](ST1003.md) | Error | An implementation weakens a guarantee: what a getter, `out` or `ref` parameter hands back. |
| [ST2001](ST2001.md) | Error | An attribute declared in this solution can't be compared; describe it. |
| [ST2002](ST2002.md) | Error | An attribute from outside the solution can't be compared; repeat it, or describe it from your assembly. |
| [ST2003](ST2003.md) | Error | A `[ComparableValidation]` description is invalid. |

The ST1xxx rules are about contracts (see [contracts](../use/contracts.md)); the ST2xxx rules
are about attributes the analyzer can't compare (see
[custom validation attributes](../use/custom-attributes.md)).

Weaving, and the build that runs it, report their own errors, ST0001 to ST0006: see
[build errors](build-errors.md).

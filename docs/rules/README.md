# Rules

The analyzer's diagnostics. Each one's help link opens its page here.

| ID | Severity | Meaning |
|---|---|---|
| [PV1001](PV1001.md) | Error | An implementation's attribute rejects something its contract accepts. |
| [PV1002](PV1002.md) | Warning | An implementation doesn't restate one of its contract's attributes. |
| [PV1003](PV1003.md) | Error | An implementation weakens a guarantee: what a getter, `out` or `ref` parameter hands back. |
| [PV2001](PV2001.md) | Error | An attribute declared in this solution can't be compared; describe it. |
| [PV2002](PV2002.md) | Error | An attribute from outside the solution can't be compared; repeat it, or describe it from your assembly. |
| [PV2003](PV2003.md) | Error | A `[ComparableValidation]` description is invalid. |

The PV1xxx rules are about contracts (see [contracts](../use/contracts.md)); the PV2xxx rules
are about attributes the analyzer can't compare (see
[custom validation attributes](../use/custom-attributes.md)).

Weaving, and the build that runs it, report their own errors, PV0001 to PV0006: see
[build errors](build-errors.md).

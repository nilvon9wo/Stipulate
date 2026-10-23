; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PV1001 | ParameterValidation | Error | An implementation strengthens its contract's precondition
PV1002 | ParameterValidation | Warning | An implementation doesn't restate one of its contract's validation attributes
PV1003 | ParameterValidation | Error | An implementation weakens its contract's guarantee
PV2001 | ParameterValidation | Error | A validation attribute declared in this solution can't be compared with the contract
PV2002 | ParameterValidation | Error | A validation attribute from outside this solution can't be compared with the contract
PV2003 | ParameterValidation | Error | An invalid [ComparableValidation] description

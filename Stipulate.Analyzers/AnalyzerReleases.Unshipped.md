; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ST1001 | Stipulate | Error | An implementation strengthens its contract's precondition
ST1002 | Stipulate | Warning | An implementation doesn't restate one of its contract's validation attributes
ST1003 | Stipulate | Error | An implementation weakens its contract's guarantee
ST2001 | Stipulate | Error | A validation attribute declared in this solution can't be compared with the contract
ST2002 | Stipulate | Error | A validation attribute from outside this solution can't be compared with the contract
ST2003 | Stipulate | Error | An invalid [ComparableValidation] description

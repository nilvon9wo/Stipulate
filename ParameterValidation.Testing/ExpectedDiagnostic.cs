using Microsoft.CodeAnalysis.Text;

namespace ParameterValidation.Testing;

/// <summary>A diagnostic a test expects: its ID, and the span of source it is reported on.</summary>
public sealed record ExpectedDiagnostic(string Id, TextSpan Span);

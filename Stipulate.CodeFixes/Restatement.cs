using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.CodeFixes;

/// <summary>A contract's attribute to restate, and the parameter or property declaration to restate it on.</summary>
internal sealed record Restatement(SyntaxNode Declaration, AttributeData Attribute);

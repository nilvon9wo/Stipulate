using Microsoft.CodeAnalysis;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>A contract member, and the member that implements it, if any.</summary>
internal sealed record ContractImplementation(ISymbol Contract, ISymbol? Implementation);

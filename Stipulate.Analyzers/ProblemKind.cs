namespace Net.NowhereAtAll.Stipulate.Analyzers;

internal enum ProblemKind
{
    /// <summary>The implementation's attribute rejects something the contract accepts.</summary>
    Strengthened,

    /// <summary>The implementation's attribute can't be compared with the contract.</summary>
    NotComparable,

    /// <summary>The implementation doesn't restate the contract's attribute.</summary>
    Dropped,

    /// <summary>The contract's attribute promises something the implementation's attributes don't.</summary>
    Weakened,
}

namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>
/// What a constraint makes a statement about: a lower bound, an upper bound, a flag, or which
/// values are allowed, of one dimension. An implementation that makes a statement about everything
/// a contract's attribute constrains has dropped that attribute deliberately.
/// </summary>
internal readonly record struct Statement(string Dimension, ConstraintKind Kind);

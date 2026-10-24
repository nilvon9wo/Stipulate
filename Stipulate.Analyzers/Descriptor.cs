using System.Collections.Immutable;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// How an attribute class constrains one dimension, before any particular usage's arguments are
/// known: built in for the DataAnnotations attributes, or read from a <c>[ComparableValidation]</c>.
/// </summary>
/// <param name="Value">
/// The bound (<see cref="double"/>) or values (<see cref="ImmutableArray{T}"/> of string) to use
/// when <paramref name="Argument"/> isn't supplied.
/// </param>
/// <param name="UnlessArgument">For a flag: the <see cref="bool"/> argument that, when true, removes it.</param>
internal sealed record Descriptor(
    string Dimension,
    ConstraintKind Kind,
    string? Argument = null,
    object? Value = null,
    bool IsExclusive = false,
    string? ExclusiveArgument = null,
    string? UnlessArgument = null
);

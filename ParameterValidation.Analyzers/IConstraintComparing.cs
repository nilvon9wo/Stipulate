namespace ParameterValidation.Analyzers;

/// <summary>How the constraints of one <see cref="ConstraintKind"/> compare with each other.</summary>
internal interface IConstraintComparing
{
    /// <summary>Whether the kind's constraints are numeric bounds, which can be exclusive.</summary>
    bool IsBound { get; }

    /// <summary>The kind of <see cref="Statement"/> a constraint of this kind makes.</summary>
    ConstraintKind StatementKind { get; }

    /// <summary>Whether <paramref name="constraint"/> rejects nothing.</summary>
    bool IsVacuous(Constraint constraint);

    /// <returns>
    /// What <paramref name="constraint"/> rejects that <paramref name="other"/> accepts, or
    /// <c>null</c> if nothing.
    /// </returns>
    string? FindStrengthening(Constraint constraint, Comparand other);
}

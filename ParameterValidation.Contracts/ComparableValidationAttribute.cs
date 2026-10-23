using System;

namespace ParameterValidation.Contracts;

/// <summary>
/// Describes one way in which a <c>System.ComponentModel.DataAnnotations.ValidationAttribute</c>
/// restricts the values it accepts, so that the ParameterValidation analyzer can tell whether an
/// implementation's attributes accept everything its contract's attributes accept.
/// </summary>
/// <remarks>
/// <para>
/// Apply it to your own validation attribute class, once per constraint. For a validation
/// attribute you can't change (for example, one from a package), apply it to your assembly
/// instead, using the constructor that takes the attribute's type.
/// </para>
/// <para>
/// The analyzer trusts the description; it doesn't check it against the attribute's
/// <c>IsValid</c>. A description is assumed to accept <c>null</c> unless it also declares
/// <see cref="ValidationDimensions.NotNull"/>, as the built-in attributes other than
/// <c>RequiredAttribute</c> do.
/// </para>
/// <para>
/// Descriptions aren't inherited: a class derived from a described attribute can change what it
/// accepts, so it must describe itself.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]
/// public sealed class NotEmptyAttribute : ValidationAttribute { ... }
///
/// [ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Argument = "minimum")]
/// public sealed class AtLeastAttribute(int minimum) : ValidationAttribute { ... }
///
/// [assembly: ComparableValidation(
///     typeof(Vendor.NonEmptyAttribute),
///     ValidationDimensions.Length,
///     ValidationComparison.LowerBound,
///     Value = 1)]
/// </code>
/// </example>
/// <remarks>Describes the validation attribute class this is applied to.</remarks>
/// <param name="dimension">
/// What is constrained; one of <see cref="ValidationDimensions"/>, or a name of your own.
/// Constraints are only compared with constraints on the same dimension.
/// </param>
/// <param name="comparison">How the dimension is constrained.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class ComparableValidationAttribute(string dimension, ValidationComparison comparison) : Attribute
{

    /// <summary>Describes <paramref name="attributeType"/>; apply this form to an assembly.</summary>
    /// <param name="attributeType">The validation attribute class being described.</param>
    /// <param name="dimension">
    /// What is constrained; one of <see cref="ValidationDimensions"/>, or a name of your own.
    /// Constraints are only compared with constraints on the same dimension.
    /// </param>
    /// <param name="comparison">How the dimension is constrained.</param>
    public ComparableValidationAttribute(Type attributeType, string dimension, ValidationComparison comparison)
        : this(dimension, comparison) => this.AttributeType = attributeType;

    /// <summary>The described attribute class, when applied to an assembly.</summary>
    public Type? AttributeType { get; }

    public string Dimension { get; } = dimension;

    public ValidationComparison Comparison { get; } = comparison;

    /// <summary>
    /// The name of the described attribute's constructor parameter, or of its property set as a
    /// named argument, that holds the bound or the list of values.
    /// </summary>
    /// <remarks>
    /// Where a usage of the attribute doesn't supply this argument, <see cref="Value"/> is used
    /// instead; if there is no <see cref="Value"/> either, that usage can't be compared.
    /// </remarks>
    public string? Argument { get; set; }

    /// <summary>
    /// The bound (a number) or the list of values (an array) when it is fixed by the attribute
    /// class, or the default when <see cref="Argument"/> isn't supplied.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>Whether the bound itself is excluded, when that is fixed by the attribute class.</summary>
    public bool IsExclusive { get; set; }

    /// <summary>
    /// The name of the described attribute's <see cref="bool"/> constructor parameter or property
    /// that says whether the bound itself is excluded. Overrides <see cref="IsExclusive"/> where a
    /// usage supplies it.
    /// </summary>
    public string? ExclusiveArgument { get; set; }
}

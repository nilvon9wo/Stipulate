namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// Builds an interface, <c>IContract</c>, with one member, and a class, <c>Implementation</c>, that
/// implements it, each with the attributes (and markup) a test gives.
/// </summary>
internal static class ContractSource
{
    /// <summary>The implementation's parameter, for marking up a diagnostic reported on it.</summary>
    public const string Parameter = "value";

    /// <summary>A method <c>Accept</c> with one parameter.</summary>
    /// <param name="contractAttributes">The contract's attributes, without brackets; or none.</param>
    /// <param name="implementationAttributes">The implementation's attributes, or their markup.</param>
    /// <param name="type">The parameter's type.</param>
    /// <param name="implementationParameter">The implementation's parameter name, or its markup.</param>
    public static string ForParameter(
        string contractAttributes,
        string implementationAttributes,
        string type,
        string implementationParameter = Parameter
    )
        => $$"""
            public interface IContract { void Accept({{Attributes(contractAttributes)}}{{type}} value); }
            public class Implementation : IContract
            {
                public void Accept({{Attributes(implementationAttributes)}}{{type}} {{implementationParameter}}) { }
            }
            """;

    /// <summary>A property <c>Value</c> with the given accessors, e.g. <c>"get;"</c> or <c>"get; set;"</c>.</summary>
    /// <param name="contractAttributes">The contract's attributes, without brackets; or none.</param>
    /// <param name="contractAccessors">The contract's accessors.</param>
    /// <param name="implementationAttributes">The implementation's attributes, or their markup.</param>
    /// <param name="implementationAccessors">The implementation's accessors.</param>
    /// <param name="type">The property's type.</param>
    /// <param name="implementationProperty">The implementation's property name, or its markup.</param>
    public static string ForProperty(
        string contractAttributes,
        string contractAccessors,
        string implementationAttributes,
        string implementationAccessors,
        string type = "int",
        string implementationProperty = "Value"
    )
        => $$"""
            public interface IContract { {{Attributes(contractAttributes)}}{{type}} Value { {{contractAccessors}} } }
            public class Implementation : IContract
            {
                {{Attributes(implementationAttributes)}}public {{type}} {{implementationProperty}}
                {
                    {{implementationAccessors}}
                }
            }
            """;

    private static string Attributes(string attributes)
        => attributes.Length == 0
            ? string.Empty
            : $"[{attributes}] ";
}

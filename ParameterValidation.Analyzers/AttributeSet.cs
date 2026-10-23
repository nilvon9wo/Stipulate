using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ParameterValidation.Analyzers;

/// <summary>The validation attributes on one side of a comparison: a contract's, or an implementation's.</summary>
internal sealed class AttributeSet(ImmutableArray<DescribedAttribute> attributes)
{
    private readonly HashSet<string> _keys = [.. attributes.Select(static attribute => attribute.Key)];

    public ImmutableArray<DescribedAttribute> Attributes { get; } = attributes;

    /// <summary>The constraints of every attribute that can be compared.</summary>
    public List<Constraint> Constraints { get; } =
        [.. attributes.SelectMany(static attribute => attribute.Constraints ?? [])];

    /// <summary>Whether <paramref name="attribute"/> is here exactly: the same class with the same arguments.</summary>
    public bool Restates(DescribedAttribute attribute) => this._keys.Contains(attribute.Key);

    /// <summary>Whether an attribute of <paramref name="attribute"/>'s class is here, whatever its arguments.</summary>
    public bool HasClassOf(DescribedAttribute attribute)
        => this.Attributes.Any(attribute.IsSameClassAs);

    /// <param name="side">How the messages name this side: "the contract" or "the implementation".</param>
    public Comparand AsComparand(string side) => new(this.Constraints, side);
}

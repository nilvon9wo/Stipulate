namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>The result of reading one <c>[ComparableValidation]</c>: a descriptor, or why there is none.</summary>
/// <param name="Error">Why the description is invalid, phrased to follow "it is invalid because".</param>
internal sealed record DescriptorReading(Descriptor? Descriptor, string? Error)
{
    public static DescriptorReading Valid(Descriptor descriptor) => new(descriptor, null);

    public static DescriptorReading Invalid(string error) => new(null, error);
}

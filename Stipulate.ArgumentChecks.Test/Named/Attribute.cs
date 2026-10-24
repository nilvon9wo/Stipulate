using System.ComponentModel.DataAnnotations;

namespace Net.NowhereAtAll.Stipulate.ArgumentChecks.Test.Named;

/// <summary>A validation attribute named "Attribute" and nothing more, which has no name to shorten to.</summary>
public sealed class Attribute : ValidationAttribute
{
    public override bool IsValid(object? value) => false;
}

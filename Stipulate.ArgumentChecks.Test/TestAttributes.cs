using System.ComponentModel.DataAnnotations;

namespace Net.NowhereAtAll.Stipulate.ArgumentChecks.Test;

/// <summary>Rejects everything, with the attribute's usual message.</summary>
public sealed class RejectAllAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => false;
}

/// <summary>A validation attribute whose name doesn't end in "Attribute".</summary>
public sealed class Strict : ValidationAttribute
{
    public override bool IsValid(object? value) => false;
}

/// <summary>A value whose text is null, not even empty.</summary>
public sealed class Unprintable
{
    public override string ToString() => null!;
}

/// <summary>Rejects everything, with no message at all.</summary>
public sealed class SilentAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => false;

    public override string FormatErrorMessage(string name) => null!;
}

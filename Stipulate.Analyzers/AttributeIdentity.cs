using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>How an attribute usage is identified and shown.</summary>
internal static class AttributeIdentity
{
    private const string AttributeSuffix = "Attribute";
    private const string ArgumentSeparator = ", ";

    /// <summary>Equal for two usages of the same attribute class with the same arguments.</summary>
    public static string GetKey(AttributeData attribute)
    {
        IEnumerable<string> named = attribute.NamedArguments
            .OrderBy(static named => named.Key, StringComparer.Ordinal)
            .Select(FormatNamed);
        string arguments = string.Join(ArgumentSeparator, FormatConstructorArguments(attribute).Concat(named));
        return $"{attribute.AttributeClass!.ToDisplayString()}({arguments})";
    }

    /// <summary>The usage as written, or as C# would write it if it comes from metadata.</summary>
    public static string GetDisplay(AttributeData attribute, CancellationToken cancellationToken)
        => attribute.ApplicationSyntaxReference is { } reference
            ? reference.GetSyntax(cancellationToken).ToString()
            : Reconstruct(attribute);

    private static string Reconstruct(AttributeData attribute)
    {
        string name = TrimSuffix(attribute.AttributeClass!.Name);
        IEnumerable<string> named = attribute.NamedArguments.Select(FormatNamed);
        string[] arguments = [.. FormatConstructorArguments(attribute), .. named];
        return arguments.Length == 0
            ? name
            : $"{name}({string.Join(ArgumentSeparator, arguments)})";
    }

    private static IEnumerable<string> FormatConstructorArguments(AttributeData attribute)
        => attribute.ConstructorArguments.Select(static argument => argument.ToCSharpString());

    private static string FormatNamed(KeyValuePair<string, TypedConstant> named)
        => $"{named.Key} = {named.Value.ToCSharpString()}";

    private static string TrimSuffix(string name)
        => name.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? name[..^AttributeSuffix.Length]
            : name;
}

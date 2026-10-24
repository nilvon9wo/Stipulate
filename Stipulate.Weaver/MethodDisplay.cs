using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// A method as C# writes it in a message, without namespaces: <c>Scale.Set(int)</c>,
/// <c>Box&lt;T&gt;.Put(T)</c>, <c>Target.Target(int)</c>, <c>Target.Size.set</c>. The interceptors
/// show the methods they check the same way (with Roslyn), so a message doesn't depend on the mode.
/// </summary>
internal static class MethodDisplay
{
    private const string ConstructorName = ".ctor";
    private const char ArityMarker = '`';
    private const string Separator = ", ";

    private static readonly Dictionary<string, string> Keywords = new()
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Decimal"] = "decimal",
        ["System.Double"] = "double",
        ["System.Single"] = "float",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.IntPtr"] = "nint",
        ["System.UIntPtr"] = "nuint",
        ["System.Object"] = "object",
        ["System.String"] = "string",
        ["System.Void"] = "void",
    };

    public static string Display(MethodDefinition method)
    {
        string type = Display(method.DeclaringType);
        PropertyDefinition? property = method.IsSetter
            ? method.DeclaringType.Properties.FirstOrDefault(candidate => candidate.SetMethod == method)
            : null;
        return property is null
            ? $"{type}.{Name(method)}({Parameters(method.Parameters)})"
            : $"{type}.{Name(property)}.{Accessor(method)}";
    }

    private static string Name(MethodDefinition method)
        => method.Name == ConstructorName
            ? WithoutArity(method.DeclaringType.Name)
            : method.Name + TypeParameters(method.GenericParameters.Select(static parameter => parameter.Name));

    /// <summary>An indexer's setter has the indexer's parameters, then the value.</summary>
    private static string Name(PropertyDefinition property)
        => property.HasParameters
            ? $"this[{Parameters(property.Parameters)}]"
            : property.Name;

    /// <summary>C# marks an init accessor, and only that, by a required modifier on its return type.</summary>
    private static string Accessor(MethodDefinition setter)
        => setter.ReturnType is RequiredModifierType
            ? "init"
            : "set";

    private static string Parameters(IEnumerable<ParameterDefinition> parameters)
        => string.Join(Separator, parameters.Select(static parameter => Display(parameter.ParameterType)));

    private static string Display(TypeReference type) => type switch
    {
        ByReferenceType reference => Display(reference.ElementType),
        IModifierType modified => Display(modified.ElementType),
        ArrayType array => $"{Display(array.ElementType)}[{new string(',', array.Rank - 1)}]",
        GenericParameter parameter => parameter.Name,
        GenericInstanceType instance => Display(instance),
        _ => Keywords.GetValueOrDefault(type.FullName) ?? Named(type, [.. type.GenericParameters.Select(Display)]),
    };

    private static string Display(GenericInstanceType instance)
    {
        List<string> arguments = [.. instance.GenericArguments.Select(Display)];
        return instance.ElementType.FullName switch
        {
            "System.Nullable`1" => arguments[0] + "?",
            { } name when name.StartsWith("System.ValueTuple`", StringComparison.Ordinal)
                => $"({string.Join(Separator, arguments)})",
            _ => Named(instance.ElementType, arguments),
        };
    }

    /// <summary>
    /// A type and the types it is nested in, outermost first, each with its own share of the type
    /// arguments: a nested type's name counts only the parameters it adds to its container's.
    /// </summary>
    private static string Named(TypeReference type, List<string> arguments)
    {
        List<TypeReference> chain = [];
        for (TypeReference? current = type; current is not null; current = current.DeclaringType)
        {
            chain.Insert(0, current);
        }

        int used = 0;
        List<string> names = [];
        foreach (TypeReference link in chain)
        {
            int arity = Arity(link.Name);
            names.Add(WithoutArity(link.Name) + TypeParameters(arguments.Skip(used).Take(arity)));
            used += arity;
        }

        return string.Join('.', names);
    }

    private static string TypeParameters(IEnumerable<string> names)
    {
        string joined = string.Join(Separator, names);
        return joined.Length == 0
            ? string.Empty
            : $"<{joined}>";
    }

    private static int Arity(string name)
    {
        int marker = name.IndexOf(ArityMarker, StringComparison.Ordinal);
        return marker < 0
            ? 0
            : int.Parse(name[(marker + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string WithoutArity(string name)
    {
        int marker = name.IndexOf(ArityMarker, StringComparison.Ordinal);
        return marker < 0
            ? name
            : name[..marker];
    }
}

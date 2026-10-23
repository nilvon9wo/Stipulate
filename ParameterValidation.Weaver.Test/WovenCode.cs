using System.Reflection;

namespace Net.NowhereAtAll.ParameterValidation.Weaver.Test;

/// <summary>Calls into a woven assembly, letting the exceptions its checks throw escape unwrapped.</summary>
internal static class WovenCode
{
    private const BindingFlags Unwrapped = BindingFlags.DoNotWrapExceptions;

    /// <summary>
    /// Calls a public method; an instance method on a new instance from the parameterless constructor.
    /// </summary>
    public static object? Invoke(Assembly assembly, string typeName, string methodName, params object?[] arguments)
    {
        Type type = GetType(assembly, typeName);
        MethodInfo method = type.GetMethod(methodName)!;
        object? target = method.IsStatic
            ? null
            : Construct(assembly, typeName);
        return method.Invoke(target, Unwrapped, binder: null, arguments, culture: null);
    }

    /// <summary>Calls the public constructor that takes exactly <paramref name="arguments"/>' types.</summary>
    public static object Construct(Assembly assembly, string typeName, params object[] arguments)
    {
        Type[] argumentTypes = [.. arguments.Select(static argument => argument.GetType())];
        ConstructorInfo constructor = GetType(assembly, typeName).GetConstructor(argumentTypes)!;
        return constructor.Invoke(Unwrapped, binder: null, arguments, culture: null);
    }

    /// <summary>A value of an enum the woven assembly declares.</summary>
    public static object EnumValue(Assembly assembly, string typeName, string name)
        => Enum.Parse(GetType(assembly, typeName), name);

    private static Type GetType(Assembly assembly, string typeName) => assembly.GetType(typeName, throwOnError: true)!;
}

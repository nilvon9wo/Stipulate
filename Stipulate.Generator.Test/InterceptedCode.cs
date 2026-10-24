using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Generator.Test;

/// <summary>Calls into an intercepted assembly, letting the exceptions its checks throw escape unwrapped.</summary>
internal static class InterceptedCode
{
    private const BindingFlags Unwrapped = BindingFlags.DoNotWrapExceptions;

    /// <summary>Calls a public static method, typically a caller whose call sites were intercepted.</summary>
    public static object? Invoke(Assembly assembly, string typeName, string methodName, params object?[] arguments)
    {
        MethodInfo method = assembly.GetType(typeName, throwOnError: true)!.GetMethod(methodName)!;
        return method.Invoke(null, Unwrapped, binder: null, arguments, culture: null);
    }
}

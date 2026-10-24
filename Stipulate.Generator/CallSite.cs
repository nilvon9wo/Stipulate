namespace Net.NowhereAtAll.Stipulate.Generator;

/// <summary>One call to a method with validated parameters, and what intercepting it takes.</summary>
/// <param name="MethodId">The same for every call to the same method, which share one interceptor.</param>
/// <param name="MethodDisplay">The method as the messages name it: <c>Scale.Set(int)</c>.</param>
/// <param name="Signature">The interceptor's signature, with <c>{0}</c> for its name.</param>
/// <param name="ForwardingCall">The call to the original method, with the interceptor's parameters.</param>
/// <param name="InterceptsLocationAttribute">The <c>[InterceptsLocation]</c> pointing at this call.</param>
internal sealed record CallSite(
    string MethodId,
    string MethodDisplay,
    string Signature,
    string ForwardingCall,
    bool ReturnsVoid,
    EquatableArray<ParameterCheck> Checks,
    string InterceptsLocationAttribute
);

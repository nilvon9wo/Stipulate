namespace ParameterValidation.Generator;

/// <summary>One call to a method with validated parameters, and what intercepting it takes.</summary>
/// <param name="MethodId">The same for every call to the same method, which share one interceptor.</param>
/// <param name="Signature">The interceptor's signature, with <c>{0}</c> for its name.</param>
/// <param name="ForwardingCall">The call to the original method, with the interceptor's parameters.</param>
/// <param name="InterceptsLocationAttribute">The <c>[InterceptsLocation]</c> pointing at this call.</param>
internal sealed record CallSite(
    string MethodId,
    string Signature,
    string ForwardingCall,
    bool ReturnsVoid,
    EquatableArray<ParameterCheck> Checks,
    string InterceptsLocationAttribute
);

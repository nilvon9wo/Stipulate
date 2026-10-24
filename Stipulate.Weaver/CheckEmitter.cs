using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// Emits the IL equivalent of the following for one validation attribute on one parameter:
/// <code>
/// ArgumentChecks.Check(new MinLengthAttribute(1), array, "array", "Multiplier.Multiply(int[])", false);
/// </code>
/// <c>ArgumentChecks</c>, compiled into the assembly by Stipulate.targets, throws if the attribute
/// rejects the value. The last argument is whether the parameter has <c>[ShowValueInMessage]</c>.
/// </summary>
/// <param name="method">The method being woven, as its messages name it; see <see cref="MethodDisplay"/>.</param>
internal sealed class CheckEmitter(
    WellKnownReferences references,
    AttributeConstructionEmitter constructions,
    InstructionBuffer buffer,
    string method
)
{
    private readonly WellKnownReferences _references = references;
    private readonly AttributeConstructionEmitter _constructions = constructions;
    private readonly InstructionBuffer _buffer = buffer;
    private readonly string _method = method;

    public void Emit(WovenCheck check)
    {
        ParameterDefinition parameter = check.Parameter;
        this._constructions.Emit(check.Attribute, check.AttributeType);
        this.EmitLoadArgumentAsObject(parameter);
        this._buffer.Emit(OpCodes.Ldstr, parameter.Name);
        this._buffer.Emit(OpCodes.Ldstr, this._method);
        this._buffer.Emit(OpCodes.Ldc_I4, Convert.ToInt32(ShowsValue(parameter)));
        this._buffer.Emit(OpCodes.Call, this._references.Check);
    }

    private static bool ShowsValue(ParameterDefinition parameter)
        => parameter.CustomAttributes.Any(
            static attribute => attribute.AttributeType.FullName == WellKnownReferences.ShowValueInMessageAttributeName
        );

    /// <remarks>
    /// Every primitive is a value type; a generic parameter may be one, and boxing a reference is a no-op.
    /// </remarks>
    private void EmitLoadArgumentAsObject(ParameterDefinition parameter)
    {
        this._buffer.Emit(OpCodes.Ldarg, parameter);
        TypeReference type = parameter.ParameterType;
        if (type.IsValueType || type.IsGenericParameter)
        {
            this._buffer.Emit(OpCodes.Box, type);
        }
    }
}

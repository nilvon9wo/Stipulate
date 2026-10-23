using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ParameterValidation.Weaver;

/// <summary>
/// Emits the IL equivalent of the following for one validation attribute on one parameter:
/// <code>
/// ValidationAttribute attribute = new MinLengthAttribute(1);
/// if (!attribute.IsValid(array))
/// {
///     if (array is null &amp;&amp; attribute is RequiredAttribute)   // only emitted for [Required]
///         throw new ArgumentNullException("array", attribute.FormatErrorMessage("array"));
///     throw new ArgumentException(attribute.FormatErrorMessage("array"), "array");
/// }
/// </code>
/// </summary>
/// <param name="attributeLocal">The method's local that holds each attribute while it is checked.</param>
internal sealed class CheckEmitter(
    WellKnownReferences references,
    AttributeConstructionEmitter constructions,
    InstructionBuffer buffer,
    VariableDefinition attributeLocal
)
{
    private readonly WellKnownReferences _references = references;
    private readonly AttributeConstructionEmitter _constructions = constructions;
    private readonly InstructionBuffer _buffer = buffer;
    private readonly VariableDefinition _attributeLocal = attributeLocal;

    public void Emit(WovenCheck check)
    {
        Instruction next = Instruction.Create(OpCodes.Nop);
        Instruction throwArgumentException = Instruction.Create(OpCodes.Ldloc, this._attributeLocal);
        this.EmitValidity(check, next);
        if (ValidationAttributeResolver.IsRequiredAttribute(check.AttributeType))
        {
            this.EmitArgumentNullThrow(check.Parameter, throwArgumentException);
        }

        this._buffer.Add(throwArgumentException);
        this.EmitArgumentThrow(check.Parameter);
        this._buffer.Add(next);
    }

    /// <summary><c>attribute = new TAttribute(...); if (attribute.IsValid(value)) goto next;</c></summary>
    private void EmitValidity(WovenCheck check, Instruction next)
    {
        this._constructions.Emit(check.Attribute, check.AttributeType);
        this._buffer.Emit(OpCodes.Stloc, this._attributeLocal);
        this._buffer.Emit(OpCodes.Ldloc, this._attributeLocal);
        this.EmitLoadArgumentAsObject(check.Parameter);
        this._buffer.Emit(OpCodes.Callvirt, this._references.IsValid);
        this._buffer.Emit(OpCodes.Brtrue, next);
    }

    /// <summary>
    /// <c>if (value is not null) goto throwArgumentException;
    /// throw new ArgumentNullException(name, attribute.FormatErrorMessage(name));</c>
    /// </summary>
    private void EmitArgumentNullThrow(ParameterDefinition parameter, Instruction throwArgumentException)
    {
        this.EmitLoadArgumentAsObject(parameter);
        this._buffer.Emit(OpCodes.Brtrue, throwArgumentException);
        this._buffer.Emit(OpCodes.Ldstr, parameter.Name);
        this._buffer.Emit(OpCodes.Ldloc, this._attributeLocal);
        this._buffer.Emit(OpCodes.Ldstr, parameter.Name);
        this._buffer.Emit(OpCodes.Callvirt, this._references.FormatErrorMessage);
        this._buffer.Emit(OpCodes.Newobj, this._references.ArgumentNullExceptionConstructor);
        this._buffer.Emit(OpCodes.Throw);
    }

    /// <summary>
    /// <c>throw new ArgumentException(attribute.FormatErrorMessage(name), name);</c>, after the
    /// attribute has been loaded by the branch target that precedes it.
    /// </summary>
    private void EmitArgumentThrow(ParameterDefinition parameter)
    {
        this._buffer.Emit(OpCodes.Ldstr, parameter.Name);
        this._buffer.Emit(OpCodes.Callvirt, this._references.FormatErrorMessage);
        this._buffer.Emit(OpCodes.Ldstr, parameter.Name);
        this._buffer.Emit(OpCodes.Newobj, this._references.ArgumentExceptionConstructor);
        this._buffer.Emit(OpCodes.Throw);
    }

    private void EmitLoadArgumentAsObject(ParameterDefinition parameter)
    {
        this._buffer.Emit(OpCodes.Ldarg, parameter);
        TypeReference type = parameter.ParameterType;
        if (type.IsValueType || type.IsPrimitive || type.IsGenericParameter)
        {
            this._buffer.Emit(OpCodes.Box, type);
        }
    }
}

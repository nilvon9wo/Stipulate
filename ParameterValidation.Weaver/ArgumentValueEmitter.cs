using System.Collections.Immutable;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

/// <summary>Pushes the value of an attribute argument, as stored in metadata, onto the stack.</summary>
/// <param name="method">The method being woven, which the errors name.</param>
internal sealed class ArgumentValueEmitter(
    WellKnownReferences references,
    MethodDefinition method,
    InstructionBuffer buffer
)
{
    /// <summary>How each primitive constant is loaded; unsigned values are loaded by their bit pattern.</summary>
    private static readonly ImmutableDictionary<Type, Func<object, Instruction>> PrimitiveLoaders =
        new Dictionary<Type, Func<object, Instruction>>
        {
            [typeof(string)] = static value => Instruction.Create(OpCodes.Ldstr, (string)value),
            [typeof(bool)] = static value => Instruction.Create(OpCodes.Ldc_I4, Convert.ToInt32(value)),
            [typeof(char)] = LoadInt32,
            [typeof(sbyte)] = LoadInt32,
            [typeof(byte)] = LoadInt32,
            [typeof(short)] = LoadInt32,
            [typeof(ushort)] = LoadInt32,
            [typeof(int)] = LoadInt32,
            [typeof(uint)] = static value => Instruction.Create(OpCodes.Ldc_I4, unchecked((int)(uint)value)),
            [typeof(long)] = static value => Instruction.Create(OpCodes.Ldc_I8, (long)value),
            [typeof(ulong)] = static value => Instruction.Create(OpCodes.Ldc_I8, unchecked((long)(ulong)value)),
            [typeof(float)] = static value => Instruction.Create(OpCodes.Ldc_R4, (float)value),
            [typeof(double)] = static value => Instruction.Create(OpCodes.Ldc_R8, (double)value),
        }.ToImmutableDictionary();

    private readonly WellKnownReferences _references = references;
    private readonly MethodDefinition _method = method;
    private readonly InstructionBuffer _buffer = buffer;

    public void Emit(CustomAttributeArgument argument)
    {
        switch (argument.Value)
        {
            case null:
                this._buffer.Emit(OpCodes.Ldnull);
                break;
            case CustomAttributeArgument boxed:
                this.EmitBoxed(boxed);
                break;
            case CustomAttributeArgument[] elements:
                this.EmitArray(argument, elements);
                break;
            case TypeReference type:
                this.EmitType(type);
                break;
            default:
                this.EmitPrimitive(argument.Value);
                break;
        }
    }

    /// <summary>An argument whose declared type is object: the value carries its own type.</summary>
    private void EmitBoxed(CustomAttributeArgument boxed)
    {
        this.Emit(boxed);
        if (boxed.Value is not (null or string or TypeReference or CustomAttributeArgument[]))
        {
            this._buffer.Emit(OpCodes.Box, this._references.Module.ImportReference(boxed.Type));
        }
    }

    private void EmitArray(CustomAttributeArgument argument, CustomAttributeArgument[] elements)
    {
        TypeReference elementType = this._references.Module.ImportReference(((ArrayType)argument.Type).ElementType);
        this._buffer.Emit(OpCodes.Ldc_I4, elements.Length);
        this._buffer.Emit(OpCodes.Newarr, elementType);
        IEnumerable<(CustomAttributeArgument Element, int Index)> indexed =
            elements.Select(static (element, index) => (element, index));
        foreach ((CustomAttributeArgument element, int index) in indexed)
        {
            this._buffer.Emit(OpCodes.Dup);
            this._buffer.Emit(OpCodes.Ldc_I4, index);
            this.Emit(element);
            this._buffer.Emit(OpCodes.Stelem_Any, elementType);
        }
    }

    private void EmitType(TypeReference type)
    {
        this._buffer.Emit(OpCodes.Ldtoken, this._references.Module.ImportReference(type));
        this._buffer.Emit(OpCodes.Call, this._references.GetTypeFromHandle);
    }

    private void EmitPrimitive(object value)
    {
        Func<object, Instruction> load = PrimitiveLoaders.GetValueOrDefault(value.GetType())
            ?? throw new WeavingException(
                $"Unsupported attribute argument of type '{value.GetType()}' in '{this._method.FullName}'."
            );
        this._buffer.Add(load(value));
    }

    private static Instruction LoadInt32(object value) => Instruction.Create(OpCodes.Ldc_I4, Convert.ToInt32(value));
}

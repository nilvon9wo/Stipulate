using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

/// <summary>Emits <c>new TAttribute(args) { Named = value, ... }</c>, leaving the attribute on the stack.</summary>
internal sealed class AttributeConstructionEmitter(
    WellKnownReferences references,
    ArgumentValueEmitter values,
    InstructionBuffer buffer
)
{
    private readonly WellKnownReferences _references = references;
    private readonly ArgumentValueEmitter _values = values;
    private readonly InstructionBuffer _buffer = buffer;

    public void Emit(CustomAttribute attribute, TypeDefinition attributeType)
    {
        foreach (CustomAttributeArgument argument in attribute.ConstructorArguments)
        {
            this._values.Emit(argument);
        }

        this._buffer.Emit(OpCodes.Newobj, attribute.Constructor);
        foreach (CustomAttributeNamedArgument property in attribute.Properties)
        {
            this.EmitPropertySet(property, attributeType);
        }

        foreach (CustomAttributeNamedArgument field in attribute.Fields)
        {
            this.EmitFieldSet(field, attributeType);
        }
    }

    private void EmitPropertySet(CustomAttributeNamedArgument property, TypeDefinition attributeType)
    {
        MethodDefinition setter = FindMember(attributeType, property.Name, type => FindSetter(type, property.Name));
        this._buffer.Emit(OpCodes.Dup);
        this._values.Emit(property.Argument);
        this._buffer.Emit(OpCodes.Callvirt, this._references.Module.ImportReference(setter));
    }

    private void EmitFieldSet(CustomAttributeNamedArgument field, TypeDefinition attributeType)
    {
        FieldDefinition definition = FindMember(attributeType, field.Name, type => FindField(type, field.Name));
        this._buffer.Emit(OpCodes.Dup);
        this._values.Emit(field.Argument);
        this._buffer.Emit(OpCodes.Stfld, this._references.Module.ImportReference(definition));
    }

    private static MethodDefinition? FindSetter(TypeDefinition type, string name)
        => type.Properties.FirstOrDefault(property => property.Name == name)?.SetMethod;

    private static FieldDefinition? FindField(TypeDefinition type, string name)
        => type.Fields.FirstOrDefault(field => field.Name == name);

    /// <summary>The member, declared by the attribute's type or one of its base types.</summary>
    private static T FindMember<T>(TypeDefinition attributeType, string name, Func<TypeDefinition, T?> find)
        where T : class
        => CecilTypeHierarchy.SelfAndBaseTypes(attributeType)
            .Select(find)
            .FirstOrDefault(static member => member is not null)
            ?? throw new WeavingException($"Could not find member '{name}' on attribute '{attributeType.FullName}'.");
}

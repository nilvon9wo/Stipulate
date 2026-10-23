using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Runtime.InteropServices;

namespace ParameterValidation.Weaver;

/// <summary>
/// Inserts, at the start of a method, the IL equivalent of the following for every
/// <c>ValidationAttribute</c> on every parameter:
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
internal sealed class MethodWeaver(WellKnownReferences references, MethodDefinition method)
{
	private readonly List<Instruction> _instructions = [];

	public bool Weave()
	{
		if (!method.HasBody)
		{
			return false;
		}

		List<(ParameterDefinition Parameter, List<(CustomAttribute Attribute, TypeDefinition? Definition)> Attributes)> checks = [.. method.Parameters
			.Where(static parameter => !parameter.ParameterType.IsByReference)
			.Select(parameter => (Parameter: parameter, Attributes: parameter.CustomAttributes
				.Select(attribute => (Attribute: attribute, Definition: this.ResolveValidationAttribute(attribute)))
				.Where(static a => a.Definition is not null)
				.ToList()))
			.Where(static check => check.Attributes.Count > 0)];

		if (checks.Count == 0)
		{
			return false;
		}

		VariableDefinition attributeLocal = new(references.ValidationAttribute);
		method.Body.Variables.Add(attributeLocal);

		foreach ((ParameterDefinition? parameter, List<(CustomAttribute Attribute, TypeDefinition? Definition)>? attributes) in checks)
		{
			foreach ((CustomAttribute? attribute, TypeDefinition? definition) in attributes)
			{
				this.EmitCheck(parameter, attribute, definition!, attributeLocal);
			}
		}

		method.Body.SimplifyMacros();
		ILProcessor il = method.Body.GetILProcessor();
		Instruction originalFirst = method.Body.Instructions[0];
		foreach (ref Instruction instruction in CollectionsMarshal.AsSpan(this._instructions))
		{
			il.InsertBefore(originalFirst, instruction);
		}

		method.Body.OptimizeMacros();
		return true;
	}

	/// <returns>The attribute's type definition if it derives from <c>ValidationAttribute</c>; otherwise <c>null</c>.</returns>
	private TypeDefinition? ResolveValidationAttribute(CustomAttribute attribute)
	{
		for (TypeDefinition? type = Resolve(attribute.AttributeType); type is not null; type = type.BaseType is null ? null : Resolve(type.BaseType))
		{
			if (type.FullName == WellKnownReferences.ValidationAttributeName)
			{
				references.EnsureValidationAttribute(type);
				return Resolve(attribute.AttributeType);
			}
		}

		return null;
	}

	private static TypeDefinition Resolve(TypeReference type)
		=> type.Resolve() ?? throw new WeavingException($"Could not resolve type '{type.FullName}' from '{type.Scope.Name}'.");

	private void EmitCheck(ParameterDefinition parameter, CustomAttribute attribute, TypeDefinition attributeType, VariableDefinition attributeLocal)
	{
		Instruction next = Instruction.Create(OpCodes.Nop);
		Instruction throwArgumentException = Instruction.Create(OpCodes.Ldloc, attributeLocal);

		// attribute = new TAttribute(args) { Named = value, ... };
		this.EmitNewAttribute(attribute, attributeType);
		this.Emit(OpCodes.Stloc, attributeLocal);

		// if (attribute.IsValid(value)) goto next;
		this.Emit(OpCodes.Ldloc, attributeLocal);
		this.EmitLoadArgumentAsObject(parameter);
		this.Emit(OpCodes.Callvirt, references.IsValid);
		this.Emit(OpCodes.Brtrue, next);

		if (IsRequiredAttribute(attributeType))
		{
			// if (value is not null) goto throwArgumentException;
			this.EmitLoadArgumentAsObject(parameter);
			this.Emit(OpCodes.Brtrue, throwArgumentException);

			// throw new ArgumentNullException(name, attribute.FormatErrorMessage(name));
			this.Emit(OpCodes.Ldstr, parameter.Name);
			this.Emit(OpCodes.Ldloc, attributeLocal);
			this.Emit(OpCodes.Ldstr, parameter.Name);
			this.Emit(OpCodes.Callvirt, references.FormatErrorMessage);
			this.Emit(OpCodes.Newobj, references.ArgumentNullExceptionCtor);
			this.Emit(OpCodes.Throw);
		}

		// throw new ArgumentException(attribute.FormatErrorMessage(name), name);
		this._instructions.Add(throwArgumentException);
		this.Emit(OpCodes.Ldstr, parameter.Name);
		this.Emit(OpCodes.Callvirt, references.FormatErrorMessage);
		this.Emit(OpCodes.Ldstr, parameter.Name);
		this.Emit(OpCodes.Newobj, references.ArgumentExceptionCtor);
		this.Emit(OpCodes.Throw);

		this._instructions.Add(next);
	}

	private static bool IsRequiredAttribute(TypeDefinition attributeType)
	{
		for (TypeDefinition? type = attributeType; type is not null; type = type.BaseType?.Resolve())
		{
			if (type.FullName == WellKnownReferences.RequiredAttributeName)
			{
				return true;
			}
		}

		return false;
	}

	private void EmitNewAttribute(CustomAttribute attribute, TypeDefinition attributeType)
	{
		foreach (CustomAttributeArgument argument in attribute.ConstructorArguments)
		{
			this.EmitValue(argument);
		}

		this.Emit(OpCodes.Newobj, attribute.Constructor);

		foreach (CustomAttributeNamedArgument property in attribute.Properties)
		{
			MethodDefinition setter = FindMember(attributeType, type => type.Properties.FirstOrDefault(p => p.Name == property.Name)?.SetMethod, property.Name);
			this.Emit(OpCodes.Dup);
			this.EmitValue(property.Argument);
			this.Emit(OpCodes.Callvirt, references.Module.ImportReference(setter));
		}

		foreach (CustomAttributeNamedArgument field in attribute.Fields)
		{
			FieldDefinition definition = FindMember(attributeType, type => type.Fields.FirstOrDefault(f => f.Name == field.Name), field.Name);
			this.Emit(OpCodes.Dup);
			this.EmitValue(field.Argument);
			this.Emit(OpCodes.Stfld, references.Module.ImportReference(definition));
		}
	}

	private static T FindMember<T>(TypeDefinition attributeType, Func<TypeDefinition, T?> find, string name)
		where T : class
	{
		for (TypeDefinition? type = attributeType; type is not null; type = type.BaseType?.Resolve())
		{
			if (find(type) is T member)
			{
				return member;
			}
		}

		throw new WeavingException($"Could not find member '{name}' on attribute '{attributeType.FullName}'.");
	}

	/// <summary>Pushes the value of an attribute argument, as stored in metadata, onto the stack.</summary>
	private void EmitValue(CustomAttributeArgument argument)
	{
		switch (argument.Value)
		{
			case null:
				this.Emit(OpCodes.Ldnull);
				break;
			case CustomAttributeArgument boxed:
				// An argument whose declared type is object: the value carries its own type.
				this.EmitValue(boxed);
				if (boxed.Value is not (null or string or TypeReference or CustomAttributeArgument[]))
				{
					this.Emit(OpCodes.Box, references.Module.ImportReference(boxed.Type));
				}

				break;
			case CustomAttributeArgument[] elements:
				TypeReference elementType = references.Module.ImportReference(((ArrayType)argument.Type).ElementType);
				this.Emit(OpCodes.Ldc_I4, elements.Length);
				this.Emit(OpCodes.Newarr, elementType);
				for (int i = 0; i < elements.Length; i++)
				{
					this.Emit(OpCodes.Dup);
					this.Emit(OpCodes.Ldc_I4, i);
					this.EmitValue(elements[i]);
					this.Emit(OpCodes.Stelem_Any, elementType);
				}

				break;
			case TypeReference type:
				this.Emit(OpCodes.Ldtoken, references.Module.ImportReference(type));
				this.Emit(OpCodes.Call, references.GetTypeFromHandle);
				break;
			case string text:
				this.Emit(OpCodes.Ldstr, text);
				break;
			case bool flag:
				this.Emit(OpCodes.Ldc_I4, flag ? 1 : 0);
				break;
			case char or sbyte or byte or short or ushort or int:
				this.Emit(OpCodes.Ldc_I4, Convert.ToInt32(argument.Value));
				break;
			case uint number:
				this.Emit(OpCodes.Ldc_I4, unchecked((int)number));
				break;
			case long number:
				this.Emit(OpCodes.Ldc_I8, number);
				break;
			case ulong number:
				this.Emit(OpCodes.Ldc_I8, unchecked((long)number));
				break;
			case float number:
				this.Emit(OpCodes.Ldc_R4, number);
				break;
			case double number:
				this.Emit(OpCodes.Ldc_R8, number);
				break;
			default:
				throw new WeavingException($"Unsupported attribute argument of type '{argument.Value.GetType()}' in '{method.FullName}'.");
		}
	}

	private void EmitLoadArgumentAsObject(ParameterDefinition parameter)
	{
		this.Emit(OpCodes.Ldarg, parameter);
		TypeReference type = parameter.ParameterType;
		if (type.IsValueType || type.IsPrimitive || type.IsGenericParameter)
		{
			this.Emit(OpCodes.Box, type);
		}
	}

	private void Emit(OpCode opCode) => this._instructions.Add(Instruction.Create(opCode));

	private void Emit(OpCode opCode, int value) => this._instructions.Add(Instruction.Create(opCode, value));

	private void Emit(OpCode opCode, long value) => this._instructions.Add(Instruction.Create(opCode, value));

	private void Emit(OpCode opCode, float value) => this._instructions.Add(Instruction.Create(opCode, value));

	private void Emit(OpCode opCode, double value) => this._instructions.Add(Instruction.Create(opCode, value));

	private void Emit(OpCode opCode, string value) => this._instructions.Add(Instruction.Create(opCode, value));

	private void Emit(OpCode opCode, Instruction target) => this._instructions.Add(Instruction.Create(opCode, target));

	private void Emit(OpCode opCode, VariableDefinition variable) => this._instructions.Add(Instruction.Create(opCode, variable));

	private void Emit(OpCode opCode, ParameterDefinition parameter) => this._instructions.Add(Instruction.Create(opCode, parameter));

	private void Emit(OpCode opCode, TypeReference type) => this._instructions.Add(Instruction.Create(opCode, type));

	private void Emit(OpCode opCode, MethodReference target) => this._instructions.Add(Instruction.Create(opCode, target));

	private void Emit(OpCode opCode, FieldReference field) => this._instructions.Add(Instruction.Create(opCode, field));
}

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ParameterValidation.Weaver;

/// <summary>The instructions to insert at the start of a method, in order.</summary>
internal sealed class InstructionBuffer
{
    private readonly List<Instruction> _instructions = [];

    public IReadOnlyList<Instruction> Instructions => this._instructions;

    /// <summary>Adds an instruction created elsewhere, such as a branch target.</summary>
    public void Add(Instruction instruction) => this._instructions.Add(instruction);

    public void Emit(OpCode opCode) => this.Add(Instruction.Create(opCode));

    public void Emit(OpCode opCode, int value) => this.Add(Instruction.Create(opCode, value));

    public void Emit(OpCode opCode, string value) => this.Add(Instruction.Create(opCode, value));

    public void Emit(OpCode opCode, Instruction target) => this.Add(Instruction.Create(opCode, target));

    public void Emit(OpCode opCode, VariableDefinition variable) => this.Add(Instruction.Create(opCode, variable));

    public void Emit(OpCode opCode, ParameterDefinition parameter) => this.Add(Instruction.Create(opCode, parameter));

    public void Emit(OpCode opCode, TypeReference type) => this.Add(Instruction.Create(opCode, type));

    public void Emit(OpCode opCode, MethodReference method) => this.Add(Instruction.Create(opCode, method));

    public void Emit(OpCode opCode, FieldReference field) => this.Add(Instruction.Create(opCode, field));
}

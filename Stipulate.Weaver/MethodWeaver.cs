using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// Inserts, at the start of a method, a check of every <c>ValidationAttribute</c> on every
/// parameter (see <see cref="CheckEmitter"/>). By-reference parameters aren't checked: what they
/// refer to may change.
/// </summary>
internal sealed class MethodWeaver(WellKnownReferences references, MethodDefinition method)
{
    private readonly WellKnownReferences _references = references;
    private readonly MethodDefinition _method = method;

    /// <returns>Whether the method had anything to check, and so was woven.</returns>
    public bool Weave()
    {
        List<WovenCheck> checks = this._method.HasBody
            ? this.FindChecks()
            : [];
        bool isWoven = checks.Count > 0;
        if (isWoven)
        {
            this.Insert(checks);
        }

        return isWoven;
    }

    private List<WovenCheck> FindChecks()
        => [.. this._method.Parameters
            .Where(static parameter => !parameter.ParameterType.IsByReference)
            .SelectMany(this.FindChecks)];

    private IEnumerable<WovenCheck> FindChecks(ParameterDefinition parameter)
        => parameter.CustomAttributes
            .Select(static attribute => (Attribute: attribute, Type: ValidationAttributeResolver.Resolve(attribute)))
            .Where(static resolved => resolved.Type is not null)
            .Select(resolved => new WovenCheck(parameter, resolved.Attribute, resolved.Type!));

    private void Insert(List<WovenCheck> checks)
    {
        InstructionBuffer buffer = this.EmitChecks(checks);
        MethodBody body = this._method.Body;
        body.SimplifyMacros();
        ILProcessor processor = body.GetILProcessor();
        Instruction originalFirst = body.Instructions[0];
        foreach (Instruction instruction in buffer.Instructions)
        {
            processor.InsertBefore(originalFirst, instruction);
        }

        body.OptimizeMacros();
    }

    private InstructionBuffer EmitChecks(List<WovenCheck> checks)
    {
        InstructionBuffer buffer = new();
        ArgumentValueEmitter values = new(this._references, this._method, buffer);
        AttributeConstructionEmitter constructions = new(this._references, values, buffer);
        CheckEmitter emitter = new(this._references, constructions, buffer, MethodDisplay.Display(this._method));
        checks.ForEach(emitter.Emit);
        return buffer;
    }
}

using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// References to the members the injected IL uses, scoped to the module being woven so they bind
/// against the same reference assemblies it was compiled against.
/// </summary>
internal sealed class WellKnownReferences
{
    public const string ValidationAttributeName = "System.ComponentModel.DataAnnotations.ValidationAttribute";
    public const string ShowValueInMessageAttributeName =
        "Net.NowhereAtAll.Stipulate.Contracts.ShowValueInMessageAttribute";
    public const string ArgumentChecksName = "Net.NowhereAtAll.Stipulate.Runtime.ArgumentChecks";

    private const string SystemNamespace = "System";
    private const string CheckName = "Check";

    private readonly Lazy<MethodDefinition> _check;

    public WellKnownReferences(ModuleDefinition module)
    {
        this.Module = module;
        TypeReference type = this.CoreType("Type");
        TypeReference runtimeTypeHandle = this.CoreType("RuntimeTypeHandle");
        runtimeTypeHandle.IsValueType = true;
        this.GetTypeFromHandle = new MethodReference("GetTypeFromHandle", type, type)
        {
            Parameters = { new ParameterDefinition(runtimeTypeHandle) },
        };
        this._check = new Lazy<MethodDefinition>(this.FindCheck);
    }

    public ModuleDefinition Module { get; }

    /// <summary>
    /// <c>Type.GetTypeFromHandle(RuntimeTypeHandle)</c>, used to load <c>typeof(...)</c> attribute arguments.
    /// </summary>
    public MethodReference GetTypeFromHandle { get; }

    /// <summary>
    /// <c>ArgumentChecks.Check(ValidationAttribute, object, string, string, bool)</c>, which
    /// Stipulate.targets compiles into the assembly itself. Looked up when the first check is
    /// emitted: an assembly with nothing to check needn't have it.
    /// </summary>
    /// <exception cref="WeavingException">The assembly doesn't contain it.</exception>
    public MethodDefinition Check => this._check.Value;

    private MethodDefinition FindCheck()
        => this.Module.GetType(ArgumentChecksName)?.Methods.FirstOrDefault(static method => method.Name == CheckName)
            ?? throw new WeavingException(
                $"{this.Module.Name} has parameters with validation attributes, but not the check that "
                + $"enforces them, {ArgumentChecksName}.{CheckName}. Stipulate.targets compiles it into "
                + "every project it weaves, from Stipulate.ArgumentChecks.cs (or .vb, or .fs, in the project's "
                + "language) in the package's buildTransitive folder: check that the project imports "
                + "Stipulate.targets, and that nothing removes that file from its Compile items.",
                WeavingErrorCodes.ChecksMissing
            );

    private TypeReference CoreType(string name)
        => new(SystemNamespace, name, this.Module, this.Module.TypeSystem.CoreLibrary);
}

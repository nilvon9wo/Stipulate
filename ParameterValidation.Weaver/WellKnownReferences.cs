using Mono.Cecil;

namespace ParameterValidation.Weaver;

/// <summary>
/// References to the framework types and members the injected IL uses, scoped to the module
/// being woven so they bind against the same reference assemblies it was compiled against.
/// </summary>
internal sealed class WellKnownReferences
{
    public const string ValidationAttributeName = "System.ComponentModel.DataAnnotations.ValidationAttribute";
    public const string RequiredAttributeName = "System.ComponentModel.DataAnnotations.RequiredAttribute";

    private const string SystemNamespace = "System";
    private const string ConstructorName = ".ctor";
    private const string IsValidName = "IsValid";
    private const string FormatErrorMessageName = "FormatErrorMessage";

    private TypeReference? _validationAttribute;
    private MethodReference? _isValid;
    private MethodReference? _formatErrorMessage;

    public WellKnownReferences(ModuleDefinition module)
    {
        this.Module = module;
        TypeReference argumentException = this.CoreType("ArgumentException");
        TypeReference argumentNullException = this.CoreType("ArgumentNullException");
        TypeReference type = this.CoreType("Type");
        TypeReference runtimeTypeHandle = this.CoreType("RuntimeTypeHandle");
        runtimeTypeHandle.IsValueType = true;
        TypeReference text = module.TypeSystem.String;
        this.ArgumentExceptionConstructor = this.Constructor(argumentException, text, text);
        this.ArgumentNullExceptionConstructor = this.Constructor(argumentNullException, text, text);
        this.GetTypeFromHandle = new MethodReference("GetTypeFromHandle", type, type)
        {
            Parameters = { new ParameterDefinition(runtimeTypeHandle) },
        };
    }

    public ModuleDefinition Module { get; }

    /// <summary><c>ArgumentException(string message, string paramName)</c></summary>
    public MethodReference ArgumentExceptionConstructor { get; }

    /// <summary><c>ArgumentNullException(string paramName, string message)</c></summary>
    public MethodReference ArgumentNullExceptionConstructor { get; }

    /// <summary>
    /// <c>Type.GetTypeFromHandle(RuntimeTypeHandle)</c>, used to load <c>typeof(...)</c> attribute arguments.
    /// </summary>
    public MethodReference GetTypeFromHandle { get; }

    /// <remarks>Resolved by <see cref="EnsureValidationAttribute"/>, before any check is emitted.</remarks>
    public TypeReference ValidationAttribute => this._validationAttribute!;

    /// <summary><c>ValidationAttribute.IsValid(object)</c></summary>
    public MethodReference IsValid => this._isValid!;

    /// <summary><c>ValidationAttribute.FormatErrorMessage(string)</c></summary>
    public MethodReference FormatErrorMessage => this._formatErrorMessage!;

    /// <summary>
    /// Records the resolved <c>ValidationAttribute</c> the first time one is found. It isn't looked
    /// up eagerly because an assembly that never uses DataAnnotations doesn't reference it.
    /// </summary>
    public void EnsureValidationAttribute(TypeDefinition validationAttribute)
    {
        this._validationAttribute ??= this.Module.ImportReference(validationAttribute);
        this._isValid ??= this.ImportMethod(validationAttribute, IsValidName, static method => method.IsPublic);
        this._formatErrorMessage ??= this.ImportMethod(validationAttribute, FormatErrorMessageName, static _ => true);
    }

    private MethodReference ImportMethod(TypeDefinition type, string name, Func<MethodDefinition, bool> isWanted)
    {
        MethodDefinition method = type.Methods
            .First(method => method.Name == name && method.Parameters.Count == 1 && isWanted(method));
        return this.Module.ImportReference(method);
    }

    private TypeReference CoreType(string name)
        => new(SystemNamespace, name, this.Module, this.Module.TypeSystem.CoreLibrary);

    private MethodReference Constructor(TypeReference declaringType, params TypeReference[] parameterTypes)
    {
        MethodReference constructor = new(ConstructorName, this.Module.TypeSystem.Void, declaringType)
        {
            HasThis = true,
        };
        foreach (TypeReference parameterType in parameterTypes)
        {
            constructor.Parameters.Add(new ParameterDefinition(parameterType));
        }

        return constructor;
    }
}

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

	private TypeReference? _validationAttribute;

	public WellKnownReferences(ModuleDefinition module)
	{
		this.Module = module;

		IMetadataScope coreLibrary = module.TypeSystem.CoreLibrary;
		TypeReference argumentException = new("System", "ArgumentException", module, coreLibrary);
		TypeReference argumentNullException = new("System", "ArgumentNullException", module, coreLibrary);
		TypeReference type = new("System", "Type", module, coreLibrary);
		TypeReference runtimeTypeHandle = new("System", "RuntimeTypeHandle", module, coreLibrary, valueType: true);

		this.ArgumentExceptionCtor = this.Constructor(argumentException, module.TypeSystem.String, module.TypeSystem.String);
		this.ArgumentNullExceptionCtor = this.Constructor(argumentNullException, module.TypeSystem.String, module.TypeSystem.String);
		this.GetTypeFromHandle = new MethodReference("GetTypeFromHandle", type, type)
		{
			Parameters = { new ParameterDefinition(runtimeTypeHandle) },
		};
	}

	public ModuleDefinition Module { get; }

	/// <summary><c>ArgumentException(string message, string paramName)</c></summary>
	public MethodReference ArgumentExceptionCtor { get; }

	/// <summary><c>ArgumentNullException(string paramName, string message)</c></summary>
	public MethodReference ArgumentNullExceptionCtor { get; }

	/// <summary><c>Type.GetTypeFromHandle(RuntimeTypeHandle)</c>, used to load <c>typeof(...)</c> attribute arguments.</summary>
	public MethodReference GetTypeFromHandle { get; }

	public TypeReference ValidationAttribute => this._validationAttribute ?? throw NotInitialized();

	/// <summary><c>ValidationAttribute.IsValid(object)</c></summary>
	public MethodReference IsValid { get => field ?? throw NotInitialized(); private set; }

	/// <summary><c>ValidationAttribute.FormatErrorMessage(string)</c></summary>
	public MethodReference FormatErrorMessage { get => field ?? throw NotInitialized(); private set; }

	/// <summary>
	/// Records the resolved <c>ValidationAttribute</c> the first time one is found. It isn't looked
	/// up eagerly because an assembly that never uses DataAnnotations doesn't reference it.
	/// </summary>
	public void EnsureValidationAttribute(TypeDefinition validationAttribute)
	{
		if (this._validationAttribute is not null)
		{
			return;
		}

		this._validationAttribute = this.Module.ImportReference(validationAttribute);
		this.IsValid = this.Module.ImportReference(validationAttribute.Methods.First(static m =>
			m.Name == "IsValid" && m.IsPublic && m.Parameters.Count == 1));
		this.FormatErrorMessage = this.Module.ImportReference(validationAttribute.Methods.First(static m =>
			m.Name == "FormatErrorMessage" && m.Parameters.Count == 1));
	}

	private MethodReference Constructor(TypeReference declaringType, params TypeReference[] parameterTypes)
	{
		MethodReference constructor = new(".ctor", this.Module.TypeSystem.Void, declaringType) { HasThis = true };
		foreach (TypeReference parameterType in parameterTypes)
		{
			constructor.Parameters.Add(new ParameterDefinition(parameterType));
		}

		return constructor;
	}

	private static InvalidOperationException NotInitialized()
		=> new("ValidationAttribute has not been resolved yet.");
}

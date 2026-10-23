using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ParameterValidation.Analyzers;

/// <summary>Mirrors <c>ParameterValidation.Contracts.ValidationDimensions</c>.</summary>
internal static class Dimensions
{
	public const string NotNull = "NotNull";
	public const string NotBlank = "NotBlank";
	public const string Length = "Length";
	public const string Value = "Value";
}

/// <summary>
/// Works out what a validation attribute usage constrains, from (in order of precedence) a
/// <c>[ComparableValidation]</c> on its class, the built-in descriptions of the DataAnnotations
/// attributes, or an <c>[assembly: ComparableValidation(typeof(...))]</c> in this compilation or
/// one it references. One instance serves one compilation.
/// </summary>
internal sealed class ValidationAttributeDescriber
{
	public const string ValidationAttributeName = "System.ComponentModel.DataAnnotations.ValidationAttribute";
	public const string MarkerName = "ParameterValidation.Contracts.ComparableValidationAttribute";
	private const string ContractsAssemblyName = "ParameterValidation.Contracts";
	private const string DataAnnotations = "System.ComponentModel.DataAnnotations.";

	/// <remarks>Only these exact classes: a derived class can change what it accepts.</remarks>
	private static readonly ImmutableDictionary<string, ImmutableArray<Descriptor>> BuiltIns = new Dictionary<string, ImmutableArray<Descriptor>>
	{
		[DataAnnotations + "RequiredAttribute"] =
		[
			new(Dimensions.NotNull, ConstraintKind.Flag),
			new(Dimensions.NotBlank, ConstraintKind.Flag, UnlessArgument: "AllowEmptyStrings"),
		],
		[DataAnnotations + "MinLengthAttribute"] = [new(Dimensions.Length, ConstraintKind.LowerBound, "length")],

		// MaxLength() means no maximum.
		[DataAnnotations + "MaxLengthAttribute"] = [new(Dimensions.Length, ConstraintKind.UpperBound, "length", double.PositiveInfinity)],
		[DataAnnotations + "LengthAttribute"] =
		[
			new(Dimensions.Length, ConstraintKind.LowerBound, "minimumLength"),
			new(Dimensions.Length, ConstraintKind.UpperBound, "maximumLength"),
		],
		[DataAnnotations + "StringLengthAttribute"] =
		[
			new(Dimensions.Length, ConstraintKind.LowerBound, "MinimumLength", 0.0),
			new(Dimensions.Length, ConstraintKind.UpperBound, "maximumLength"),
		],

		// Range(typeof(T), "min", "max")'s string bounds are read by ToRangeLimit.
		[DataAnnotations + "RangeAttribute"] =
		[
			new(Dimensions.Value, ConstraintKind.LowerBound, "minimum", ExclusiveArgument: "MinimumIsExclusive"),
			new(Dimensions.Value, ConstraintKind.UpperBound, "maximum", ExclusiveArgument: "MaximumIsExclusive"),
		],
		[DataAnnotations + "AllowedValuesAttribute"] = [new(Dimensions.Value, ConstraintKind.AllowedValues, "values")],
		[DataAnnotations + "DeniedValuesAttribute"] = [new(Dimensions.Value, ConstraintKind.DeniedValues, "values")],
	}.ToImmutableDictionary();

	private readonly Compilation _compilation;
	private readonly string? _solutionRoot;
	private readonly Dictionary<ITypeSymbol, List<AttributeData>> _assemblyDescriptions = new(SymbolEqualityComparer.Default);
	private readonly ConcurrentDictionary<ITypeSymbol, ImmutableArray<Descriptor>?> _descriptors = new(SymbolEqualityComparer.Default);

	/// <param name="solutionRoot">
	/// Referenced assemblies whose files are under this directory count as part of the solution,
	/// so their attributes can be given a <c>[ComparableValidation]</c> by the developer.
	/// </param>
	public ValidationAttributeDescriber(Compilation compilation, string? solutionRoot)
	{
		this._compilation = compilation;
		this._solutionRoot = solutionRoot is null || solutionRoot.Trim().Length == 0 ? null : NormalizePath(solutionRoot.Trim());

		foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols.Prepend(compilation.Assembly))
		{
			// Only an assembly that references the contracts can contain a description.
			if (!assembly.Modules.Any(static module => module.ReferencedAssemblies.Any(static identity => identity.Name == ContractsAssemblyName)))
			{
				continue;
			}

			foreach (AttributeData attribute in assembly.GetAttributes())
			{
				if (IsMarker(attribute) && attribute.ConstructorArguments.Length == 3 && attribute.ConstructorArguments[0].Value is ITypeSymbol target)
				{
					if (!this._assemblyDescriptions.TryGetValue(target.OriginalDefinition, out List<AttributeData>? markers))
					{
						this._assemblyDescriptions[target.OriginalDefinition] = markers = [];
					}

					markers.Add(attribute);
				}
			}
		}
	}

	public static bool IsMarker(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() == MarkerName;

	public static bool IsValidationAttribute(ITypeSymbol? type)
	{
		for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (current.ToDisplayString() == ValidationAttributeName)
			{
				return true;
			}
		}

		return false;
	}

	public ImmutableArray<DescribedAttribute> GetValidationAttributes(ISymbol parameterOrProperty, CancellationToken cancellationToken)
		=> [.. parameterOrProperty.GetAttributes()
			.Where(static attribute => IsValidationAttribute(attribute.AttributeClass))
			.Select(attribute => this.Describe(attribute, cancellationToken))];

	/// <summary>
	/// Whether <paramref name="type"/> is declared in this solution, so that the developer could add
	/// a <c>[ComparableValidation]</c> to it.
	/// </summary>
	public bool IsInSolution(INamedTypeSymbol type)
	{
		IAssemblySymbol assembly = type.ContainingAssembly;
		return SymbolEqualityComparer.Default.Equals(assembly, this._compilation.Assembly)
			|| this._compilation.GetMetadataReference(assembly) switch
			{
				// The IDE references other projects in the solution as compilations, the command line as files.
				CompilationReference => true,
				PortableExecutableReference { FilePath: { } path } => this._solutionRoot is not null
					&& NormalizePath(path).StartsWith(this._solutionRoot, StringComparison.OrdinalIgnoreCase),
				_ => false,
			};
	}

	/// <summary>Reads one <c>[ComparableValidation]</c> describing <paramref name="target"/>.</summary>
	/// <param name="error">Why the description is invalid, phrased to follow "it is invalid because".</param>
	public static bool TryReadDescriptor(AttributeData marker, ITypeSymbol target, out Descriptor? descriptor, out string? error)
	{
		descriptor = null;
		error = Validate(marker, target, out string dimension, out ConstraintKind kind, out string? argument, out object? value, out bool isExclusive, out string? exclusiveArgument);
		if (error is not null)
		{
			return false;
		}

		descriptor = new Descriptor(dimension, kind, argument, value, isExclusive, exclusiveArgument);
		return true;
	}

	private static string? Validate(
		AttributeData marker,
		ITypeSymbol target,
		out string dimension,
		out ConstraintKind kind,
		out string? argument,
		out object? value,
		out bool isExclusive,
		out string? exclusiveArgument)
	{
		dimension = "";
		kind = default;
		argument = null;
		value = null;
		isExclusive = false;
		exclusiveArgument = null;

		ImmutableArray<TypedConstant> arguments = marker.ConstructorArguments;
		int offset = arguments.Length - 2;
		if (offset < 0 || arguments[offset].Value is not string dimensionArgument || arguments[offset + 1].Value is not int comparison)
		{
			return "it needs a dimension and a comparison";
		}

		dimension = dimensionArgument;
		if (dimension.Trim().Length == 0)
		{
			return "the dimension is empty";
		}

		if (comparison is < (int)ConstraintKind.Flag or > (int)ConstraintKind.DeniedValues)
		{
			return $"{comparison} isn't a ValidationComparison";
		}

		kind = (ConstraintKind)comparison;
		TypedConstant? valueArgument = null;
		foreach (KeyValuePair<string, TypedConstant> named in marker.NamedArguments)
		{
			switch (named.Key)
			{
				case "Argument":
					argument = named.Value.Value as string;
					break;
				case "Value" when !named.Value.IsNull:
					valueArgument = named.Value;
					break;
				case "IsExclusive":
					isExclusive = named.Value.Value is true;
					break;
				case "ExclusiveArgument":
					exclusiveArgument = named.Value.Value as string;
					break;
			}
		}

		if (!IsValidationAttribute(target))
		{
			return $"'{target.ToDisplayString()}' isn't a ValidationAttribute";
		}

		if (kind == ConstraintKind.Flag)
		{
			return argument is null && valueArgument is null && !isExclusive && exclusiveArgument is null
				? null
				: "a Flag takes no Argument, Value, IsExclusive or ExclusiveArgument";
		}

		if (argument is null && valueArgument is null)
		{
			return $"a {kind} needs an Argument or a Value";
		}

		if (argument is not null && !HasArgument(target, argument, requireBoolean: false))
		{
			return $"'{target.Name}' has no constructor parameter or settable property named '{argument}'";
		}

		bool isBound = IsBound(kind);
		if (valueArgument is { } constant && (value = isBound ? ToBound(constant) : ToValues(constant)) is null)
		{
			return $"the Value of a {kind} must be {(isBound ? "a number" : "an array")}";
		}

		if (!isBound && (isExclusive || exclusiveArgument is not null))
		{
			return "only a LowerBound or an UpperBound can be exclusive";
		}

		return exclusiveArgument is not null && !HasArgument(target, exclusiveArgument, requireBoolean: true)
			? $"'{target.Name}' has no bool constructor parameter or settable property named '{exclusiveArgument}'"
			: null;
	}

	private DescribedAttribute Describe(AttributeData attribute, CancellationToken cancellationToken)
	{
		ImmutableArray<Descriptor>? descriptors = this._descriptors.GetOrAdd(attribute.AttributeClass!.OriginalDefinition, this.ReadDescriptors);
		return new DescribedAttribute(
			attribute,
			GetKey(attribute),
			GetDisplay(attribute, cancellationToken),
			descriptors is { } known ? Evaluate(known, attribute) : null);
	}

	/// <returns>The descriptors, or <c>null</c> if the attribute class isn't (validly) described.</returns>
	private ImmutableArray<Descriptor>? ReadDescriptors(ITypeSymbol type)
	{
		List<AttributeData> own = [.. type.GetAttributes().Where(static attribute => IsMarker(attribute) && attribute.ConstructorArguments.Length == 2)];
		if (own.Count > 0)
		{
			return ReadAll(own, type);
		}

		if (BuiltIns.TryGetValue(type.ToDisplayString(), out ImmutableArray<Descriptor> builtIn))
		{
			return builtIn;
		}

		return this._assemblyDescriptions.TryGetValue(type, out List<AttributeData>? markers) ? ReadAll(markers, type) : null;
	}

	/// <remarks>One invalid description makes the whole class undescribed: half a description would understate it.</remarks>
	private static ImmutableArray<Descriptor>? ReadAll(IEnumerable<AttributeData> markers, ITypeSymbol target)
	{
		ImmutableArray<Descriptor>.Builder descriptors = ImmutableArray.CreateBuilder<Descriptor>();
		foreach (AttributeData marker in markers)
		{
			if (!TryReadDescriptor(marker, target, out Descriptor? descriptor, out _))
			{
				return null;
			}

			descriptors.Add(descriptor!);
		}

		return descriptors.ToImmutable();
	}

	/// <returns>The usage's constraints, or <c>null</c> if an argument it depends on isn't a constant of the right kind.</returns>
	private static ImmutableArray<Constraint>? Evaluate(ImmutableArray<Descriptor> descriptors, AttributeData attribute)
	{
		ImmutableArray<Constraint>.Builder constraints = ImmutableArray.CreateBuilder<Constraint>(descriptors.Length);
		foreach (Descriptor descriptor in descriptors)
		{
			if (descriptor.Kind == ConstraintKind.Flag)
			{
				if (descriptor.UnlessArgument is null || GetArgument(attribute, descriptor.UnlessArgument) is not { Value: true })
				{
					constraints.Add(new Constraint(descriptor.Dimension, descriptor.Kind, 0, IsExclusive: false, []));
				}

				continue;
			}

			object? value = descriptor.Argument is not null && GetArgument(attribute, descriptor.Argument) is { } argument
				? IsBound(descriptor.Kind) ? ToBound(argument) ?? ToRangeLimit(argument, attribute) : ToValues(argument)
				: descriptor.Value;
			bool isExclusive = descriptor.ExclusiveArgument is not null && GetArgument(attribute, descriptor.ExclusiveArgument) is { Value: bool exclusive }
				? exclusive
				: descriptor.IsExclusive;
			switch (value)
			{
				case double bound:
					constraints.Add(new Constraint(descriptor.Dimension, descriptor.Kind, bound, isExclusive, []));
					break;
				case ParsedBound parsed:
					constraints.Add(new Constraint(descriptor.Dimension, descriptor.Kind, parsed.Value, isExclusive, [], parsed.Display));
					break;
				case ImmutableArray<string> values:
					constraints.Add(new Constraint(descriptor.Dimension, descriptor.Kind, 0, IsExclusive: false, values));
					break;
				default:
					return null;
			}
		}

		return constraints.ToImmutable();
	}

	/// <summary>The value given for a named argument, or else for a constructor parameter, called <paramref name="name"/>.</summary>
	private static TypedConstant? GetArgument(AttributeData attribute, string name)
	{
		foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
		{
			if (named.Key == name)
			{
				return named.Value;
			}
		}

		if (attribute.AttributeConstructor is { } constructor)
		{
			for (int i = 0; i < constructor.Parameters.Length && i < attribute.ConstructorArguments.Length; i++)
			{
				if (constructor.Parameters[i].Name == name)
				{
					return attribute.ConstructorArguments[i];
				}
			}
		}

		return null;
	}

	private static bool HasArgument(ITypeSymbol target, string name, bool requireBoolean)
	{
		bool Fits(ITypeSymbol type) => !requireBoolean || type.SpecialType == SpecialType.System_Boolean;

		for (ITypeSymbol? type = target; type is not null; type = type.BaseType)
		{
			foreach (ISymbol member in type.GetMembers(name))
			{
				if (member is IPropertySymbol { IsStatic: false, SetMethod: not null } property && Fits(property.Type)
					|| member is IFieldSymbol { IsStatic: false, IsReadOnly: false, IsConst: false } field && Fits(field.Type))
				{
					return true;
				}
			}
		}

		return target is INamedTypeSymbol named
			&& named.InstanceConstructors.Any(constructor => constructor.Parameters.Any(parameter => parameter.Name == name && Fits(parameter.Type)));
	}

	private static bool IsBound(ConstraintKind kind) => kind is ConstraintKind.LowerBound or ConstraintKind.UpperBound;

	/// <returns>The constant as a boxed <see cref="double"/>, or <c>null</c> if it isn't a number.</returns>
	private static object? ToBound(TypedConstant constant)
		=> constant.Kind == TypedConstantKind.Primitive && constant.Value is IConvertible number and not (bool or char or string)
			? Convert.ToDouble(number, CultureInfo.InvariantCulture)
			: null;

	/// <summary>
	/// Reads one of <c>Range(typeof(T), "minimum", "maximum")</c>'s limits, which RangeAttribute
	/// parses with T's TypeConverter in the current culture, unless
	/// <c>ParseLimitsInInvariantCulture</c> is set.
	/// </summary>
	/// <returns>
	/// The limit, or <c>null</c> if T isn't supported, the value might depend on the culture, or it
	/// can't be represented exactly enough as a <see cref="double"/> to compare.
	/// </returns>
	private static ParsedBound? ToRangeLimit(TypedConstant constant, AttributeData attribute)
	{
		if (constant.Value is not string text
			|| attribute.AttributeClass?.ToDisplayString() != DataAnnotations + "RangeAttribute"
			|| attribute.ConstructorArguments.Length != 3
			|| attribute.ConstructorArguments[0].Value is not ITypeSymbol operandType)
		{
			return null;
		}

		text = text.Trim();
		bool invariant = GetArgument(attribute, "ParseLimitsInInvariantCulture") is { Value: true };
		bool digitsOnly = text.Length > 0 && text.All(static c => c is >= '0' and <= '9');
		CultureInfo culture = CultureInfo.InvariantCulture;
		double? value = operandType.SpecialType switch
		{
			SpecialType.System_SByte => Integer(sbyte.MinValue, sbyte.MaxValue),
			SpecialType.System_Byte => Integer(byte.MinValue, byte.MaxValue),
			SpecialType.System_Int16 => Integer(short.MinValue, short.MaxValue),
			SpecialType.System_UInt16 => Integer(ushort.MinValue, ushort.MaxValue),
			SpecialType.System_Int32 => Integer(int.MinValue, int.MaxValue),
			SpecialType.System_UInt32 => Integer(uint.MinValue, uint.MaxValue),
			SpecialType.System_Int64 => Integer(long.MinValue, long.MaxValue),
			SpecialType.System_UInt64 => Integer(ulong.MinValue, ulong.MaxValue),
			SpecialType.System_Single => (invariant || digitsOnly) && float.TryParse(text, NumberStyles.Float, culture, out float single) ? single : null,
			SpecialType.System_Double => (invariant || digitsOnly) && double.TryParse(text, NumberStyles.Float, culture, out double number) ? number : null,
			SpecialType.System_Decimal => (invariant || digitsOnly) && decimal.TryParse(text, NumberStyles.Number, culture, out decimal money) && SignificantDigits(money) <= 15
				? (double)money
				: null,
			SpecialType.System_DateTime => invariant ? Ticks(ParseDateTime(["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"])?.Ticks) : null,
			_ => operandType.ToDisplayString() switch
			{
				"System.DateOnly" => invariant && ParseDateTime(["yyyy-MM-dd"]) is { } date ? date.Ticks / TimeSpan.TicksPerDay : null,
				"System.TimeOnly" => invariant ? Ticks(ParseDateTime(["HH:mm", "HH:mm:ss"])?.TimeOfDay.Ticks) : null,

				// [-][d.]hh:mm[:ss] is parsed the same in every culture.
				"System.TimeSpan" => System.Text.RegularExpressions.Regex.IsMatch(text, @"^-?([0-9]+\.)?[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?$")
					&& TimeSpan.TryParse(text, culture, out TimeSpan span)
					? Ticks(span.Ticks)
					: null,
				_ => null,
			},
		};
		return value is { } bound && !double.IsNaN(bound) ? new ParsedBound(bound, text) : null;

		// Plain digits mean the same in every culture; a sign might not.
		double? Integer(decimal min, decimal max)
			=> (invariant || digitsOnly)
				&& decimal.TryParse(text, NumberStyles.Integer, culture, out decimal integer)
				&& integer >= min && integer <= max
				&& Math.Abs(integer) <= 9007199254740992m
				? (double)integer
				: null;

		DateTime? ParseDateTime(string[] formats)
			=> System.DateTime.TryParseExact(text, formats, culture, DateTimeStyles.None, out DateTime parsed) ? parsed : null;

		static double? Ticks(long? ticks) => ticks is { } exact && (long)(double)exact == exact ? exact : null;

		static int SignificantDigits(decimal number)
			=> Math.Abs(number).ToString(CultureInfo.InvariantCulture).Replace(".", "").TrimStart('0').TrimEnd('0').Length;
	}

	/// <returns>The array's elements as C# literals, or <c>null</c> if it isn't an array.</returns>
	private static object? ToValues(TypedConstant constant)
		=> constant.Kind == TypedConstantKind.Array && !constant.IsNull
			? constant.Values.Select(static value => value.ToCSharpString()).Distinct().ToImmutableArray()
			: null;

	private static string GetKey(AttributeData attribute)
		=> $"{attribute.AttributeClass!.ToDisplayString()}({string.Join(", ", attribute.ConstructorArguments
			.Select(static argument => argument.ToCSharpString())
			.Concat(attribute.NamedArguments
				.OrderBy(static named => named.Key, StringComparer.Ordinal)
				.Select(static named => $"{named.Key} = {named.Value.ToCSharpString()}")))})";

	private static string GetDisplay(AttributeData attribute, CancellationToken cancellationToken)
	{
		if (attribute.ApplicationSyntaxReference is { } reference)
		{
			return reference.GetSyntax(cancellationToken).ToString();
		}

		string name = attribute.AttributeClass!.Name;
		if (name.EndsWith("Attribute", StringComparison.Ordinal))
		{
			name = name.Substring(0, name.Length - "Attribute".Length);
		}

		string[] arguments = [.. attribute.ConstructorArguments.Select(static argument => argument.ToCSharpString()),
			.. attribute.NamedArguments.Select(static named => $"{named.Key} = {named.Value.ToCSharpString()}")];
		return arguments.Length == 0 ? name : $"{name}({string.Join(", ", arguments)})";
	}

	private static string NormalizePath(string path) => path.Replace('\\', '/').TrimEnd('/') + "/";
}

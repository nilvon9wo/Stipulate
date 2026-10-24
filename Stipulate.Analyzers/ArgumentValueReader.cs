using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>Works out what is known at compile time about the value an operation passes.</summary>
internal sealed class ArgumentValueReader(StaticReadOnlyFields fields)
{
    private const string ListName = "System.Collections.Generic.List<T>";

    private readonly StaticReadOnlyFields _fields = fields;

    /// <param name="value">The value as passed, after any conversion to the parameter's type.</param>
    /// <param name="noun">How the messages name it, before what it is: "The argument", "The default value".</param>
    /// <param name="cancellationToken">Cancels reading a <c>static readonly</c> field's initializer.</param>
    /// <returns>What is known, or <c>null</c> if nothing is.</returns>
    public ArgumentValue? Read(IOperation value, string noun, CancellationToken cancellationToken)
    {
        IOperation passed = this.Resolve(Unwrap(value), cancellationToken);
        string written = value.Syntax.ToString();
        return passed.ConstantValue is { HasValue: true, Value: var constant }
            ? ArgumentValue.OfConstant(Describe(noun, written, constant), passed.Type, constant)
            : ReadCollection(passed, $"{noun} {written}");
    }

    private static ArgumentValue? ReadCollection(IOperation passed, string description)
        => CountElements(passed) is { } count
            ? ArgumentValue.OfCollection(description, count)
            : null;

    /// <summary>
    /// The array or collection the compiler builds from a call's <c>params</c> arguments, or
    /// <c>null</c> if the number of elements isn't known.
    /// </summary>
    public static ArgumentValue? ReadParams(IOperation value)
    {
        string collection = value.Type is IArrayTypeSymbol
            ? "array"
            : "collection";
        return CountElements(Unwrap(value)) is { } count
            ? ArgumentValue.OfCollection($"The params {collection} of {count} {Pluralize("argument", count)}", count)
            : null;
    }

    private static string Pluralize(string noun, int count)
        => count == 1
            ? noun
            : noun + "s";

    /// <summary>
    /// A <c>static readonly</c> field's initial value, if nothing can change it: a constant, or an
    /// array, whose length is fixed. Not a field set from another field, whose value depends on
    /// which is initialized first, nor a collection that can grow, such as a <c>List&lt;T&gt;</c>.
    /// Otherwise, <paramref name="passed"/> itself.
    /// </summary>
    private IOperation Resolve(IOperation passed, CancellationToken cancellationToken)
        => passed is IFieldReferenceOperation { Field: var field }
            && (field.IsStatic, field.IsReadOnly) is (true, true)
            && this._fields.FindInitialValue(field, cancellationToken) is { } initial
            && Unwrap(initial) is var value
            && (value.ConstantValue.HasValue || value.Type is IArrayTypeSymbol)
            ? value
            : passed;

    /// <summary>
    /// Looks through the conversions that box a value, make it nullable, or change only its static
    /// type, and the one that makes a collection expression its collection: the value they pass is
    /// still the one written. (A conversion of a constant to another constant is the constant.)
    /// </summary>
    private static IOperation Unwrap(IOperation value)
    {
        IOperation current = value;
        while (!current.ConstantValue.HasValue
            && current is IConversionOperation conversion
            && IsTransparent(conversion))
        {
            current = conversion.Operand;
        }

        return current;
    }

    /// <remarks>A user-defined conversion, whose result isn't known, is none of these.</remarks>
    private static bool IsTransparent(IConversionOperation conversion)
    {
        Conversion kind = conversion.GetConversion();
        return kind.IsBoxing || kind.IsNullable || kind.IsReference || kind.IsCollectionExpression;
    }

    /// <summary>
    /// The elements of a one-dimensional array created here, or of a collection expression that makes
    /// an array or a <c>List&lt;T&gt;</c> without spreading another collection; otherwise <c>null</c>.
    /// Other collection types can drop or merge elements, as a set does.
    /// </summary>
    private static int? CountElements(IOperation passed) => passed switch
    {
        IArrayCreationOperation { DimensionSizes.Length: 1, Initializer: { } initializer } =>
            initializer.ElementValues.Length,
        IArrayCreationOperation { DimensionSizes: [{ ConstantValue: { HasValue: true, Value: int size } }] } => size,
        ICollectionExpressionOperation collection when IsCountable(collection) => collection.Elements.Length,
        _ => null,
    };

    private static bool IsCountable(ICollectionExpressionOperation collection)
        => IsArrayOrList(collection.Type!) && !collection.Elements.Any(static element => element is ISpreadOperation);

    /// <param name="type">A collection expression's type: one passed as an argument has one, its parameter's.</param>
    private static bool IsArrayOrList(ITypeSymbol type)
        => type is IArrayTypeSymbol || type.OriginalDefinition.ToDisplayString() == ListName;

    /// <summary>
    /// The value as written, and as a literal as well if it is written otherwise, as a constant's
    /// name is.
    /// </summary>
    private static string Describe(string noun, string written, object? constant)
    {
        string literal = SymbolDisplay.FormatPrimitive(constant!, quoteStrings: true, useHexadecimalNumbers: false);
        return literal == written
            ? $"{noun} {written}"
            : $"{noun} {written} ({literal})";
    }
}

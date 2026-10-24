using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>
/// The initial values of the <c>static readonly</c> fields declared in one compilation: the value
/// such a field has whenever it is passed, unless its type's static constructor can set another.
/// </summary>
/// <remarks>
/// A field's initializer may be in another syntax tree than the call that passes the field, so it
/// is bound with that tree's semantic model; each field once.
/// </remarks>
internal sealed class StaticReadOnlyFields(Compilation compilation)
{
    private readonly Compilation _compilation = compilation;
    private readonly ConcurrentDictionary<IFieldSymbol, IOperation?> _initialValues =
        new(SymbolEqualityComparer.Default);

    /// <param name="field">A <c>static readonly</c> field.</param>
    /// <param name="cancellationToken">Cancels binding the initializer and the static constructors.</param>
    /// <returns>
    /// The field's initializer, after its conversion to the field's type; or <c>null</c> if the field
    /// isn't declared here with an initializer, or a static constructor of its type mentions it.
    /// </returns>
    public IOperation? FindInitialValue(IFieldSymbol field, CancellationToken cancellationToken)
        => this._initialValues.GetOrAdd(field, declared => this.ReadInitialValue(declared, cancellationToken));

    /// <remarks>
    /// A field declared here has one declarator, which an initializer always binds to a field
    /// initializer in; a field from another assembly has none.
    /// </remarks>
    private IOperation? ReadInitialValue(IFieldSymbol field, CancellationToken cancellationToken)
    {
        EqualsValueClauseSyntax? initializer = field.DeclaringSyntaxReferences
            .Select(reference => ((VariableDeclaratorSyntax)reference.GetSyntax(cancellationToken)).Initializer)
            .FirstOrDefault();
        return initializer is not null && !this.IsMentionedInStaticConstructor(field, cancellationToken)
            ? ((IFieldInitializerOperation)this.Bind(initializer, cancellationToken)!).Value
            : null;
    }

    /// <summary>
    /// Whether a static constructor of the field's type refers to it at all: only there can a
    /// <c>static readonly</c> field be set again, or passed by reference to be.
    /// </summary>
    private bool IsMentionedInStaticConstructor(IFieldSymbol field, CancellationToken cancellationToken)
        => field.ContainingType.StaticConstructors
            .SelectMany(static constructor => constructor.DeclaringSyntaxReferences)
            .Select(reference => this.Bind(reference.GetSyntax(cancellationToken), cancellationToken))
            .Any(body => body is not null && Mentions(body, field));

    private static bool Mentions(IOperation body, IFieldSymbol field)
        => body.Descendants()
            .OfType<IFieldReferenceOperation>()
            .Any(reference => SymbolEqualityComparer.Default.Equals(reference.Field, field));

#pragma warning disable RS1030 // The field's declaration may be in another tree than the operation analyzed.
    private IOperation? Bind(SyntaxNode node, CancellationToken cancellationToken)
        => this._compilation.GetSemanticModel(node.SyntaxTree).GetOperation(node, cancellationToken);
#pragma warning restore RS1030
}

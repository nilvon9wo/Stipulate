using System;

namespace Net.NowhereAtAll.Stipulate.Contracts;

/// <summary>
/// Shows the parameter's value in the message of the exception Stipulate throws when one of the
/// parameter's validation attributes rejects it.
/// </summary>
/// <remarks>
/// <para>
/// Without it, the message shows only what can't be a secret: a number, an enum, a date or a
/// <c>bool</c> as it is, but a string by its length (and whether it is empty or whitespace), a
/// collection by its number of elements, and any other object by its type. Exception messages end
/// up in logs, telemetry and error responses; a password or an email address shouldn't.
/// </para>
/// <para>
/// Apply it where the value is safe to show and helps to see: a string, for example, that
/// <c>[RegularExpression]</c> rejected. It changes only the message, not what is accepted, so the
/// analyzer doesn't compare it with a contract's.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public void Order([RegularExpression("^[A-Z]{3}-[0-9]{4}$"), ShowValueInMessage] string sku) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ShowValueInMessageAttribute : Attribute;

namespace Net.NowhereAtAll.Stipulate.Analyzers;

/// <summary>What an ST3001 message says about the calls that throw because of the value.</summary>
/// <param name="ThrowingCalls">"this call", or every call that leaves a default argument out.</param>
/// <param name="Receiver">
/// The object the call is made on, where its type is certain; see <see cref="ConcreteReceiver"/>.
/// </param>
internal sealed record Consequence(string ThrowingCalls, ConcreteReceiver? Receiver);

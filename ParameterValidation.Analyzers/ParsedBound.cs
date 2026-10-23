namespace Net.NowhereAtAll.ParameterValidation.Analyzers;

/// <summary>A bound read from a string, such as <c>Range(typeof(DateTime), "2020-01-01", ...)</c>'s.</summary>
/// <param name="Display">The string as written.</param>
internal sealed record ParsedBound(double Value, string Display);

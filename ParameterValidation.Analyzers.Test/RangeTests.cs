using static ParameterValidation.Analyzers.Test.AnalyzerVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// <c>Range(typeof(T), "minimum", "maximum")</c>, whose limits are compared when they can't depend
/// on the culture and are exact, and otherwise only with identical usages.
/// </summary>
public class RangeTests
{
	[Fact]
	public Task IntegerStrings_CompareWithNumbers() => VerifyAsync("""
		public interface IScale { void Set([Range(1, 10)] int a, [Range(typeof(int), "1", "10")] int b, [Range(typeof(int), "1", "10")] int c); }
		public class Scale : IScale
		{
			public void Set([Range(typeof(int), "0", "10")] int a, [Range(0, 10)] int b, [{|PV1001:Range(typeof(int), "2", "10")|}] int c) { }
		}
		""");

	[Fact]
	public Task IdenticalStrings_AreAllowedWhateverTheType() => VerifyAsync("""
		public interface IScale { void Set([Range(typeof(System.DateTime), "1/1/2020", "1/1/2030")] System.DateTime when); }
		public class Scale : IScale
		{
			public void Set([Range(typeof(System.DateTime), "1/1/2020", "1/1/2030")] System.DateTime when) { }
		}
		""");

	[Fact]
	public Task SignedIntegers_NeedTheInvariantCulture() => VerifyAsync("""
		public interface IScale
		{
			void Set(
				[Range(typeof(int), "-5", "5")] int a,
				[Range(typeof(int), "-5", "5", ParseLimitsInInvariantCulture = true)] int b);
		}
		public class Scale : IScale
		{
			public void Set(
				[{|PV1005:Range(typeof(int), "-4", "5")|}] int a,
				[{|PV1001:Range(typeof(int), "-4", "5", ParseLimitsInInvariantCulture = true)|}] int b) { }
		}
		""");

	[Fact]
	public Task Fractions_NeedTheInvariantCulture() => VerifyAsync("""
		public interface IScale
		{
			void Set(
				[Range(typeof(double), "0.5", "10")] double a,
				[Range(typeof(double), "0.5", "10", ParseLimitsInInvariantCulture = true)] double b,
				[Range(typeof(decimal), "0.5", "10", ParseLimitsInInvariantCulture = true)] decimal c);
		}
		public class Scale : IScale
		{
			public void Set(
				[{|PV1005:Range(typeof(double), "0.25", "10")|}] double a,
				[Range(typeof(double), "0.25", "10", ParseLimitsInInvariantCulture = true)] double b,
				[{|PV1001:Range(typeof(decimal), "0.75", "10", ParseLimitsInInvariantCulture = true)|}] decimal c) { }
		}
		""");

	[Fact]
	public Task InexactLimits_AreNotCompared() => VerifyAsync("""
		public interface IScale
		{
			void Set(
				[Range(typeof(long), "0", "9007199254740992")] long a,
				[Range(typeof(decimal), "0", "2", ParseLimitsInInvariantCulture = true)] decimal b,
				[Range(typeof(byte), "0", "200")] byte c);
		}
		public class Scale : IScale
		{
			public void Set(
				[{|PV1005:Range(typeof(long), "0", "9007199254740993")|}] long a,
				[{|PV1005:Range(typeof(decimal), "0", "1.0000000000000001", ParseLimitsInInvariantCulture = true)|}] decimal b,
				[{|PV1005:Range(typeof(byte), "0", "300")|}] byte c) { }
		}
		""");

	[Fact]
	public Task Dates_AreComparedOnlyInTheInvariantCulture() => VerifyAsync("""
		public interface IDiary
		{
			void Write(
				[Range(typeof(System.DateTime), "2020-01-01", "2030-12-31")] System.DateTime a,
				[Range(typeof(System.DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] System.DateTime b,
				[Range(typeof(System.DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] System.DateTime c);
		}
		public class Diary : IDiary
		{
			public void Write(
				[{|PV1005:Range(typeof(System.DateTime), "2019-01-01", "2030-12-31")|}] System.DateTime a,
				[Range(typeof(System.DateTime), "2019-01-01", "2030-12-31T12:00:00", ParseLimitsInInvariantCulture = true)] System.DateTime b,
				[{|PV1001:Range(typeof(System.DateTime), "2020-01-01", "2030-12-30 23:59:59", ParseLimitsInInvariantCulture = true)|}] System.DateTime c) { }
		}
		""");

	[Fact]
	public async Task Dates_AppearAsWrittenInTheMessage()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + """
			public interface IDiary { void Write([Range(typeof(System.DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] System.DateTime when); }
			public class Diary : IDiary
			{
				public void Write([Range(typeof(System.DateTime), "2021-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] System.DateTime when) { }
			}
			""").ConfigureAwait(true));

		Assert.EndsWith("requires Value >= 2021-01-01, but the contract accepts Value >= 2020-01-01", diagnostic.GetMessage(), StringComparison.Ordinal);
	}

	[Fact]
	public Task DateOnlyAndTimeOnly_AreCompared() => VerifyAsync("""
		public interface IDiary
		{
			void Write(
				[Range(typeof(System.DateOnly), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] System.DateOnly day,
				[Range(typeof(System.TimeOnly), "09:00", "17:00", ParseLimitsInInvariantCulture = true)] System.TimeOnly time);
		}
		public class Diary : IDiary
		{
			public void Write(
				[{|PV1001:Range(typeof(System.DateOnly), "2020-01-02", "2030-12-31", ParseLimitsInInvariantCulture = true)|}] System.DateOnly day,
				[Range(typeof(System.TimeOnly), "08:30", "17:00:00", ParseLimitsInInvariantCulture = true)] System.TimeOnly time) { }
		}
		""");

	[Fact]
	public Task TimeSpans_AreComparedInAnyCulture() => VerifyAsync("""
		public interface ITimer { void Start([Range(typeof(System.TimeSpan), "00:00:01", "1.00:00")] System.TimeSpan a, [Range(typeof(System.TimeSpan), "00:00:01", "1.00:00")] System.TimeSpan b); }
		public class Timer : ITimer
		{
			public void Start([Range(typeof(System.TimeSpan), "00:00:00", "2.00:00")] System.TimeSpan a, [{|PV1001:Range(typeof(System.TimeSpan), "00:00:01", "12:00")|}] System.TimeSpan b) { }
		}
		""");
}

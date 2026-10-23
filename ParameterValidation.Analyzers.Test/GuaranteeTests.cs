using static ParameterValidation.Analyzers.Test.AnalyzerVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// What an implementation hands back (a property's value, an out or ref parameter) must satisfy
/// everything its contract's attributes promise.
/// </summary>
public class GuaranteeTests
{
	private const string Sized = """
		public interface ISized
		{
			[Range(1, 10)] int Size { get; }
		}

		""";

	[Fact]
	public Task GetOnlyProperty_StrongerAttribute_IsAllowed() => VerifyAsync(Sized + """
		public class Sized : ISized
		{
			[Range(2, 5)] public int Size => 3;
		}
		""");

	[Fact]
	public Task GetOnlyProperty_WeakerAttribute_IsReported() => VerifyAsync(Sized + """
		public class Sized : ISized
		{
			[Range(1, 20)] public int {|PV1007:Size|} => 3;
		}
		""");

	[Fact]
	public async Task GetOnlyProperty_MessageExplainsWhy()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Sized + """
			public class Sized : ISized
			{
				[Range(1, 20)] public int Size => 3;
			}
			""").ConfigureAwait(true));

		Assert.Equal(
			"Property 'Sized.Size' weakens the guarantee of 'ISized.Size': "
				+ "[Range(1, 10)] requires Value <= 10, but the implementation accepts Value <= 20",
			diagnostic.GetMessage());
	}

	[Fact]
	public async Task GetOnlyProperty_MissingAttribute_IsReportedAsWeakened()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Sized + """
			public class Sized : ISized
			{
				public int Size => 3;
			}
			""").ConfigureAwait(true));

		Assert.Equal("PV1007", diagnostic.Id);
		Assert.Equal(
			"Property 'Sized.Size' weakens the guarantee of 'ISized.Size': it doesn't restate [Range(1, 10)]",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task GetOnlyProperty_NullableImplementation_WeakensRequired() => VerifyAsync("""
		public interface INamed { [Required] string Name { get; } }
		public class Named : INamed
		{
			public string? {|PV1007:Name|} => null;
		}
		""");

	[Fact]
	public Task GetOnlyProperty_ImplementationWithSetter_IsHeldToTheGuarantee() => VerifyAsync(Sized + """
		public class Sized : ISized
		{
			[Range(0, 10)] public int {|PV1007:Size|} { get; set; }
		}
		""");

	[Fact]
	public Task GetSetProperty_MustMatchItsContract() => VerifyAsync("""
		public interface IWidget { [Range(1, 10)] int A { get; set; } [Range(1, 10)] int B { get; set; } [Range(1, 10)] int C { get; set; } }
		public class Widget : IWidget
		{
			[Range(1, 10)] public int A { get; set; }
			[Range(0, 10)] public int {|PV1007:B|} { get; set; }
			[{|PV1001:Range(2, 10)|}] public int C { get; set; }
		}
		""");

	[Fact]
	public Task GetSetProperty_DroppedUndescribedAttribute_IsReportedOnce() => VerifyAsync("""
		public sealed class EvenAttribute : ValidationAttribute { }
		public interface IWidget { [Even] int Size { get; set; } }
		public class Widget : IWidget
		{
			public int {|PV1007:Size|} { get; set; }
		}
		""");

	[Fact]
	public Task GetSetProperty_UndescribedAttributeWithOtherArguments_IsReportedOnce() => VerifyAsync("""
		public sealed class DivisibleAttribute(int divisor) : ValidationAttribute { public int Divisor => divisor; }
		public interface IWidget { [Divisible(2)] int Size { get; set; } }
		public class Widget : IWidget
		{
			[{|PV1002:Divisible(4)|}] public int Size { get; set; }
		}
		""");

	[Fact]
	public Task OutParameter_StrongerAttribute_IsAllowed() => VerifyAsync("""
		public interface IReader { bool TryRead([MinLength(1)] out string text); }
		public class Reader : IReader
		{
			public bool TryRead([MinLength(2)] out string text) { text = "ab"; return true; }
		}
		""");

	[Fact]
	public Task OutParameter_WeakerOrMissingAttribute_IsReported() => VerifyAsync("""
		public interface IReader { bool TryRead([MinLength(1)] out string a, [MinLength(1)] out string b); }
		public class Reader : IReader
		{
			public bool TryRead([MinLength(0)] out string {|PV1007:a|}, out string {|PV1007:b|}) { a = b = ""; return true; }
		}
		""");

	[Fact]
	public Task RefParameter_MustMatchItsContract() => VerifyAsync("""
		public interface IEditor { void Edit([MinLength(1)] ref string a, [MinLength(1)] ref string b, [MinLength(1)] ref string c); }
		public class Editor : IEditor
		{
			public void Edit([MinLength(1)] ref string a, [MinLength(0)] ref string {|PV1007:b|}, [{|PV1001:MinLength(2)|}] ref string c) { }
		}
		""");

	[Fact]
	public Task InheritedGetOnlyProperty_IsReportedOnTheClass() => VerifyAsync(Sized + """
		public class Base
		{
			[Range(1, 20)] public int Size => 3;
		}
		public class {|PV1007:Derived|} : Base, ISized { }
		""");

	[Fact]
	public Task GetOnlyOverride_IsChecked() => VerifyAsync("""
		public abstract class Base { [Range(1, 10)] public abstract int Size { get; } }
		public class Derived : Base
		{
			[Range(1, 20)] public override int {|PV1007:Size|} => 3;
		}
		""");
}

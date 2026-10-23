using static ParameterValidation.Analyzers.Test.AnalyzerVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>How the built-in DataAnnotations attributes compare, and which methods are compared.</summary>
public class ContractTests
{
	private const string Multiplier = """
		public interface IMultiplier
		{
			int[] Multiply([MinLength(1)] int[] array);
		}

		""";

	[Fact]
	public Task SameAttribute_IsAllowed() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public int[] Multiply([MinLength(1)] int[] array) => array;
		}
		""");

	[Fact]
	public Task WeakerAttribute_IsAllowed() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public int[] Multiply([MinLength(0)] int[] array) => array;
		}
		""");

	[Fact]
	public Task StrongerAttribute_IsReported() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public int[] Multiply([{|PV1001:MinLength(2)|}] int[] array) => array;
		}
		""");

	[Fact]
	public async Task StrongerAttribute_MessageExplainsWhy()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Multiplier + """
			public class Multiplier : IMultiplier
			{
				public int[] Multiply([MinLength(2)] int[] array) => array;
			}
			""").ConfigureAwait(true));

		Assert.Equal(
			"Parameter 'array' of 'Multiplier.Multiply(int[])' strengthens the precondition of 'IMultiplier.Multiply(int[])': "
				+ "[MinLength(2)] requires Length >= 2, but the contract accepts Length >= 1",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task MissingAttribute_IsReported() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public int[] Multiply(int[] {|PV1003:array|}) => array;
		}
		""");

	[Fact]
	public Task ParametersAreMatchedByPosition() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public int[] Multiply([{|PV1001:MinLength(2)|}] int[] values) => values;
		}
		""");

	[Fact]
	public Task AddedRequired_IsReported() => VerifyAsync("""
		public interface IStore { void Add(string item); }
		public class Store : IStore
		{
			public void Add([{|PV1001:Required|}] string item) { }
		}
		""");

	[Fact]
	public Task DroppedRequired_IsReported() => VerifyAsync("""
		public interface IStore { void Add([Required] string item); }
		public class Store : IStore
		{
			public void Add(string {|PV1003:item|}) { }
		}
		""");

	[Fact]
	public Task DroppedRequired_OnNullableParameter_IsExplicit() => VerifyAsync("""
		public interface IStore { void Add([Required] string item, [Required] int? count); }
		public class Store : IStore
		{
			public void Add(string? item, int? count) { }
		}
		""");

	[Fact]
	public Task Required_AllowingEmptyStrings_IsWeaker() => VerifyAsync("""
		public interface IStore { void Add([Required] string item, [Required(AllowEmptyStrings = true)] string name); }
		public class Store : IStore
		{
			public void Add([Required(AllowEmptyStrings = true)] string item, [{|PV1001:Required|}] string name) { }
		}
		""");

	[Fact]
	public Task LengthAttributes_AreComparedAcrossClasses() => VerifyAsync("""
		public interface INamer { void Name([Length(1, 10)] string first, [Length(1, 10)] string last, [StringLength(10)] string nick); }
		public class Namer : INamer
		{
			public void Name([MinLength(1), MaxLength(20)] string first, [MinLength(1), {|PV1001:MaxLength(5)|}] string last, [StringLength(20)] string nick) { }
		}
		""");

	[Fact]
	public Task EachDroppedBound_MustBeExplicit() => VerifyAsync("""
		public interface INamer { void Name([Length(1, 10)] string a, [Length(1, 10)] string b, [Length(1, 10)] string c); }
		public class Namer : INamer
		{
			public void Name([MinLength(1)] string {|PV1003:a|}, [MinLength(1), MaxLength] string b, [MinLength(0), MaxLength(10)] string c) { }
		}
		""");

	[Fact]
	public Task UnboundedLength_IsNoConstraint() => VerifyAsync("""
		public interface INamer { void Name(string first, string last); }
		public class Namer : INamer
		{
			public void Name([MaxLength] string first, [{|PV1001:StringLength(10)|}] string last) { }
		}
		""");

	[Fact]
	public Task Range_ComparesBoundsAndExclusivity() => VerifyAsync("""
		public interface IScale { void Set([Range(1, 10)] int a, [Range(1, 10)] int b, [Range(1, 10)] int c, [Range(1, 10)] int d); }
		public class Scale : IScale
		{
			public void Set(
				[Range(0, 10)] int a,
				[{|PV1001:Range(2, 10)|}] int b,
				[Range(1.0, 10.0)] int c,
				[{|PV1001:Range(1, 10, MinimumIsExclusive = true)|}] int d) { }
		}
		""");

	[Fact]
	public Task AllowedAndDeniedValues_CompareAsSets() => VerifyAsync("""
		public interface IPicker
		{
			void Pick([AllowedValues(1, 2)] int a, [AllowedValues(1, 2)] int b, [DeniedValues(1, 2)] int c, [DeniedValues(1, 2)] int d, [AllowedValues(1, 2)] int e);
		}
		public class Picker : IPicker
		{
			public void Pick(
				[AllowedValues(1, 2, 3)] int a,
				[{|PV1001:AllowedValues(1)|}] int b,
				[DeniedValues(1)] int c,
				[{|PV1001:DeniedValues(1, 2, 3)|}] int d,
				[DeniedValues(3)] int e) { }
		}
		""");

	[Fact]
	public Task ExplicitImplementation_IsChecked() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			int[] IMultiplier.Multiply([{|PV1001:MinLength(2)|}] int[] array) => array;
		}
		""");

	[Fact]
	public Task StaticAbstractImplementation_IsChecked() => VerifyAsync("""
		public interface IParser { static abstract int Parse([MinLength(1)] string text); }
		public class Parser : IParser
		{
			public static int Parse([{|PV1001:MinLength(2)|}] string text) => 0;
		}
		""");

	[Fact]
	public Task GenericInterface_IsChecked() => VerifyAsync("""
		public interface IStore<T> { void Add([Required] T item); }
		public class Store : IStore<string>
		{
			public void Add(string {|PV1003:item|}) { }
		}
		""");

	[Fact]
	public Task Override_IsChecked() => VerifyAsync("""
		public abstract class Base { public abstract void Add([MinLength(1)] int[] items); }
		public class Derived : Base
		{
			public override void Add([{|PV1001:MinLength(2)|}] int[] items) { }
		}
		""");

	[Fact]
	public Task Override_OfInterfaceImplementation_IsReportedOnce() => VerifyAsync(Multiplier + """
		public class Multiplier : IMultiplier
		{
			public virtual int[] Multiply([MinLength(1)] int[] array) => array;
		}
		public class Derived : Multiplier
		{
			public override int[] Multiply([{|PV1001:MinLength(2)|}] int[] array) => array;
		}
		""");

	[Fact]
	public Task InheritedImplementation_IsReportedOnTheClass() => VerifyAsync(Multiplier + """
		public class Base
		{
			public int[] Multiply([MinLength(2)] int[] array) => array;
		}
		public class {|PV1001:Derived|} : Base, IMultiplier { }
		""");

	[Fact]
	public Task InheritedImplementation_FromImplementingBase_IsCheckedOnTheBase() => VerifyAsync(Multiplier + """
		public class Base : IMultiplier
		{
			public int[] Multiply([{|PV1001:MinLength(2)|}] int[] array) => array;
		}
		public class Derived : Base, IMultiplier { }
		""");

	[Fact]
	public Task UnrelatedMethods_AreIgnored() => VerifyAsync(Multiplier + """
		public class Multiplier
		{
			public int[] Multiply([MinLength(2)] int[] array) => array;
		}
		""");

	[Fact]
	public Task UndescribedBuiltInAttribute_IsReportedAsExternal() => VerifyAsync("""
		public interface IMailer { void Send([EmailAddress] string to, string cc); }
		public class Mailer : IMailer
		{
			public void Send([EmailAddress] string to, [{|PV1005:EmailAddress|}] string cc) { }
		}
		""");
}

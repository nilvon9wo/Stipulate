using static ParameterValidation.Analyzers.Test.AnalyzerVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>Which properties and indexers are compared, and what's compared on them.</summary>
public class PropertyTests
{
	private const string Grid = """
		public interface IGrid
		{
			int this[[Range(0, 9)] int index] { get; }
		}

		""";

	private const string Widget = """
		public interface IWidget
		{
			[Range(1, 10)] int Size { get; set; }
		}

		""";

	[Fact]
	public Task IndexerParameter_SameAttribute_IsAllowed() => VerifyAsync(Grid + """
		public class Grid : IGrid
		{
			public int this[[Range(0, 9)] int index] => index;
		}
		""");

	[Fact]
	public Task IndexerParameter_StrongerAttribute_IsReportedOnce() => VerifyAsync("""
		public interface IGrid { int this[[Range(0, 9)] int index] { get; set; } }
		public class Grid : IGrid
		{
			public int this[[{|PV1001:Range(0, 5)|}] int index] { get => index; set { } }
		}
		""");

	[Fact]
	public Task IndexerParameter_MissingAttribute_IsReported() => VerifyAsync(Grid + """
		public class Grid : IGrid
		{
			public int this[int {|PV1003:index|}] => index;
		}
		""");

	[Fact]
	public async Task IndexerParameter_MessageNamesTheIndexer()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Grid + """
			public class Grid : IGrid
			{
				public int this[[Range(0, 5)] int index] => index;
			}
			""").ConfigureAwait(true));

		Assert.Equal(
			"Parameter 'index' of 'Grid.this[int]' strengthens the precondition of 'IGrid.this[int]': "
				+ "[Range(0, 5)] requires Value <= 5, but the contract accepts Value <= 9",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task ExplicitIndexerImplementation_IsChecked() => VerifyAsync(Grid + """
		public class Grid : IGrid
		{
			int IGrid.this[[{|PV1001:Range(1, 9)|}] int index] => index;
		}
		""");

	[Fact]
	public Task IndexerOverride_IsChecked() => VerifyAsync("""
		public abstract class Base { public abstract int this[[MinLength(1)] string key] { get; } }
		public class Derived : Base
		{
			public override int this[[{|PV1001:MinLength(2)|}] string key] => 0;
		}
		""");

	[Fact]
	public Task SettableProperty_SameAttribute_IsAllowed() => VerifyAsync(Widget + """
		public class Widget : IWidget
		{
			[Range(1, 10)] public int Size { get; set; }
		}
		""");

	[Fact]
	public Task SetOnlyProperty_WeakerAttribute_IsAllowed() => VerifyAsync("""
		public interface IWidget { [Range(1, 10)] int Size { set; } }
		public class Widget : IWidget
		{
			[Range(0, 10)] public int Size { set { } }
		}
		""");

	[Fact]
	public Task SettableProperty_StrongerAttribute_IsReported() => VerifyAsync(Widget + """
		public class Widget : IWidget
		{
			[{|PV1001:Range(1, 5)|}] public int Size { get; set; }
		}
		""");

	[Fact]
	public async Task SettableProperty_MessageNamesTheProperty()
	{
		Microsoft.CodeAnalysis.Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Widget + """
			public class Widget : IWidget
			{
				[Range(1, 5)] public int Size { get; set; }
			}
			""").ConfigureAwait(true));

		Assert.Equal(
			"Property 'Widget.Size' strengthens the precondition of 'IWidget.Size': "
				+ "[Range(1, 5)] requires Value <= 5, but the contract accepts Value <= 10",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task SetOnlyProperty_MissingAttribute_IsReported() => VerifyAsync("""
		public interface IWidget { [Range(1, 10)] int Size { set; } }
		public class Widget : IWidget
		{
			public int {|PV1003:Size|} { set { } }
		}
		""");

	[Fact]
	public Task SettableProperty_AddedRequired_IsReported() => VerifyAsync("""
		public interface INamed { string Name { get; set; } }
		public class Named : INamed
		{
			[{|PV1001:Required|}] public string Name { get; set; } = "";
		}
		""");

	[Fact]
	public Task SetOnlyProperty_DroppedRequired_OnNullableProperty_IsExplicit() => VerifyAsync("""
		public interface INamed { [Required] string Name { set; } }
		public class Named : INamed
		{
			public string? Name { set { } }
		}
		""");

	[Fact]
	public Task InitOnlyProperty_IsChecked() => VerifyAsync("""
		public interface IWidget { [Range(1, 10)] int Size { get; init; } }
		public class Widget : IWidget
		{
			[{|PV1001:Range(2, 10)|}] public int Size { get; init; }
		}
		""");

	[Fact]
	public Task GetOnlyContract_AllowsNarrowerValues() => VerifyAsync("""
		public interface IWidget { [Range(1, 10)] int Size { get; } }
		public class Widget : IWidget
		{
			[Range(1, 5)] public int Size { get; set; }
		}
		""");

	[Fact]
	public Task SettableIndexer_ValueIsChecked() => VerifyAsync("""
		public interface IGrid { [Range(0, 100)] int this[int index] { get; set; } }
		public class Grid : IGrid
		{
			[{|PV1001:Range(0, 50)|}] public int this[int index] { get => 0; set { } }
		}
		""");

	[Fact]
	public Task ExplicitPropertyImplementation_IsChecked() => VerifyAsync(Widget + """
		public class Widget : IWidget
		{
			[{|PV1001:Range(1, 5)|}] int IWidget.Size { get; set; }
		}
		""");

	[Fact]
	public Task PropertyOverride_IsChecked() => VerifyAsync("""
		public abstract class Base { [Range(1, 10)] public abstract int Size { get; set; } }
		public class Derived : Base
		{
			[{|PV1001:Range(1, 5)|}] public override int Size { get; set; }
		}
		""");

	[Fact]
	public Task PropertyOverride_OfInterfaceImplementation_IsReportedOnce() => VerifyAsync(Widget + """
		public class Widget : IWidget
		{
			[Range(1, 10)] public virtual int Size { get; set; }
		}
		public class Derived : Widget
		{
			[{|PV1001:Range(1, 5)|}] public override int Size { get; set; }
		}
		""");

	[Fact]
	public Task InheritedProperty_IsReportedOnTheClass() => VerifyAsync(Widget + """
		public class Base
		{
			[Range(1, 5)] public int Size { get; set; }
		}
		public class {|PV1001:Derived|} : Base, IWidget { }
		""");

	[Fact]
	public Task InheritedIndexer_IsReportedOnTheClass() => VerifyAsync(Grid + """
		public class Base
		{
			public int this[[Range(0, 5)] int index] => index;
		}
		public class {|PV1001:Derived|} : Base, IGrid { }
		""");

	[Fact]
	public Task UnrelatedProperties_AreIgnored() => VerifyAsync(Widget + """
		public class Widget
		{
			[Range(1, 5)] public int Size { get; set; }
		}
		""");
}

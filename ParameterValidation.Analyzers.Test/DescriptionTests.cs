using Microsoft.CodeAnalysis;
using static ParameterValidation.Analyzers.Test.AnalyzerVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>
/// Custom validation attributes: comparable once described with <c>[ComparableValidation]</c>,
/// and otherwise reported according to whether the developer can describe them.
/// </summary>
public class DescriptionTests
{
	private const string Even = """
		public sealed class EvenAttribute(int divisor) : ValidationAttribute
		{
			public int Divisor => divisor;
			public override bool IsValid(object? value) => value is not int number || number % divisor == 0;
		}

		""";

	private const string Described = """
		[ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]
		public sealed class NotEmptyAttribute : ValidationAttribute
		{
			public override bool IsValid(object? value) => value is not System.Collections.ICollection { Count: 0 };
		}

		[ComparableValidation(ValidationDimensions.Value, ValidationComparison.LowerBound, Argument = "minimum")]
		public sealed class AtLeastAttribute(int minimum) : ValidationAttribute
		{
			public override bool IsValid(object? value) => value is not int number || number >= minimum;
		}

		""";

	private const string Vendor = """
		namespace Vendor;

		public sealed class EvenAttribute(int divisor) : ValidationAttribute
		{
			public override bool IsValid(object? value) => value is not int number || number % divisor == 0;
		}

		public sealed class NonEmptyAttribute : ValidationAttribute
		{
			public override bool IsValid(object? value) => value is not System.Collections.ICollection { Count: 0 };
		}
		""";

	private const string VendorEvenUsage = """
		public interface ICounter { void Count([Vendor.Even(2)] int a, [Vendor.Even(2)] int b); }
		public class Counter : ICounter
		{
			public void Count([Vendor.Even(2)] int a, [{|ID:Vendor.Even(4)|}] int b) { }
		}
		""";

	[Fact]
	public Task UndescribedAttribute_UsedIdentically_IsAllowed() => VerifyAsync(Even + """
		public interface ICounter { void Count([Even(2)] int n); }
		public class Counter : ICounter { public void Count([Even(2)] int n) { } }
		""");

	[Fact]
	public Task UndescribedAttribute_InThisProject_IsReportedAsFixable() => VerifyAsync(Even + """
		public interface ICounter { void Count([Even(2)] int n); }
		public class Counter : ICounter { public void Count([{|PV1002:Even(4)|}] int n) { } }
		""");

	[Fact]
	public async Task UndescribedAttribute_InThisProject_MessageSaysHowToFixIt()
	{
		Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + Even + """
			public interface ICounter { void Count([Even(2)] int n); }
			public class Counter : ICounter { public void Count([Even(4)] int n) { } }
			""").ConfigureAwait(true));

		Assert.Equal(
			"Parameter 'n' of 'Counter.Count(int)': [Even(4)] can't be compared with the attributes of 'ICounter.Count(int)'. "
				+ "Describe 'EvenAttribute' with [ComparableValidation], or repeat the contract's attribute exactly.",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task DescribedAttribute_IsComparedWithBuiltIns() => VerifyAsync(Described + """
		public interface IBag
		{
			void Fill([MinLength(2)] int[] a, [NotEmpty] int[] b, [Range(5, 100)] int c, [AtLeast(5)] int d, [AtLeast(5)] int e);
		}
		public class Bag : IBag
		{
			public void Fill([NotEmpty] int[] a, [{|PV1001:MinLength(2)|}] int[] b, [AtLeast(5)] int c, [AtLeast(3)] int d, [{|PV1001:AtLeast(6)|}] int e) { }
		}
		""");

	[Fact]
	public Task UndescribedAttribute_FromAPackage_IsReportedAsExternal()
		=> VerifyAsync(VendorEvenUsage.Replace("ID", "PV1005"), CompileToImage(Vendor, "Vendor"));

	[Fact]
	public async Task UndescribedAttribute_FromAPackage_MessageSaysWhereItIsFrom()
	{
		Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(
			Usings + VendorEvenUsage.Replace("{|ID:", "").Replace("|}", ""),
			references: CompileToImage(Vendor, "Vendor")).ConfigureAwait(true));

		Assert.Equal(
			"Parameter 'b' of 'Counter.Count(int, int)': [Vendor.Even(4)] can't be compared with the attributes of 'ICounter.Count(int, int)', "
				+ "because 'Vendor.EvenAttribute' comes from 'Vendor', outside this solution. "
				+ "Repeat the contract's attribute exactly, or describe it with [assembly: ComparableValidation(typeof(Vendor.EvenAttribute), ...)].",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task UndescribedAttribute_FromAProjectReference_IsReportedAsFixable()
		=> VerifyAsync(VendorEvenUsage.Replace("ID", "PV1002"), Compile(Usings + Vendor, "Vendor").ToMetadataReference());

	[Fact]
	public Task UndescribedAttribute_FromAFileUnderTheSolutionRoot_IsReportedAsFixable()
		=> VerifyAsync(
			VendorEvenUsage.Replace("ID", "PV1002"),
			solutionRoot: @"C:\Repo\",
			CompileToImage(Vendor, "Vendor", filePath: @"C:\Repo\Vendor\bin\Debug\Vendor.dll"));

	[Fact]
	public Task UndescribedAttribute_FromAFileOutsideTheSolutionRoot_IsReportedAsExternal()
		=> VerifyAsync(
			VendorEvenUsage.Replace("ID", "PV1005"),
			solutionRoot: @"C:\Repo\",
			CompileToImage(Vendor, "Vendor", filePath: @"C:\Users\me\.nuget\packages\vendor\1.0.0\lib\Vendor.dll"));

	[Fact]
	public Task AssemblyDescription_MakesAPackagesAttributeComparable() => VerifyAsync(
		"""
		[assembly: ComparableValidation(typeof(Vendor.NonEmptyAttribute), ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]

		public interface IBag { void Fill([Vendor.NonEmpty] int[] a, [Vendor.NonEmpty] int[] b); }
		public class Bag : IBag
		{
			public void Fill([MinLength(1)] int[] a, [{|PV1001:MinLength(2)|}] int[] b) { }
		}
		""",
		CompileToImage(Vendor, "Vendor"));

	[Fact]
	public Task AssemblyDescription_InAReferencedAssembly_Applies()
	{
		MetadataReference vendor = CompileToImage(Vendor, "Vendor");
		MetadataReference descriptions = CompileToImage(
			"""
			[assembly: ComparableValidation(typeof(Vendor.NonEmptyAttribute), ValidationDimensions.Length, ValidationComparison.LowerBound, Value = 1)]
			""",
			"Descriptions",
			references: vendor);

		return VerifyAsync(
			"""
			public interface IBag { void Fill([Vendor.NonEmpty] int[] a, [Vendor.NonEmpty] int[] b); }
			public class Bag : IBag
			{
				public void Fill([MinLength(1)] int[] a, [{|PV1001:MinLength(2)|}] int[] b) { }
			}
			""",
			vendor,
			descriptions);
	}

	[Fact]
	public Task DerivedClass_DoesNotInheritTheDescription() => VerifyAsync(Described.Replace("public sealed class AtLeast", "public class AtLeast") + """
		public sealed class AtLeastOrZeroAttribute(int minimum) : AtLeastAttribute(minimum) { }

		public interface ICounter { void Count([AtLeast(5)] int n); }
		public class Counter : ICounter { public void Count([AtLeast(5), {|PV1002:AtLeastOrZero(5)|}] int n) { } }
		""");

	[Fact]
	public Task InvalidDescriptions_AreReported() => VerifyAsync("""
		[{|PV1006:ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Argument = "nope")|}]
		public sealed class UnknownArgumentAttribute : ValidationAttribute { }

		[{|PV1006:ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound)|}]
		public sealed class NoBoundAttribute : ValidationAttribute { }

		[{|PV1006:ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Value = "one")|}]
		public sealed class TextBoundAttribute : ValidationAttribute { }

		[{|PV1006:ComparableValidation(ValidationDimensions.Value, ValidationComparison.AllowedValues, Value = new[] { 1 }, IsExclusive = true)|}]
		public sealed class ExclusiveSetAttribute : ValidationAttribute { }

		[{|PV1006:ComparableValidation("Shape", ValidationComparison.Flag)|}]
		public sealed class NotValidationAttribute : System.Attribute { }

		[{|PV1006:ComparableValidation(typeof(string), "Shape", ValidationComparison.Flag)|}]
		public sealed class TypeFormOnClassAttribute : ValidationAttribute { }

		[ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Argument = "minimum", ExclusiveArgument = "Exclusive")]
		public sealed class ValidAttribute(int minimum) : ValidationAttribute
		{
			public int Minimum => minimum;
			public bool Exclusive { get; set; }
		}
		""");

	[Fact]
	public Task InvalidAssemblyDescription_IsReported() => VerifyAsync("""
		[assembly: {|PV1006:ComparableValidation(typeof(string), "Shape", ValidationComparison.Flag)|}]
		""");

	[Fact]
	public async Task InvalidDescription_MessageSaysWhy()
	{
		Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(Usings + """
			[ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound, Argument = "nope")]
			public sealed class UnknownArgumentAttribute : ValidationAttribute { }
			""").ConfigureAwait(true));

		Assert.Equal(
			"This [ComparableValidation] is invalid because 'UnknownArgumentAttribute' has no constructor parameter or settable property named 'nope'",
			diagnostic.GetMessage());
	}

	[Fact]
	public Task InvalidDescription_LeavesTheAttributeUndescribed() => VerifyAsync("""
		[{|PV1006:ComparableValidation(ValidationDimensions.Length, ValidationComparison.LowerBound)|}]
		public sealed class NoBoundAttribute(int length) : ValidationAttribute { public int Length => length; }

		public interface IBag { void Fill([NoBound(1)] int[] items); }
		public class Bag : IBag { public void Fill([{|PV1002:NoBound(0)|}] int[] items) { } }
		""");
}

using static ParameterValidation.Analyzers.Test.CodeFixVerifier;

namespace ParameterValidation.Analyzers.Test;

/// <summary>The code fix that restates a contract's missing attribute.</summary>
public class CodeFixTests
{
	[Fact]
	public Task MissingParameterAttribute_IsRestated() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
		public class Multiplier : IMultiplier
		{
			public int[] Multiply(int[] array) => array;
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
		public class Multiplier : IMultiplier
		{
			public int[] Multiply([MinLength(1)] int[] array) => array;
		}
		""");

	[Fact]
	public Task MissingUsing_IsAdded() => VerifyAsync("""
		public interface IScale { void Set([System.ComponentModel.DataAnnotations.Range(1, 10)] int level); }
		public class Scale : IScale
		{
			public void Set(int level) { }
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface IScale { void Set([System.ComponentModel.DataAnnotations.Range(1, 10)] int level); }
		public class Scale : IScale
		{
			public void Set([Range(1, 10)] int level) { }
		}
		""");

	[Fact]
	public Task MissingPropertyAttribute_IsRestated() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface ISized { [Range(1, 10)] int Size { get; } }
		public class Sized : ISized
		{
			public int Size => 3;
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface ISized { [Range(1, 10)] int Size { get; } }
		public class Sized : ISized
		{
			[Range(1, 10)]
			public int Size => 3;
		}
		""");

	[Fact]
	public Task NamedArgumentsAndTypes_AreRestatedExactly() => VerifyAsync("""
		using System;
		using System.ComponentModel.DataAnnotations;

		public interface IDiary { void Write([Range(typeof(DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] DateTime when); }
		public class Diary : IDiary
		{
			public void Write(DateTime when) { }
		}
		""", """
		using System;
		using System.ComponentModel.DataAnnotations;

		public interface IDiary { void Write([Range(typeof(DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] DateTime when); }
		public class Diary : IDiary
		{
			public void Write([Range(typeof(DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)] DateTime when) { }
		}
		""");

	[Fact]
	public Task GenericContract_IsRestated() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface IStore<T> { void Add([Required] T item); }
		public class Store : IStore<string>
		{
			public void Add(string item) { }
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface IStore<T> { void Add([Required] T item); }
		public class Store : IStore<string>
		{
			public void Add([Required] string item) { }
		}
		""");

	[Fact]
	public Task ContractInAReferencedAssembly_IsRestated() => VerifyAsync("""
		public class Multiplier : Library.IMultiplier
		{
			public int[] Multiply(int[] array) => array;
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public class Multiplier : Library.IMultiplier
		{
			public int[] Multiply([MinLength(1)] int[] array) => array;
		}
		""",
		fixAll: false,
		AnalyzerVerifier.CompileToImage("""
			namespace Library;
			public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
			""", "Library"));

	[Fact]
	public Task FixAll_RestatesEveryMissingAttributeInOrder() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required, MinLength(1)] string first, [MaxLength(10)] string last); }
		public class Namer : INamer
		{
			public void Name(string first, string last) { }
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required, MinLength(1)] string first, [MaxLength(10)] string last); }
		public class Namer : INamer
		{
			public void Name([Required, MinLength(1)] string first, [MaxLength(10)] string last) { }
		}
		""",
		fixAll: true);

	[Fact]
	public Task RestatedAttribute_JoinsAnExistingValidationList() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required, MinLength(1), MaxLength(10)] string name); }
		public class Namer : INamer
		{
			public void Name([Required] string name) { }
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required, MinLength(1), MaxLength(10)] string name); }
		public class Namer : INamer
		{
			public void Name([Required, MinLength(1), MaxLength(10)] string name) { }
		}
		""");

	[Fact]
	public Task RestatedAttribute_KeepsOutOfOtherLists() => VerifyAsync("""
		using System.ComponentModel;
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required] string name); }
		public class Namer : INamer
		{
			public void Name([Description("The name")] string name) { }
		}
		""", """
		using System.ComponentModel;
		using System.ComponentModel.DataAnnotations;

		public interface INamer { void Name([Required] string name); }
		public class Namer : INamer
		{
			public void Name([Description("The name")][Required] string name) { }
		}
		""");

	[Fact]
	public Task RestatedPropertyAttribute_JoinsAnExistingValidationList() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface INamed { [Required, MinLength(1)] string Name { get; } }
		public class Named : INamed
		{
			[Required]
			public string Name => "x";
		}
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface INamed { [Required, MinLength(1)] string Name { get; } }
		public class Named : INamed
		{
			[Required, MinLength(1)]
			public string Name => "x";
		}
		""",
		fixAll: true);

	[Fact]
	public Task InheritedImplementation_HasNoFix() => VerifyAsync("""
		using System.ComponentModel.DataAnnotations;

		public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
		public class Base
		{
			public int[] Multiply(int[] array) => array;
		}
		public class Derived : Base, IMultiplier { }
		""", """
		using System.ComponentModel.DataAnnotations;

		public interface IMultiplier { int[] Multiply([MinLength(1)] int[] array); }
		public class Base
		{
			public int[] Multiply(int[] array) => array;
		}
		public class Derived : Base, IMultiplier { }
		""");
}

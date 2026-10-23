using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ParameterValidation.Weaver;

internal static class AssemblyWeaver
{
	/// <summary>
	/// Added to every woven assembly so that an incremental build, which skips compilation and
	/// leaves the already-woven assembly in place, doesn't insert the checks a second time.
	/// </summary>
	private const string MarkerTypeName = "<ParameterValidationWoven>";

	/// <returns>The number of methods woven, or <c>null</c> if the assembly was already woven.</returns>
	public static int? Weave(string assemblyPath, IEnumerable<string> referencePaths)
	{
		using DefaultAssemblyResolver resolver = new();
		foreach (string directory in referencePaths
			.Where(static path => path.Length > 0)
			.Select(static path => Path.GetDirectoryName(path)!)
			.Append(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!)
			.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			resolver.AddSearchDirectory(directory);
		}

		string symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
		bool symbolsExist = File.Exists(symbolsPath);
		using ModuleDefinition module = FileRetry.Run("read the assembly", () => ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters
		{
			AssemblyResolver = resolver,
			ReadingMode = ReadingMode.Immediate,
			InMemory = true,
			ReadSymbols = symbolsExist,
			ThrowIfSymbolsAreNotMatching = false,
		}));

		if (module.Types.Any(static type => type.Name == MarkerTypeName))
		{
			return null;
		}

		bool hasSymbols = module.HasSymbols;
		if (symbolsExist && !hasSymbols)
		{
			Console.WriteLine($"{symbolsPath} : warning PV0006: The symbols don't match the assembly, so they were left unchanged; debugging the woven code may not work.");
		}

		WellKnownReferences references = new(module);
		int wovenCount = module.GetTypes()
			.SelectMany(static type => type.Methods)
			.ToList()
			.Count(method => new MethodWeaver(references, method).Weave());

		module.Types.Add(new TypeDefinition(
			"",
			MarkerTypeName,
			TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
			module.TypeSystem.Object));

		// Write to memory first and only then replace the files on disk, so a failure part-way
		// through (e.g. another process grabbing the file) can't leave behind a half-written
		// assembly that already contains the marker and would never be re-woven.
		using MemoryStream assembly = new();
		using MemoryStream symbols = new();
		module.Write(assembly, new WriterParameters
		{
			WriteSymbols = hasSymbols,
			SymbolWriterProvider = hasSymbols ? new PortablePdbWriterProvider() : null,
			SymbolStream = hasSymbols ? symbols : null,
		});

		// The assembly is replaced first: if that fails, nothing on disk has changed. Each file is
		// retried on its own from the bytes already in memory; re-reading and re-weaving after a
		// partial replacement would pair the original assembly with the woven symbols.
		FileRetry.Run("replace the assembly", () => Replace(assemblyPath, assembly));
		if (hasSymbols)
		{
			FileRetry.Run("replace the symbols", () => Replace(symbolsPath, symbols));
		}

		return wovenCount;
	}

	private static void Replace(string path, MemoryStream contents)
	{
		string temporaryPath = path + ".weaving";
		try
		{
			File.WriteAllBytes(temporaryPath, contents.ToArray());
			File.Move(temporaryPath, path, overwrite: true);
		}
		finally
		{
			File.Delete(temporaryPath);
		}
	}
}

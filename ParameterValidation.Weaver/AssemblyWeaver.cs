using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Net.NowhereAtAll.ParameterValidation.Weaver;

/// <summary>Weaves every method of one assembly, in place, along with its symbols.</summary>
/// <param name="output">Where warnings, in MSBuild's canonical format, are written.</param>
internal sealed class AssemblyWeaver(TextWriter output, FileRetry retry)
{
    private const string SymbolsExtension = ".pdb";

    private readonly TextWriter _output = output;
    private readonly FileRetry _retry = retry;

    /// <returns>The number of methods woven, or <c>null</c> if the assembly was already woven.</returns>
    public int? Weave(string assemblyPath, IEnumerable<string> referencePaths)
    {
        using DefaultAssemblyResolver resolver = CreateResolver(assemblyPath, referencePaths);
        string symbolsPath = Path.ChangeExtension(assemblyPath, SymbolsExtension);
        bool hasSymbolsFile = File.Exists(symbolsPath);
        using ModuleDefinition module = this._retry.Run(
            "read the assembly",
            (Path: assemblyPath, Resolver: resolver, HasSymbolsFile: hasSymbolsFile),
            static read => ReadModule(read.Path, read.Resolver, read.HasSymbolsFile)
        );
        return WovenMarker.IsWoven(module)
            ? null
            : this.Weave(module, assemblyPath, symbolsPath, hasSymbolsFile);
    }

    private int Weave(ModuleDefinition module, string assemblyPath, string symbolsPath, bool hasSymbolsFile)
    {
        if (hasSymbolsFile && !module.HasSymbols)
        {
            this._output.WriteLine(
                $"{symbolsPath} : warning {WeavingErrorCodes.SymbolsMismatch}: The symbols don't match the "
                + "assembly, so they were left unchanged; debugging the woven code may not work."
            );
        }

        int wovenCount = WeaveMethods(module);
        WovenMarker.Mark(module);
        this.Write(module, assemblyPath, symbolsPath);
        return wovenCount;
    }

    private static DefaultAssemblyResolver CreateResolver(string assemblyPath, IEnumerable<string> referencePaths)
    {
        DefaultAssemblyResolver resolver = new();
        IEnumerable<string> directories = referencePaths
            .Where(static path => path.Length > 0)
            .Append(Path.GetFullPath(assemblyPath))
            .Select(static path => Path.GetDirectoryName(path)!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in directories)
        {
            resolver.AddSearchDirectory(directory);
        }

        return resolver;
    }

    private static ModuleDefinition ReadModule(string assemblyPath, IAssemblyResolver resolver, bool hasSymbolsFile)
        => ModuleDefinition.ReadModule(
            assemblyPath,
            new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Immediate,
                InMemory = true,
                ReadSymbols = hasSymbolsFile,
                ThrowIfSymbolsAreNotMatching = false,
            }
        );

    private static int WeaveMethods(ModuleDefinition module)
    {
        WellKnownReferences references = new(module);
        List<MethodDefinition> methods = [.. module.GetTypes().SelectMany(static type => type.Methods)];
        return methods.Count(method => new MethodWeaver(references, method).Weave());
    }

    /// <summary>
    /// Writes to memory first and only then replaces the files on disk, so a failure part-way
    /// through (e.g. another process grabbing the file) can't leave behind a half-written assembly
    /// that already contains the marker and would never be re-woven.
    /// </summary>
    /// <remarks>
    /// The assembly is replaced first: if that fails, nothing on disk has changed. Each file is
    /// retried on its own from the bytes already in memory; re-reading and re-weaving after a
    /// partial replacement would pair the original assembly with the woven symbols.
    /// </remarks>
    private void Write(ModuleDefinition module, string assemblyPath, string symbolsPath)
    {
        bool hasSymbols = module.HasSymbols;
        using MemoryStream assembly = new();
        using MemoryStream symbols = new();
        module.Write(assembly, CreateWriterParameters(hasSymbols, symbols));
        this.Replace("replace the assembly", assemblyPath, assembly);
        if (hasSymbols)
        {
            this.Replace("replace the symbols", symbolsPath, symbols);
        }
    }

    private void Replace(string description, string path, MemoryStream contents)
        => this._retry.Run(
            description,
            (Path: path, Contents: contents),
            static file => AtomicFile.Replace(file.Path, file.Contents)
        );

    private static WriterParameters CreateWriterParameters(bool hasSymbols, MemoryStream symbols)
        => hasSymbols
            ? new WriterParameters
            {
                WriteSymbols = true,
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = symbols,
            }
            : new WriterParameters();
}

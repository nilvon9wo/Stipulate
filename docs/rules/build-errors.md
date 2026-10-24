# Build errors

Errors the build reports when it can't weave an assembly (see
[enforcement modes](../use/enforcement-modes.md#weaving)), and warnings about what it wove
differently or not at all. Each one names the assembly or the project, and appears in Visual
Studio's Error List with its cause.

| ID | Severity | Meaning | What to do |
|---|---|---|---|
| ST0001 | Error | The assembly can't be woven: a type it uses can't be resolved, typically because a validation attribute's assembly isn't among the project's references. The message names the type and the missing assembly. | Reference the assembly that declares the attribute. |
| ST0002 | Error | A file stayed in use by another process (another build, a test run, an antivirus scan) through every retry. | Close whatever holds it, and build again. |
| ST0003 | Error | The weaver failed in a way it didn't anticipate. The message includes the exception. | Please report it, with the message. |
| ST0004 | Error | The weaver crashed or never started. On Windows, "An Application Control policy has blocked this file" means Windows refused to load the freshly built weaver. | Build under WSL, or ask for an exception for the folder. |
| ST0005 | Error | The weaver isn't where `StipulateWeaverPath` says. | With the package, restore again; in this repository, build `Stipulate.Weaver` first. Or correct the path. |
| ST0006 | Warning | The symbols (`.pdb`) don't match the assembly, so they were left unchanged; debugging the woven code may not work. | Rebuild, so the assembly and its symbols match. |
| ST0007 | Error | The assembly has parameters with validation attributes, but not the check that enforces them, `Net.NowhereAtAll.Stipulate.Runtime.ArgumentChecks.Check`, which `Stipulate.targets` compiles into every project it weaves (see [what the check adds](../use/enforcement-modes.md#what-the-check-adds-to-a-project)). | Make sure the project imports `Stipulate.targets`, and that nothing removes `Stipulate.ArgumentChecks.cs` (or `.vb`, or `.fs`, in its language) from its `Compile` items. |
| ST0008 | Warning | Stipulate can't enforce anything in the project, so the attributes on its parameters aren't checked: it is a Visual Basic or F# project in `Interceptors` mode (interceptors are a C# feature), or in a language other than C#, Visual Basic and F# (see [other .NET languages](../use/other-languages.md)). Its calls into assemblies that Stipulate wove are still checked. | In Visual Basic or F#, set the project's `StipulateMode` to `Weaving`; otherwise, move the validated methods into a C#, Visual Basic or F# project. Or, to acknowledge that they aren't enforced, set `StipulateMode` to `None`. |
| ST0009 | Error | An F# project's `StipulateFSharpChecks` isn't one of its values, so Stipulate can't tell which calls to guarantee are checked (see [F# and its optimizer](../use/other-languages.md#f-and-its-optimizer)). | Set it to `EveryCall` (the default), `CallsFromOtherAssemblies` or `NoInliningMethods`. |

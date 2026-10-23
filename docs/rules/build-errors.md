# Build errors

Errors the build reports when it can't weave an assembly (see
[enforcement modes](../use/enforcement-modes.md#weaving)). Each one names the assembly, and
appears in Visual Studio's Error List with its cause.

| ID | Severity | Meaning | What to do |
|---|---|---|---|
| PV0001 | Error | The assembly can't be woven: a type it uses can't be resolved, typically because a validation attribute's assembly isn't among the project's references. The message names the type and the missing assembly. | Reference the assembly that declares the attribute. |
| PV0002 | Error | A file stayed in use by another process (another build, a test run, an antivirus scan) through every retry. | Close whatever holds it, and build again. |
| PV0003 | Error | The weaver failed in a way it didn't anticipate. The message includes the exception. | Please report it, with the message. |
| PV0004 | Error | The weaver crashed or never started. On Windows, "An Application Control policy has blocked this file" means Windows refused to load the freshly built weaver. | Build under WSL, or ask for an exception for the folder. |
| PV0005 | Error | The weaver hasn't been built: `ParameterValidationWeaverPath` doesn't exist. | Build `ParameterValidation.Weaver` first, or correct the path. |
| PV0006 | Warning | The symbols (`.pdb`) don't match the assembly, so they were left unchanged; debugging the woven code may not work. | Rebuild, so the assembly and its symbols match. |

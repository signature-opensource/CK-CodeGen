Compiles a CK.CodeGen workspace into an assembly with Roslyn, optionally loading it.
Main types: `CodeGenerator` (its `Generate` methods and `ICodeGeneratorModule` rewriting modules) and
`GenerateResult`, which keeps emit errors, load errors and assembly binding conflicts apart.

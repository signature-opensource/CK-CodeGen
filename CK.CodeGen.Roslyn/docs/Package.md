Compiles a CK.CodeGen workspace into an assembly with Roslyn.

The CodeGenerator parses the generated sources, closes the reference set transitively from a minimal
list of assemblies, emits the assembly and optionally loads it. Code generation modules can rewrite the
syntax trees and inject additional code before compilation.

The GenerateResult keeps the emit error, the load error and the assembly binding conflicts apart, so a
compilation that succeeded but could not be loaded is not reported as a code generation failure.

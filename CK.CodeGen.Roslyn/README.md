# CK.CodeGen.Roslyn

Turns an [`ICodeWorkspace`](../CK.CodeGen/ICodeWorkspace.cs) into an assembly. This is the compilation
half of [CK.CodeGen](../CK.CodeGen/README.md), kept in its own package because it drags in
`Microsoft.CodeAnalysis.CSharp` and most consumers of the source model do not need it.

> ℹ️ Read [CK.CodeGen](../CK.CodeGen/README.md) first: everything here takes a workspace as input.

## Generating

[`CodeGenerator`](CodeGenerator.cs) *"encapsulates Roslyn compiler"*. It is constructed with a
`Func<ICodeWorkspace>` factory rather than a workspace: it needs fresh ones for the modules (below), and
one for the source-string overload. Its knobs:

| Member | Default |
|--------|---------|
| `ParseOptions` | `null` - all defaults apply, language version is `LanguageVersion.Default` |
| `CompilationOptions` | a **new** `CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary )`, assigned by the constructor |
| `AutoRegisterRuntimeAssembly` | `true` - the assembly defining `object` is referenced automatically, but only when compiling |
| `Modules` | empty |

The `CompilationOptions` row is worth reading twice, because the property's own summary says *"When let
to null, defaults to the `DefaultCompilationOptions`"*. The constructor never lets it be null - it
assigns an equivalent new instance - so that `?? DefaultCompilationOptions` fallback only ever fires on
the `static` overloads, where the caller passes null explicitly. The comment is stale; the observable
behaviour is the same output kind either way.

`AutoRegisterRuntimeAssembly` is read as `!skipCompilation && AutoRegisterRuntimeAssembly`, so it does
nothing in a parse-only run.

`Generate( ICodeWorkspace code, string assemblyPath, bool skipCompilation, Func<string,Assembly>? loader )`
is the main entry point; overloads take a raw source string plus *"a minimal list of required reference
assemblies"* instead. That tolerance is real: whatever you pass is closed transitively by a private
`Discover` walk over `Assembly.GetReferencedAssemblies`, so naming the direct dependencies is enough.
`skipCompilation: true` stops after parsing and source generation - only `Sources` is then meaningful,
and `assemblyPath` may be null.

The whole load is wrapped in `WeakAssemblyNameResolver.TemporaryInstall()`, which is what allows a
reference to bind to a different version than the one requested. The mismatches it had to absorb are
not swallowed: they come back as `GenerateResult.LoadConflicts`. One overload is exempt and says so -
the `SyntaxTree`-and-`MetadataReference` one is *"not protected by the WeakAssemblyNameResolver. It
should be done, if necessary, by the caller."*

## Modules

[`ICodeGeneratorModule`](ICodeGenerationModule.cs) is the post-processing hook, with two methods:

```csharp
IReadOnlyList<SyntaxTree> Rewrite( IReadOnlyList<SyntaxTree> trees );
void Inject( ICodeWorkspace code );
```

`Rewrite` may return the trees unchanged or a rewritten set; `Inject` adds new code and assembly
references into a workspace of the module's own. The order is fixed and matters: each module rewrites
what the previous ones produced, then injects. Its own injected code is therefore **not** processed by
itself - *"they act as a post processor: their own injected code source (if any) is not processed by the
code module itself."*

Because a module may hold state between its `Rewrite` and its `Inject`, the `Modules` list is **cleared
by every call to the instance `Generate` methods**. Refill it before each generation rather than
configuring it once.

## Reading the result

[`GenerateResult`](GenerateResult.cs) is a bag of public readonly fields, and it distinguishes failures
that are usually collapsed into one:

| Field | Meaning |
|-------|---------|
| `Sources` | the final syntax trees - never null, but empty when the emit threw |
| `CompilationSkipped` | true when only `Sources` is relevant |
| `EmitResult` | the Roslyn result - null when compilation was skipped |
| `EmitError` | an exception from the emit process itself; when set, `EmitResult` is necessarily null |
| `Assembly` | the loaded assembly, when a loader was given |
| `AssemblyLoadError` | the exception from loading it |
| `LoadConflicts` | the `AssemblyLoadConflict` encountered while resolving dependencies |

`Success` means *"whether the parsing or full compilation succeeds"*, and is computed differently in
each of the two constructors:

- compiled: `EmitResult?.Success == true && AssemblyLoadError == null`;
- skipped: every parse diagnostic is below `DiagnosticSeverity.Error`.

So a parse-only run can and does report success. On the compiled path, note that `Success` is false when
the code compiled but the resulting assembly could not be loaded - the usual symptom of a version
conflict rather than a code generation bug. `LoadConflicts` is where to look then.

`ParseDiagnostics` gathers the diagnostics of every source tree, which is what you read when
`skipCompilation` is true and there is no `EmitResult`.

## Requires.

- `CK.CodeGen`, `Microsoft.CodeAnalysis.CSharp`, `CK.ActivityMonitor.SimpleSender` and
  `CK.WeakAssemblyNameResolver` - the last one being what makes the relaxed version binding and the
  `LoadConflicts` reporting possible.

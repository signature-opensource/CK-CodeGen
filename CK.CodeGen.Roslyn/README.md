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
assigns an equivalent new instance. The property stays `public CSharpCompilationOptions? { get; set; }`
though, so setting it back to null reaches the `?? DefaultCompilationOptions` fallback through the
instance path too - the comment is accurate for a state you can reach, the constructor merely
pre-assigns. Either way the output kind is the same.

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

### End to end

Compiling a workspace and loading the result. This is the fixture's `CreateAssembly` and its
`HandleCreateResult` joined into one flow - the fixture splits them across two methods:

```csharp
var g = new CodeGenerator( CodeWorkspace.Factory );
g.Modules.AddRange( modules );
GenerateResult result = g.Generate( sourceCode,
                                    RandomDllPath,
                                    references,
                                    false,                      // skipCompilation
                                    System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath );

result.LogResult( TestHelper.Monitor, LogLevel.Info );
result.Success.ShouldBeTrue();
return result.Assembly;
```

`RandomDllPath` is the fixture's own per-run output path and `TestHelper` its monitor, from
`using static CK.Testing.MonitorTestHelper`; the fixture also picks the loader through an `#if NET461`
branch, `Assembly.LoadFrom` on the framework side. Note `Modules` being filled immediately before the
call rather than once at construction - the list is cleared by every instance `Generate`, as the
section below explains.

`LogResult( IActivityMonitor monitor, LogLevel? dumpSources = null )` is the shortcut worth knowing,
and it is outcome-driven rather than exhaustive: it always reports the load conflicts and closes with
whether the assembly loaded, but the diagnostics appear **only on failure** - the emit ones, or the
parse ones when compilation was skipped - and the sources only when you pass a level. So a successful
generation logs three lines and a failing one logs what you need. Reach for it before reading the
fields one by one.

The parse-only variant, and the two forms side by side:

```csharp
var workspace = CodeWorkspace.Create();
var global = workspace.Global;
global.EnsureUsing( "System" );
global.CreateType( "public class Tester" )
         .Append( "public bool OK => true;" ).NewLine();

// Compiled: CreateAssembly is the fixture's own wrapper around the call shown above.
Assembly a = LocalTestHelper.CreateAssembly( workspace.GetGlobalSource(), workspace.AssemblyReferences );
a.ShouldNotBeNull();

// Parse-only: no path, no loader, and it still succeeds.
var g = new CodeGenerator( CodeWorkspace.Factory );
var r = g.Generate( workspace, null, skipCompilation: true );
r.Success.ShouldBeTrue();
r.Sources.Count.ShouldBe( 1 );
```

Note `workspace.AssemblyReferences` being passed straight through as the reference list: that is the
transitive closure described above doing its job, so a caller hands over what the workspace collected
and nothing more. And the shortest form of all needs no workspace at all:

```csharp
var r = CodeGenerator.Generate( "class P {}", null );
r.Success.ShouldBeTrue();
r.Sources.Count.ShouldBe( 1 );
r.ParseDiagnostics.ShouldBeEmpty();
```

From [`LocalTestHelper`](../Tests/CK.CodeGen.Roslyn.Tests/LocalTestHelper.cs) and
[`CompileToSourceStringTests`](../Tests/CK.CodeGen.Roslyn.Tests/CompileToSourceStringTests.cs).

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
- skipped: `ParseDiagnostics.All( d => d.Severity != DiagnosticSeverity.Error )` - an inequality on the
  severity, not an ordering test.

So a parse-only run can and does report success. On the compiled path, note that `Success` is false when
the code compiled but the resulting assembly could not be loaded - the usual symptom of a version
conflict rather than a code generation bug. `LoadConflicts` is where to look then.

`ParseDiagnostics` gathers the diagnostics of every source tree, which is what you read when
`skipCompilation` is true and there is no `EmitResult`.

## Requires.

- `CK.CodeGen`, `Microsoft.CodeAnalysis.CSharp`, `CK.ActivityMonitor.SimpleSender` and
  `CK.WeakAssemblyNameResolver` - the last one being what makes the relaxed version binding and the
  `LoadConflicts` reporting possible.

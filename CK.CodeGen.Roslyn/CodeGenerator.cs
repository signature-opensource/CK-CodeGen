using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using CK.Core;

namespace CK.CodeGen;

/// <summary>
/// Encapsulates Roslyn compiler.
/// </summary>
public class CodeGenerator
{
    readonly Func<ICodeWorkspace> _workspaceFactory;

    /// <summary>
    /// Gets the default option, initialized to produce <see cref="OutputKind.DynamicallyLinkedLibrary"/> output.
    /// </summary>
    public static readonly CSharpCompilationOptions DefaultCompilationOptions = new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary );

    /// <summary>
    /// Initializes a new <see cref="CodeGenerator"/> with options.
    /// </summary>
    /// <param name="workspaceFactory">
    /// Factory for <see cref="ICodeWorkspace"/> implementations.
    /// Must not be null.
    /// </param>
    public CodeGenerator( Func<ICodeWorkspace> workspaceFactory )
    {
        if( workspaceFactory == null ) throw new ArgumentNullException( nameof( workspaceFactory ) );
        _workspaceFactory = workspaceFactory;
        CompilationOptions = new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary );
    }

    /// <summary>
    /// Gets or sets the parse options to use.
    /// Default to null: all default applies, the language version is <see cref="LanguageVersion.Default"/>.
    /// </summary>
    public CSharpParseOptions? ParseOptions { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="CSharpCompilationOptions"/>.
    /// When let to null, defaults to the <see cref="DefaultCompilationOptions"/>.
    /// </summary>
    public CSharpCompilationOptions? CompilationOptions { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly that defines the object type is
    /// automatically registered.
    /// Defaults to true.
    /// </summary>
    public bool AutoRegisterRuntimeAssembly { get; set; } = true;

    /// <summary>
    /// Gets a mutable list of <see cref="ICodeGeneratorModule"/>.
    /// Since a code module can maintain an internal state between the calls to <see cref="ICodeGeneratorModule.Rewrite(IReadOnlyList{SyntaxTree})"/>
    /// and <see cref="ICodeGeneratorModule.Inject(ICodeWorkspace)"/>, this list is cleared by each
    /// call to Generate instance methods.
    /// </summary>
    public List<ICodeGeneratorModule> Modules { get; } = new List<ICodeGeneratorModule>();

    /// <summary>
    /// Generates an assembly from a source, a minimal list of required reference assemblies.
    /// </summary>
    /// <param name="sourceCode">The source code. Must be valid C# code.</param>
    /// <param name="assemblyPath">The full final assembly path (including the .dll extension). Can be null if skipCompilation is true.</param>
    /// <param name="someReferences">List of reference assemblies that can be a subset of the actual dependencies.</param>
    /// <param name="skipCompilation">True to skip the compilation. Only the parsing and the source generation is done.</param>
    /// <param name="loader">Optional loader function to load the final emitted assembly.</param>
    /// <returns>Encapsulation of the result.</returns>
    public GenerateResult Generate( string sourceCode, string assemblyPath, IEnumerable<Assembly> someReferences, bool skipCompilation, Func<string, Assembly>? loader = null )
    {
        var w = _workspaceFactory();
        if( !String.IsNullOrWhiteSpace( sourceCode ) ) w.Global.Append( sourceCode );
        foreach( var a in someReferences ) w.DoEnsureAssemblyReference( a );
        return Generate( w, assemblyPath, skipCompilation, loader );
    }

    /// <summary>
    /// Generates an assembly from a source and a minimal list of required reference assemblies.
    /// </summary>
    /// <param name="code">The source code.</param>
    /// <param name="assemblyPath">The full final assembly path (including the .dll extension). Can be null only if <paramref name="skipCompilation"/> is true.</param>
    /// <param name="skipCompilation">True to skip the compilation. Only the parsing and the source generation is done.</param>
    /// <param name="loader">Optional loader function to load the final emitted assembly.</param>
    /// <returns>Encapsulation of the result.</returns>
    public GenerateResult Generate( ICodeWorkspace code, string? assemblyPath, bool skipCompilation, Func<string, Assembly>? loader = null )
    {
        Throw.CheckNotNullArgument( code );
        using( var weakLoader = WeakAssemblyNameResolver.TemporaryInstall() )
        {
            var input = GeneratorInput.Create( _workspaceFactory, code, Modules, !skipCompilation && AutoRegisterRuntimeAssembly, ParseOptions );
            Modules.Clear();

            if( skipCompilation ) return new GenerateResult( input.Trees );
            Throw.CheckNotNullArgument( assemblyPath );

            var collector = new HashSet<Assembly>();
            var skipped = new Dictionary<string, SkippedAssemblyReference>();
            foreach( var a in input.Assemblies )
            {
                if( collector.Add( a ) ) Discover( a, collector, skipped );
            }
            return Generate( CompilationOptions,
                             input.Trees,
                             assemblyPath,
                             collector.Select( a => MetadataReference.CreateFromFile( new Uri( a.Location ).LocalPath ) ),
                             loader )
                    .WithLoadFailures( weakLoader.Conflicts, skipped );
        }
    }

    static void Discover( Assembly a, HashSet<Assembly> collector, Dictionary<string, SkippedAssemblyReference> skipped )
    {
        foreach( var name in a.GetReferencedAssemblies() )
        {
            Assembly dep;
            try
            {
                dep = Assembly.Load( name );
            }
            catch( Exception ex ) when( ex is FileNotFoundException or FileLoadException or BadImageFormatException )
            {
                // A reference that cannot be loaded is not necessarily an error: the CLR binds references
                // lazily (per method), so a reference that no executed code needs is never resolved. Being
                // more eager than the CLR would turn any latent packaging defect anywhere in the transitive
                // closure into a fatal error. Roslyn knows whether the generated code actually needs this
                // assembly: if it does, a CS0012 diagnostic names both the missing assembly and the type
                // that requires it. Skipping defers the failure to a strictly more informative one.
                var fullName = name.FullName;
                if( !skipped.ContainsKey( fullName ) )
                {
                    skipped.Add( fullName, new SkippedAssemblyReference( a.GetName(), name, ex ) );
                }
                continue;
            }
            if( collector.Add( dep ) ) Discover( dep, collector, skipped );
        }
    }

    /// <summary>
    /// Compiles or parses only a single code source.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="assemblyPath">The output path or null if only parsing is required.</param>
    /// <param name="references">Optional list of dependent assemblies. Used only if compilation is required. This list will be transitively closed.</param>
    /// <param name="parseOptions">By default, all default applies, the language version is <see cref="LanguageVersion.Default"/>.</param>
    /// <param name="compileOptions">The compilation options. Used only if compilation is required. Defaults to <see cref="DefaultCompilationOptions"/>.</param>
    /// <param name="loader">Optional loader function to load the final emitted assembly. Used only if compilation is required.</param>
    /// <returns>Encapsulation of the result.</returns>
    static public GenerateResult Generate( string code,
                                           string? assemblyPath = null,
                                           IEnumerable<Assembly>? references = null,
                                           CSharpParseOptions? parseOptions = null,
                                           CSharpCompilationOptions? compileOptions = null,
                                           Func<string, Assembly>? loader = null )
    {
        SyntaxTree[] trees = [SyntaxFactory.ParseSyntaxTree( code, parseOptions )];
        if( String.IsNullOrEmpty( assemblyPath ) )
        {
            // Parsing is enough.
            return new GenerateResult( trees );
        }
        using( var weakLoader = WeakAssemblyNameResolver.TemporaryInstall() )
        {
            var collector = new HashSet<Assembly>();
            var skipped = new Dictionary<string, SkippedAssemblyReference>();
            collector.Add( typeof( object ).Assembly );
            if( references != null )
            {
                foreach( var a in references )
                {
                    if( collector.Add( a ) ) Discover( a, collector, skipped );
                }
            }
            return Generate( compileOptions,
                             trees,
                             assemblyPath,
                             collector.Select( a => MetadataReference.CreateFromFile( new Uri( a.Location ).LocalPath ) ),
                             loader )
                    .WithLoadFailures( weakLoader.Conflicts, skipped );
        }
    }

    /// <summary>
    /// Generates an assembly from a <see cref="SyntaxTree"/> list and a
    /// list of <see cref="MetadataReference"/> required reference assemblies.
    /// <para>
    /// Caution: this method is not protected by the <see cref="WeakAssemblyNameResolver"/>.
    /// It should be done, if necessary, by the caller.
    /// </para>
    /// </summary>
    /// <param name="compileOptions">Compilation options.</param>
    /// <param name="trees">The syntax trees.</param>
    /// <param name="assemblyPath">The full final assembly path (including the .dll extension).</param>
    /// <param name="allReferences">Optional list of assemblies' references.</param>
    /// <param name="loader">Optional loader function to load the final emitted assembly.</param>
    /// <returns>Encapsulation of the result.</returns>
    static public GenerateResult Generate( CSharpCompilationOptions? compileOptions,
                                           IReadOnlyList<SyntaxTree> trees,
                                           string assemblyPath,
                                           IEnumerable<MetadataReference>? allReferences = null,
                                           Func<string, Assembly>? loader = null )
    {
        if( assemblyPath == null ) throw new ArgumentNullException( nameof( assemblyPath ) );
        try
        {
            var option = (compileOptions ?? DefaultCompilationOptions).WithAssemblyIdentityComparer( DesktopAssemblyIdentityComparer.Default );
            CSharpCompilation compilation = CSharpCompilation.Create( Path.GetFileNameWithoutExtension( assemblyPath ),
                                                                      trees,
                                                                      allReferences,
                                                                      option );
            var r = compilation.Emit( assemblyPath );
            if( r.Success && loader != null )
            {
                try
                {
                    return new GenerateResult( null, trees, r, loader( assemblyPath ), null, null );
                }
                catch( Exception ex )
                {
                    return new GenerateResult( null, trees, r, null, ex, null );
                }
            }
            return new GenerateResult( null, trees, r, null, null, null );
        }
        catch( Exception ex )
        {
            return new GenerateResult( ex, [], null, null, null, null );
        }
    }
}

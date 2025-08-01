using CK.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace CK.CodeGen;

/// <summary>
/// Internal helper that applies code modules to a workspace.
/// </summary>
struct GeneratorInput
{
    public readonly IReadOnlyCollection<Assembly> Assemblies;
    public readonly IReadOnlyList<SyntaxTree> Trees;

    GeneratorInput( IReadOnlyCollection<Assembly> a, IReadOnlyList<SyntaxTree> t )
    {
        Assemblies = a;
        Trees = t;
    }

    /// <summary>
    /// Gets whether this input is empty.
    /// </summary>
    public bool IsEmpty => (Assemblies == null || Assemblies.Count == 0) && (Trees == null || Trees.Count == 0);

    /// <summary>
    /// Combines code workspaces and modules into a <see cref="GeneratorInput"/>.
    /// </summary>
    /// <param name="workspaceFactory">Factory for <see cref="ICodeWorkspace"/> implementations. Must not be null.</param>
    /// <param name="code">Original code. Can be null or empty.</param>
    /// <param name="modules">Code modules. Can be null or empty.</param>
    /// <param name="addRuntimeAssembly">True to automatically add the typeof(object)'s assembly.</param>
    /// <param name="options">The optional parse options to use.</param>
    /// <returns>A generator input.</returns>
    internal static GeneratorInput Create(
        Func<ICodeWorkspace> workspaceFactory,
        ICodeWorkspace code,
        IEnumerable<ICodeGeneratorModule> modules,
        bool addRuntimeAssembly,
        CSharpParseOptions? options )
    {
        Throw.DebugAssert( workspaceFactory != null );
        var assemblies = new HashSet<Assembly>();
        if( addRuntimeAssembly ) assemblies.Add( typeof( object ).Assembly );
        var trees = new List<SyntaxTree>();
        if( code != null ) CombineWorkspace( assemblies, trees, code, options );
        if( modules != null )
        {
            foreach( var m in modules )
            {
                var transformed = m.Rewrite( trees );
                if( transformed != null && transformed != trees )
                {
                    trees.Clear();
                    trees.AddRange( transformed );
                }
                var wM = workspaceFactory();
                m.Inject( wM );
                CombineWorkspace( assemblies, trees, wM, options );
            }
        }
        return new GeneratorInput( assemblies, trees );
    }

    static void CombineWorkspace( HashSet<Assembly> assemblies, List<SyntaxTree> trees, ICodeWorkspace c, CSharpParseOptions? options )
    {
        foreach( var a in c.AssemblyReferences ) assemblies.Add( a );
        var s = c.GetGlobalSource();
        if( !String.IsNullOrWhiteSpace( s ) ) trees.Add( SyntaxFactory.ParseSyntaxTree( s, options ) );
    }
}

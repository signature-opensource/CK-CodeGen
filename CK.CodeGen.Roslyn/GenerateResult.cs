using CK.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.CodeGen;

/// <summary>
/// Captures generation result.
/// </summary>
public class GenerateResult
{
    /// <summary>
    /// Gets whether the actual compilation has been skipped: only the
    /// source is available in <see cref="Sources"/>.
    /// When compilation has been skipped, only the Sources are relevant.
    /// </summary>
    public readonly bool CompilationSkipped;

    /// <summary>
    /// The loaded assembly (optional).
    /// </summary>
    public readonly Assembly? Assembly;

    /// <summary>
    /// List of <see cref="AssemblyLoadConflict"/> that occured while
    /// resolving assembly dependencies.
    /// Defaults to null.
    /// </summary>
    public readonly IReadOnlyCollection<AssemblyLoadConflict>? LoadConflicts;

    /// <summary>
    /// List of <see cref="SkippedAssemblyReference"/>: assembly references that could not be loaded
    /// while transitively closing the set of references and have been skipped.
    /// This is not an error by itself (the CLR resolves references lazily, so a reference that no code
    /// needs is never loaded): when the generated code actually requires one of them, the compilation
    /// fails with a CS0012 diagnostic that names it.
    /// Defaults to null.
    /// </summary>
    public readonly IReadOnlyList<SkippedAssemblyReference>? SkippedReferences;

    /// <summary>
    /// List of final Syntax trees that have been generated, parsed (and compiled
    /// if compilation has not been skipped).
    /// </summary>
    public readonly IReadOnlyList<SyntaxTree> Sources;

    /// <summary>
    /// Error raised by the emit process itself.
    /// <para>
    /// When this is not null, <see cref="EmitResult"/> is necessarily null.
    /// </para>
    /// </summary>
    public readonly Exception? EmitError;

    /// <summary>
    /// The Roselyn result.
    /// Null if <see cref="CompilationSkipped"/> is true.
    /// </summary>
    public readonly EmitResult? EmitResult;

    /// <summary>
    /// Error resulting from the attempt to load the generated <see cref="Assembly"/> if any.
    /// </summary>
    public readonly Exception? AssemblyLoadError;

    /// <summary>
    /// Gets whether the parsing or full compilation succeeds.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the parse diagnostics.
    /// </summary>
    public IEnumerable<Diagnostic> ParseDiagnostics => Sources.SelectMany( t => t.GetDiagnostics() );

    /// <summary>
    /// Initializes a new compilation result (CompilationSkipped is false).
    /// </summary>
    /// <param name="eE">Emit exception.</param>
    /// <param name="sources">Sources.</param>
    /// <param name="r">Roselyn result.</param>
    /// <param name="a">Loaded assembly if any.</param>
    /// <param name="e">Load error if any.</param>
    /// <param name="f">Load failures.</param>
    /// <param name="skipped">Skipped assembly references.</param>
    internal GenerateResult( Exception? eE,
                             IReadOnlyList<SyntaxTree> sources,
                             EmitResult? r,
                             Assembly? a,
                             Exception? e,
                             IReadOnlyList<AssemblyLoadConflict>? f,
                             IReadOnlyList<SkippedAssemblyReference>? skipped = null )
    {
        CompilationSkipped = false;
        SkippedReferences = skipped;
        EmitError = eE;
        Assembly = a;
        EmitResult = r;
        Sources = sources;
        AssemblyLoadError = e;
        LoadConflicts = f;
        Success = r?.Success == true && e == null;
    }

    /// <summary>
    /// Initializes a new result when CompilationSkipped is true.
    /// </summary>
    /// <param name="sources">Sources.</param>
    internal GenerateResult( IReadOnlyList<SyntaxTree> sources )
    {
        EmitError = null;
        Assembly = null;
        EmitResult = null;
        AssemblyLoadError = null;
        LoadConflicts = null;
        SkippedReferences = null;
        CompilationSkipped = true;
        Sources = sources;
        Success = ParseDiagnostics.All( d => d.Severity != DiagnosticSeverity.Error );
    }

    /// <summary>
    /// Dumps the result of the compilation into a monitor.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="dumpSources">Optionally dumps the source as another <see cref="CK.Core.LogLevel"/>.</param>
    public void LogResult( IActivityMonitor monitor, LogLevel? dumpSources = null )
    {
        if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
        using( monitor.OpenInfo( "Code Generation information." ) )
        {
            if( LoadConflicts != null && LoadConflicts.Count > 0 )
            {
                using( monitor.OpenWarn( $"{LoadConflicts.Count} assembly load conflict(s)." ) )
                {
                    foreach( var e in LoadConflicts )
                    {
                        if( e.Resolved != null )
                        {
                            monitor.Warn( e.ToString() );
                        }
                        else
                        {
                            monitor.Error( e.ToString() );
                        }
                    }
                }
            }
            if( SkippedReferences != null && SkippedReferences.Count > 0 )
            {
                using( monitor.OpenWarn( $"{SkippedReferences.Count} assembly reference(s) skipped: they cannot be loaded. If the generated code requires one of them, a CS0012 compilation error names it." ) )
                {
                    foreach( var s in SkippedReferences )
                    {
                        monitor.Warn( s.ToString() );
                    }
                }
            }
            if( Success )
            {
                monitor.Info( CompilationSkipped ? "Source code parsing succeeded." : "Source code compilation succeeded." );
                if( dumpSources.HasValue ) DumpSources( monitor, dumpSources.Value );
            }
            else
            {
                using( monitor.OpenError( CompilationSkipped ? "Parsing failed." : "Compilation failed." ) )
                {
                    if( EmitError != null )
                    {
                        monitor.Error( EmitError );
                    }
                    else
                    {
                        if( EmitResult != null )
                        {
                            if( !EmitResult.Success )
                            {
                                using( monitor.OpenInfo( $"{EmitResult.Diagnostics.Count()} Compilation diagnostics." ) )
                                {
                                    foreach( var diag in EmitResult.Diagnostics )
                                    {
                                        monitor.Trace( diag.ToString() );
                                    }
                                }
                            }
                        }
                        else
                        {
                            Throw.DebugAssert( CompilationSkipped );
                            using( monitor.OpenInfo( $"{ParseDiagnostics.Count()} Parsing diagnostics." ) )
                            {
                                foreach( var diag in ParseDiagnostics )
                                {
                                    monitor.Trace( diag.ToString() );
                                }
                            }
                        }
                    }
                    if( dumpSources.HasValue ) DumpSources( monitor, dumpSources.Value );
                }
            }
            if( AssemblyLoadError != null )
            {
                monitor.Error( "Generated assembly load failed.", AssemblyLoadError );
            }
            monitor.CloseGroup( Assembly != null
                                        ? "Generated assembly successfully loaded."
                                        : (Success ? "Succeeded." : "Failed.") );
        }
    }

    void DumpSources( IActivityMonitor monitor, LogLevel level )
    {
        if( Sources != null && Sources.Count > 0 )
        {
            using( monitor.OpenGroup( level, $"Processed {Sources.Count} source tree(s):" ) )
            {
                for( int i = 0; i < Sources.Count; ++i )
                {
                    using( monitor.OpenGroup( level, $"Source n°{i}" ) )
                    {
                        monitor.Log( level, Sources[i].ToString() );
                    }
                }
            }
        }
    }

    internal GenerateResult WithLoadFailures( IReadOnlyList<AssemblyLoadConflict> f, Dictionary<string, SkippedAssemblyReference> skipped )
        => new GenerateResult( EmitError, Sources, EmitResult, Assembly, AssemblyLoadError, f,
                               skipped.Count > 0 ? skipped.Values.ToArray() : null );
}

using CK.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using static CK.Testing.MonitorTestHelper;

namespace CK.CodeGen.Roslyn.Tests;

[TestFixture]
public class SkippedReferenceTests
{
    // 'Dep' is compiled into a folder that the default AssemblyLoadContext does not probe and is
    // never loaded: Assembly.Load can not resolve it. 'Main' references it and is loaded from the
    // bin folder, so Main.Location is available but its reference to 'Dep' is unresolvable.
    // This reproduces what a package with an undeclared dependency does to the reference closure.
    static (Assembly Main, string DepName) CreateMainWithUnresolvableReference()
    {
        var hidden = Path.Combine( Path.GetTempPath(), "CK.CodeGen.HiddenDep-" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 ) );
        Directory.CreateDirectory( hidden );
        string depName = "Dep-" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
        string depPath = Path.Combine( hidden, depName + ".dll" );
        Compile( "public class DepType {}", depPath ).Success.ShouldBeTrue();

        string mainPath = LocalTestHelper.RandomDllPath;
        Compile( "public class MainType : DepType {}", mainPath, depPath ).Success.ShouldBeTrue();
        return (AssemblyLoadContext.Default.LoadFromAssemblyPath( mainPath ), depName);
    }

    static GenerateResult Compile( string code, string path, params string[] extraReferences )
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
                            .Where( a => !a.IsDynamic && a.Location.Length > 0 )
                            .Select( a => MetadataReference.CreateFromFile( a.Location ) )
                            .Concat( extraReferences.Select( p => (MetadataReference)MetadataReference.CreateFromFile( p ) ) );
        return CodeGenerator.Generate( null, new[] { SyntaxFactory.ParseSyntaxTree( code ) }, path, refs );
    }

    [Test]
    public void unresolvable_reference_is_skipped_and_exposed_by_SkippedReferences()
    {
        var (main, depName) = CreateMainWithUnresolvableReference();

        // The generated code does not need 'Dep': generation must succeed.
        var r = CodeGenerator.Generate( "public class Generated {}", LocalTestHelper.RandomDllPath, new[] { main } );
        r.LogResult( TestHelper.Monitor, LogLevel.Info );
        r.Success.ShouldBeTrue();

        r.SkippedReferences.ShouldNotBeNull();
        var skipped = r.SkippedReferences.Single( s => s.Wanted.Name == depName );
        skipped.Requesting.Name.ShouldBe( main.GetName().Name );
        skipped.Error.ShouldBeOfType<FileNotFoundException>();
    }

    [Test]
    public void when_the_generated_code_needs_a_skipped_reference_Roslyn_reports_CS0012()
    {
        var (main, depName) = CreateMainWithUnresolvableReference();

        // 'Generated' derives from MainType whose base type lives in 'Dep': the compilation must
        // fail, and its diagnostic is more precise than the FileNotFoundException would have been.
        var r = CodeGenerator.Generate( "public class Generated : MainType {}", LocalTestHelper.RandomDllPath, new[] { main } );
        r.LogResult( TestHelper.Monitor, LogLevel.Info );
        r.Success.ShouldBeFalse();

        r.EmitResult.ShouldNotBeNull();
        var diagnostics = r.EmitResult.Diagnostics.Where( d => d.Severity == DiagnosticSeverity.Error ).ToList();
        diagnostics.Select( d => d.Id ).ShouldContain( "CS0012" );
        diagnostics.Select( d => d.GetMessage() ).ShouldContain( m => m.Contains( depName, StringComparison.Ordinal ) );
    }
}

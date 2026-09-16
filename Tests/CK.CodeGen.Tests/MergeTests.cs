using NUnit.Framework;
using System;
using System.Linq;

namespace CK.CodeGen.Tests;

[TestFixture]
public class MergeTests
{
    [Test]
    public void merging_code_space_with_one_type()
    {
        var c1 = CodeWorkspace.Create();
        var c2 = CodeWorkspace.Create();

        c1.EnsureAssemblyReference( typeof( object ), typeof( MergeTests ) );
        c2.EnsureAssemblyReference( typeof( Enumerable ), typeof( TestFixtureAttribute ), typeof( MergeTests ) );

        c1.Global.EnsureUsing( "A" );

        var ns1 = c1.Global.FindOrCreateNamespace( "Sub" );
        var t1 = ns1.CreateType( h => h.Append( "class C {//t1-Code" ).NewLine() );
        t1.Append( "//t1-MoreCode" );

        c2.Global.EnsureUsing( "B" );
        var ns2 = c2.Global.FindOrCreateNamespace( "Sub" );
        var t2 = ns2.CreateType( "class C {//t2-Code" + Environment.NewLine );
        t2.Append( "//t2-MoreCode" );

        c1.MergeWith( c2 );

        c1.AssemblyReferences.ShouldAllBe( a => a == typeof( object ).Assembly
                                             || a == typeof( Enumerable ).Assembly
                                             || a == typeof( TestFixtureAttribute ).Assembly
                                             || a == typeof( MergeTests ).Assembly );

        string code = c1.GetGlobalSource();
        code.ShouldBe(
            "namespace Sub" + Environment.NewLine +
            "{" + Environment.NewLine +
            "using A;" + Environment.NewLine +
            "using B;" + Environment.NewLine +
            "class C" + Environment.NewLine +
            "{" + Environment.NewLine +
            "//t1-Code" + Environment.NewLine +
            "//t1-MoreCode" + "//t2-Code" + Environment.NewLine +
            "//t2-MoreCode" + Environment.NewLine +
            "}" + Environment.NewLine +
            "}" );
    }

    [Test]
    public void merging_code_space_with_parts()
    {
        var c1 = CodeWorkspace.Create();
        var c2 = CodeWorkspace.Create();

        INamespaceScopePart c1Part1 = c1.Global.CreatePart();
        INamespaceScope c1Part1Sub = c1Part1.CreatePart();

        c1.Global.Append( "n°0 (but after having created the 2 parts!)" ).NewLine();
        INamespaceScope c1Part2 = c1.Global.CreatePart();
        c1Part2.Append( "2 - n°0" ).NewLine();
        c1Part2.Append( "2 - n°1" ).NewLine();
        c1Part1.Append( "1 - n°0" ).NewLine();
        c1Part1.Append( "1 - n°1" ).NewLine();
        c1.Global.Append( "n°1" ).NewLine();
        c1Part1Sub.Append( "Hop! (Later but in a sup part of the first part)." ).NewLine();

        INamespaceScope c2Part1 = c1.Global.CreatePart();
        INamespaceScope c2Part2 = c1.Global.CreatePart();
        c2.Global.Append( "!n°0" ).NewLine();
        c2Part2.Append( "!2 - n°0" ).NewLine();
        c2Part2.Append( "!2 - n°1" ).NewLine();
        c2Part1.Append( "!1 - n°0" ).NewLine();
        c2Part1.Append( "!1 - n°1" ).NewLine();
        c2.Global.Append( "!n°1" ).NewLine();

        c1.MergeWith( c2 );

        string code = c1.GetGlobalSource().Trim();
        code.ShouldBe( @"
Hop! (Later but in a sup part of the first part).
1 - n°0
1 - n°1
n°0 (but after having created the 2 parts!)
2 - n°0
2 - n°1
n°1
!1 - n°0
!1 - n°1
!2 - n°0
!2 - n°1
!n°0
!n°1
".Trim().ReplaceLineEndings() );
    }
}

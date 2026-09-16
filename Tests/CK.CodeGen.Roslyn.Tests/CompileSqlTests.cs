using Shouldly;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;

namespace CK.CodeGen.Roslyn.Tests;

public abstract class SimpleBase
{
    public abstract SqlCommand Do( ref int? i );
}

[TestFixture]
public class CompileSqlTests
{
    [Test]
    public void SqlTest()
    {
        ICodeWorkspace workspace = CodeWorkspace.Create();
        INamespaceScope global = workspace.Global;
        INamespaceScope b = global.FindOrCreateNamespace( "CK._g" );

        workspace.EnsureAssemblyReference( typeof( SqlCommand ), typeof( SimpleBase ) );

        b.EnsureUsing( "System" )
         .EnsureUsing( "System.Collections.Generic" )
         .EnsureUsing( "Microsoft.Data.SqlClient" );

        var type = b.CreateType( w => w.Append( "public class GGGG : " ).AppendCSharpName( typeof( SimpleBase ), true, true, true ) );
        type.CreateOverride( typeof( SimpleBase ).GetMethod( "Do" ).ShouldNotBeNull() )
            .Append(
            @"if( i.HasValue )
                {
                    i *= i;
                }
                var c = new SqlCommand(""p""+i.ToString());
                var p = c.Parameters.AddWithValue(""@i"", (object)i ?? DBNull.Value);
                return c;" );

        var source = workspace.GetGlobalSource();
        var references = workspace.AssemblyReferences;

        Assembly a = LocalTestHelper.CreateAssembly( source, references );
        Type t = a.GetTypes().Single( n => n.Name == "GGGG" );
        SimpleBase gotIt = (SimpleBase)Activator.CreateInstance( t ).ShouldNotBeNull();
        int? k = 67;
        SqlCommand cmd = gotIt.Do( ref k );
        k.ShouldBe( 67 * 67 );
        cmd.CommandText.ShouldBe( "p" + k );
        cmd.Parameters.Cast<SqlParameter>().Single().Value.ShouldBe( k );
    }
}

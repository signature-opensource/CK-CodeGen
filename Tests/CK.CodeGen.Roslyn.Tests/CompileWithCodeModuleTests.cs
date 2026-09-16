using Shouldly;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static CK.Testing.MonitorTestHelper;

namespace System
{
    public static class CKFixDBNull
    {
        public static object Value = typeof( object ).Assembly.GetType( "System.DBNull" ).ShouldNotBeNull()
                                                              .GetField( "Value" ).ShouldNotBeNull()
                                                              .GetValue( null ).ShouldNotBeNull();
    }
}

namespace CK.CodeGen.Roslyn.Tests
{

    [TestFixture]
    public class CompileWithCodeModuleTests
    {
        class DoChangeDBNull : ICodeGeneratorModule
        {
            class DBNullToCKFixRewriter : CSharpSyntaxRewriter
            {
                public override SyntaxNode VisitIdentifierName( IdentifierNameSyntax node )
                {
                    if( node.Identifier.Text == "DBNull" )
                    {
                        return node.WithIdentifier( SyntaxFactory.Identifier( "CKFixDBNull" ) );
                    }
                    return node;
                }

                public static SyntaxTree Run( SyntaxTree t )
                {
                    return t.WithRootAndOptions( new DBNullToCKFixRewriter().Visit( t.GetRoot() ), t.Options );
                }
            }

            public IReadOnlyList<SyntaxTree> Rewrite( IReadOnlyList<SyntaxTree> trees )
            {
                return trees.Select( t => DBNullToCKFixRewriter.Run( t ) ).ToList();
            }

            public void Inject( ICodeWorkspace code )
            {
                code.Global
                    .EnsureUsing( "System" )
                    .EnsureUsing( "System.Reflection" )
                    .Append(
                      @"static class CKFixDBNull
                        { 
                            // This was the original required code (before netcoreapp2.0):
                            // public static object Value = typeof( object ).Assembly.GetType( ""System.DBNull"" ).GetField( ""Value"" ).GetValue( null );
                            // Here we use a stupid value for the demo.
                            public static object Value = ""I'm the DBNull."";
                        }" );
            }
        }

        class ThisModuleUsesTheRealDBNullValue : ICodeGeneratorModule
        {
            public IReadOnlyList<SyntaxTree> Rewrite( IReadOnlyList<SyntaxTree> trees ) => trees;

            public void Inject( ICodeWorkspace code )
            {
                code.Global
                    .EnsureUsing( "System" )
                    .CreateType( "public static class RealDBNull" )
                    .Append( "public static object V = DBNull.Value;" ).NewLine();
            }

        }

        [Test]
        public void demonstrating_simple_transformations()
        {
            var workspace = CodeWorkspace.Create();
            workspace.Global
                .FindOrCreateNamespace( "Original" )
                .EnsureUsing( "System" )
                .CreateType( "public static class DBNullWillBeReplaced" )
                .Append( "public static object V = DBNull.Value;" ).NewLine();

            var gen = new CodeGenerator( CodeWorkspace.Factory );
            gen.Modules.Add( new DoChangeDBNull() );
            gen.Modules.Add( new ThisModuleUsesTheRealDBNullValue() );

            var r = gen.Generate( workspace, LocalTestHelper.RandomDllPath, false, Assembly.LoadFrom );
            r.LogResult( TestHelper.Monitor );
            r.Success.ShouldBeTrue();
            r.Assembly.ShouldNotBeNull();
            gen.Modules.ShouldBeEmpty();

            var replaced = r.Assembly.ExportedTypes.Single( t => t.FullName == "Original.DBNullWillBeReplaced" );
            replaced.GetField( "V", BindingFlags.Static | BindingFlags.Public ).ShouldNotBeNull().GetValue( null )
                .ShouldBe( "I'm the DBNull." );

            var real = r.Assembly.ExportedTypes.Single( t => t.FullName == "RealDBNull" );
            real.GetField( "V", BindingFlags.Static | BindingFlags.Public ).ShouldNotBeNull().GetValue( null )
                .ShouldBe( DBNull.Value );

        }
    }
}


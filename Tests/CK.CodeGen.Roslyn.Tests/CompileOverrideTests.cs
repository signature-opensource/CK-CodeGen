using NUnit.Framework;
using System;
using System.Reflection;
using System.Linq;
using Shouldly;

namespace CK.CodeGen.Roslyn.Tests;

public abstract class BaseToBeOverridden
{
    protected BaseToBeOverridden( int val )
    {
        ValFromCtor = val;
    }

    public int ValFromCtor { get; }

    public abstract int Simple1();

    public abstract void VoidMethod();

    protected abstract string Simple2( string x, Guid g );

    protected abstract string VerbatimParameters( string @this, string @operator );

    internal protected abstract BaseToBeOverridden Simple3( out string x, ref Guid g, int p );

}

public abstract class ContainsGenericMethods<T>
{
    public abstract TResult Simple1<TResult>( T arg );

    public virtual bool Simple2<T1, T2>( T1 arg1, T2 arg2 )
    {
        return false;
    }
}

[TestFixture]
public class CompileOverrideTests
{
    [Test]
    public void BaseTest()
    {
        ICodeWorkspace workspace = CodeWorkspace.Create();
        INamespaceScope global = workspace.Global;
        INamespaceScope b = global.FindOrCreateNamespace( "CK._g" );

        Type t = typeof( BaseToBeOverridden );

        workspace.EnsureAssemblyReference( t );

        b.EnsureUsing( "System" )
         .EnsureUsing( "System.Collections.Generic" )
         .EnsureUsing( t.Namespace.ShouldNotBeNull() );

        var c = b.CreateType( h => h.Append( "class Specialized : " ).AppendCSharpName( t, true, true, true ) );
        c.CreatePassThroughConstructors( t );

        c.CreateOverride( t.GetMethod( "Simple1" ).ShouldNotBeNull() )
         .Append( "=> 3712;" );

        c.CreateOverride( t.GetMethod( "VoidMethod" ).ShouldNotBeNull() );

        c.CreateOverride( t.GetMethod( "Simple2", BindingFlags.Instance | BindingFlags.NonPublic ).ShouldNotBeNull() )
            .Append( "=> x + '-' + g.ToString();" );

        c.CreateOverride( t.GetMethod( "Simple3", BindingFlags.Instance | BindingFlags.NonPublic ).ShouldNotBeNull() )
            .Append( "g = Guid.NewGuid();" ).NewLine()
            .Append( @"x = ""Hello World!"" + Simple2( ""YES"", g );" ).NewLine()
            .Append( "return this;" );

        c.CreateOverride( t.GetMethod( "VerbatimParameters", BindingFlags.Instance | BindingFlags.NonPublic ).ShouldNotBeNull() ).Append( " => @this + @operator;" );

        Assembly a = LocalTestHelper.CreateAssembly( workspace.GetGlobalSource(), workspace.AssemblyReferences );

        Type tC = a.GetTypes().Single( n => n.Name == "Specialized" );
        BaseToBeOverridden gotIt = (BaseToBeOverridden)Activator.CreateInstance( tC, [3712 * 3712] ).ShouldNotBeNull();
        gotIt.ValFromCtor.ShouldBe( 3712 * 3712 );
        gotIt.Simple1().ShouldBe( 3712 );
        string s;
        Guid g = Guid.Empty;
        gotIt.Simple3( out s, ref g, 9 ).ShouldBeSameAs( gotIt );
        s.ShouldBe( "Hello World!YES-" + g.ToString() );
        g.ShouldNotBe( Guid.Empty );
    }

    [Test]
    public void BuildGenericMethods()
    {
        ICodeWorkspace workspace = CodeWorkspace.Create();
        INamespaceScope global = workspace.Global;
        INamespaceScope b = global.FindOrCreateNamespace( "CK._g" );

        Type t = typeof( ContainsGenericMethods<> );

        workspace.EnsureAssemblyReference( t );

        b.EnsureUsing( t.Namespace.ShouldNotBeNull() );
        var c = b.CreateType( header => header.Append( "class Specialized<T> : " ).AppendCSharpName( t, true, true, true ).NewLine() );
        c.CreateOverride( t.GetMethod( "Simple1" ).ShouldNotBeNull() )
            .Append( "if (arg.Equals(default(T))) throw new System.ArgumentException();" ).NewLine()
            .Append( "return default(TResult);" );

        c.CreateOverride( t.GetMethod( "Simple2" ).ShouldNotBeNull() )
            .Append( "=> arg2 is T1;" );

        Assembly a = LocalTestHelper.CreateAssembly( workspace.GetGlobalSource(), workspace.AssemblyReferences );

        Type tC = a.GetTypes().Single( n => n.Name == "Specialized`1" ).MakeGenericType( typeof( int ) );
        ContainsGenericMethods<int> gotIt = (ContainsGenericMethods<int>)Activator.CreateInstance( tC ).ShouldNotBeNull();
        gotIt.Simple1<bool>( 25 ).ShouldBeFalse();
        gotIt.Simple2( new object(), "test" ).ShouldBeTrue();
    }
}

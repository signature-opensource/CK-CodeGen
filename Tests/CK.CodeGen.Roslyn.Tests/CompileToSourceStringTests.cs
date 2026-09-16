using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using CK.Core;
using NUnit.Framework.Internal;

namespace CK.CodeGen.Roslyn.Tests;

[TestFixture]
public class CompileToSourceStringTests
{
    [Test]
    public void parsing_only_tests()
    {
        var workspace = CodeWorkspace.Create();
        var global = workspace.Global;
        global.EnsureUsing( "System" );
        global.CreateType( "public class Tester" )
                 .Append( "public bool OK => true;" ).NewLine();

        Assembly a = LocalTestHelper.CreateAssembly( workspace.GetGlobalSource(), workspace.AssemblyReferences );
        a.ShouldNotBeNull();

        var g = new CodeGenerator( CodeWorkspace.Factory );
        var r = g.Generate( workspace, null, skipCompilation: true );
        r.Success.ShouldBeTrue();
        r.Sources.Count.ShouldBe( 1 );
    }

    [Test]
    public void simple_generation_or_parsing_code_string()
    {
        {
            var r = CodeGenerator.Generate( "class P {}", null );
            r.Success.ShouldBeTrue();
            r.Sources.Count.ShouldBe( 1 );
            r.ParseDiagnostics.ShouldBeEmpty();
        }
        {
            var r = CodeGenerator.Generate( "class P }", null );
            r.Success.ShouldBeFalse();
            r.Sources.Count.ShouldBe( 1 );
            r.ParseDiagnostics.ShouldNotBeEmpty();
        }
    }

    [Test]
    public void writing_and_reading_simple_values_of_all_known_types()
    {
        var workspace = CodeWorkspace.Create();
        var global = workspace.Global;

        #region Injected code
        DateTime tD = new DateTime( 2017, 5, 27, 13, 47, 23, DateTimeKind.Local );
        DateTimeOffset tO = new DateTimeOffset( tD.Ticks, TimeSpan.FromMinutes( 42 ) );
        TimeSpan tT = TimeSpan.FromMilliseconds( 987897689 );
        Guid g = new Guid( "4CCF5143-11C2-485F-93CE-76B4B8E1AFA0" );

        object?[] array =
        [
            null,
            true,
            false,
            "Hello World!",
            (sbyte)1,
            (short)2,
            3,
            4398798739879743,
            (byte)5,
            (ushort)6,
            (uint)7,
            (ulong)8,
            'a',
            '\uD800', // A high surrogate
            '\uDC00', // A low surrogate
            '\0',
            '\u3712',
            '\t',
            4.89e-2,
            4.8965e-2f,
            new Decimal( Math.E ),
            g,
            tD,
            tO,
            tT,
            System.Type.Missing,
            typeof(Dictionary<string,int>),
        ];
        #endregion

        global.EnsureUsing( "System" )
              .EnsureUsing( "NUnit.Framework" )
              .EnsureUsing( "CK.Core" )
              .EnsureUsing( "System.Collections.Generic" )
              .EnsureUsing( "System.Linq" );

        global.CreateType( "public class Tester" )
            .Append( "public string Run() {" ).NewLine()
            .Append( @"
            DateTime tD = new DateTime(2017,5,27,13,47,23,DateTimeKind.Local);
            DateTimeOffset tO = new DateTimeOffset( tD.Ticks, TimeSpan.FromMinutes(42) );
            TimeSpan tT = TimeSpan.FromMilliseconds( 987897689 );
            Guid g = new Guid( ""4CCF5143-11C2-485F-93CE-76B4B8E1AFA0"" );

            object[] array = new object[]
            {
                null,
                true,
                false,
                ""Hello World!"",
                (sbyte)1,
                (short)2,
                3,
                4398798739879743,
                (byte)5,
                (ushort)6,
                (uint)7,
                (ulong)8,
                'a',
                '\uD800', // A high surrogate
                '\uDC00', // A low surrogate
                '\0',
                '\u3712',
                '\t',
                4.89e-2,
                4.8965e-2f,
                new decimal( Math.E ),
                g,
                tD,
                tO,
                tT,
                System.Type.Missing,
                typeof(Dictionary<string,int>),
            }; " )
            .NewLine()
            .Append( "var rewrite = " ).AppendArray( array ).Append( ";" ).NewLine()
            .Append( @"
                   var diff = array
                               .Select( ( o, idx ) => new { O = o, T = rewrite[idx], I = idx } )
                               .Where( x => (x.O == null && x.T != null) || (x.O != null && !x.O.Equals( x.T )) )
                               .Select( x => $""{x.I} - {x.O} != {x.T}"" )
                               .Concatenate();
                   return diff;" ).NewLine()
            .Append( "}" )
            .NewLine();

        workspace.EnsureAssemblyReference(
            typeof( object ),
            typeof( ROSpanCharMatcher ),
            typeof( System.Diagnostics.Debug ),
            typeof( System.Linq.Enumerable ),
            typeof( TestFixtureAttribute )
        );

        var source = workspace.GetGlobalSource();
        var references = workspace.AssemblyReferences;
        Assembly a = LocalTestHelper.CreateAssembly( source, references );
        object tester = Activator.CreateInstance( a.ExportedTypes.Single( t => t.Name == "Tester" ) ).ShouldNotBeNull();
        string diff = (string)tester.GetType().GetMethod( "Run" ).ShouldNotBeNull().Invoke( tester, [] ).ShouldNotBeNull();
        diff.ShouldBeEmpty();
    }

    public class Nested<T>
    {
        public Nested() { }

        int PrivateVal( int i ) => i * i;

        int PrivateVal( string s ) => s.Length;

        int PrivateProp => 37132;

        public event EventHandler<int>? Event;
    }

    [Test]
    public void writing_any_MemberInfo_uses_the_Module()
    {
        var workspace = CodeWorkspace.Create();
        workspace.EnsureAssemblyReference( typeof( CompileToSourceStringTests ), typeof( MethodInfo ) );

        var t = workspace.Global.CreateType( "public class MemberFinder" );
        t.Namespace.EnsureUsing( "System.Reflection" );

        var thisTestMethod = typeof( CompileToSourceStringTests ).GetMethod( "writing_any_MemberInfo_uses_the_Module" );
        var ValIGen = typeof( Nested<> ).GetMethod( "Val", new[] { typeof( int ) } );
        var ValSGen = typeof( Nested<> ).GetMethod( "Val", new[] { typeof( string ) } );
        var PropGen = typeof( Nested<> ).GetProperty( "Prop" );
        var EventGen = typeof( Nested<> ).GetEvent( "Event" );
        var CtorGen = typeof( Nested<> ).GetConstructor( Type.EmptyTypes );

        var ValI = typeof( Nested<TestAttribute> ).GetMethod( "Val", new[] { typeof( int ) } );
        var ValS = typeof( Nested<Comparer<List<KeyValuePair<int, byte>>>> ).GetMethod( "Val", new[] { typeof( string ) } );
        var Prop = typeof( Nested<Test> ).GetProperty( "Prop" );
        var Event = typeof( Nested<string> ).GetEvent( nameof( Nested<string>.Event ) );
        var Ctor = typeof( Nested<ICodePart> ).GetConstructor( Type.EmptyTypes );

        t.Append( "public readonly static MethodInfo ThisTestMethod = " ).Append( thisTestMethod ).Append( ";" );

        t.Append( "public readonly static MethodInfo ValIGen = " ).Append( ValIGen ).Append( ";" );
        t.Append( "public readonly static MethodInfo ValSGen = " ).Append( ValSGen ).Append( ";" );
        t.Append( "public readonly static PropertyInfo PropGen = " ).Append( PropGen ).Append( ";" );
        t.Append( "public readonly static EventInfo EventGen = " ).Append( EventGen ).Append( ";" );
        t.Append( "public readonly static ConstructorInfo CtorGen = " ).Append( CtorGen ).Append( ";" );

        t.Append( "public readonly static MethodInfo ValI = " ).Append( ValI ).Append( ";" );
        t.Append( "public readonly static MethodInfo ValS = " ).Append( ValS ).Append( ";" );
        t.Append( "public readonly static PropertyInfo Prop = " ).Append( Prop ).Append( ";" );
        t.Append( "public readonly static EventInfo Event = " ).Append( Event ).Append( ";" );
        t.Append( "public readonly static ConstructorInfo Ctor = " ).Append( Ctor ).Append( ";" );

        Assembly a = LocalTestHelper.CreateAssembly( workspace.GetGlobalSource(), workspace.AssemblyReferences );
        Type memberFinder = a.ExportedTypes.Single( t => t.Name == "MemberFinder" );

        var eThisTestMethod = (MethodInfo?)memberFinder.GetField( "ThisTestMethod" ).ShouldNotBeNull().GetValue( null );
        eThisTestMethod.ShouldBeSameAs( thisTestMethod );

        var eValIGen = (MethodInfo?)memberFinder.GetField( "ValIGen" ).ShouldNotBeNull().GetValue( null );
        eValIGen.ShouldBeSameAs( ValIGen );

        var eValSGen = (MethodInfo?)memberFinder.GetField( "ValSGen" ).ShouldNotBeNull().GetValue( null );
        eValSGen.ShouldBeSameAs( ValSGen );

        var ePropGen = (PropertyInfo?)memberFinder.GetField( "PropGen" ).ShouldNotBeNull().GetValue( null );
        ePropGen.ShouldBeSameAs( PropGen );

        var eEventGen = (EventInfo?)memberFinder.GetField( "EventGen" ).ShouldNotBeNull().GetValue( null );
        eEventGen.ShouldBeSameAs( EventGen );

        var eCtorGen = (ConstructorInfo?)memberFinder.GetField( "CtorGen" ).ShouldNotBeNull().GetValue( null );
        eCtorGen.ShouldBeSameAs( CtorGen );

        var eValI = (MethodInfo?)memberFinder.GetField( "ValI" ).ShouldNotBeNull().GetValue( null );
        eValI.ShouldBeSameAs( ValI );

        var eValS = (MethodInfo?)memberFinder.GetField( "ValS" ).ShouldNotBeNull().GetValue( null );
        eValS.ShouldBeSameAs( ValS );

        var eProp = (PropertyInfo?)memberFinder.GetField( "Prop" ).ShouldNotBeNull().GetValue( null );
        eProp.ShouldBeSameAs( Prop );

        var eEvent = (EventInfo?)memberFinder.GetField( "Event" ).ShouldNotBeNull().GetValue( null );
        eEvent.ShouldBeSameAs( Event );

        var eCtor = (ConstructorInfo?)memberFinder.GetField( "Ctor" ).ShouldNotBeNull().GetValue( null );
        eCtor.ShouldBeSameAs( Ctor );
    }
}

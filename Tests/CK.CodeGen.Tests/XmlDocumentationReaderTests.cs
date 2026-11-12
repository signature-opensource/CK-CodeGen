using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static CK.Testing.MonitorTestHelper;

namespace CK.CodeGen.Tests;

/// <summary>
/// A documented type.
/// </summary>
public class DocumentedSample
{
    /// <summary>
    /// A documented nested type.
    /// </summary>
    public sealed class Nested<T>
    {
    }

    /// <summary>
    /// A documented property.
    /// </summary>
    public int Prop { get; set; }

    /// <summary>
    /// A documented field.
    /// </summary>
    public string? Field;

    /// <summary>
    /// A documented event.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// A documented constructor.
    /// </summary>
    public DocumentedSample() { }

    /// <summary>
    /// A documented method.
    /// </summary>
    public void Method( int i, List<string> l, Dictionary<int, Nested<string>> d ) { }

    /// <summary>
    /// A documented generic method.
    /// </summary>
    public T Generic<T>( T value, int[] values ) => value;

    void Raise() => Changed?.Invoke( this, EventArgs.Empty );
}

[TestFixture]
public class XmlDocumentationReaderTests
{
    static readonly Type _t = typeof( DocumentedSample );

    [Test]
    public void documentation_names_of_the_members()
    {
        XmlDocumentationReader.GetNameAttributeValueFor( _t ).ShouldBe( "T:CK.CodeGen.Tests.DocumentedSample" );
        XmlDocumentationReader.GetNameAttributeValueFor( typeof( DocumentedSample.Nested<> ) )
            .ShouldBe( "T:CK.CodeGen.Tests.DocumentedSample.Nested`1" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetProperty( "Prop" )! ).ShouldBe( "P:CK.CodeGen.Tests.DocumentedSample.Prop" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetField( "Field" )! ).ShouldBe( "F:CK.CodeGen.Tests.DocumentedSample.Field" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetEvent( "Changed" )! ).ShouldBe( "E:CK.CodeGen.Tests.DocumentedSample.Changed" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetConstructor( Type.EmptyTypes )! ).ShouldBe( "M:CK.CodeGen.Tests.DocumentedSample.#ctor" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetMethod( "Method" )! )
            .ShouldBe( "M:CK.CodeGen.Tests.DocumentedSample.Method(System.Int32,System.Collections.Generic.List{System.String},System.Collections.Generic.Dictionary{System.Int32,CK.CodeGen.Tests.DocumentedSample.Nested{System.String}})" );
        XmlDocumentationReader.GetNameAttributeValueFor( _t.GetMethod( "Generic" )! )
            .ShouldBe( "M:CK.CodeGen.Tests.DocumentedSample.Generic``1(``0,System.Int32[])" );
    }

    [Test]
    public void the_trailing_member_name_is_appended()
    {
        XmlDocumentationReader.GetNameAttributeValueFor( "F:", typeof( DayOfWeek ), "Monday" ).ShouldBe( "F:System.DayOfWeek.Monday" );
    }

    [Test]
    public void documentation_elements_are_found_in_the_xml_file()
    {
        var cache = new Dictionary<object, object?>();
        var members = new MemberInfo[]
        {
            _t,
            typeof( DocumentedSample.Nested<> ),
            _t.GetProperty( "Prop" )!,
            _t.GetField( "Field" )!,
            _t.GetEvent( "Changed" )!,
            _t.GetConstructor( Type.EmptyTypes )!,
            _t.GetMethod( "Method" )!,
            _t.GetMethod( "Generic" )!
        };
        var summaries = XmlDocumentationReader.GetDocumentationFor( TestHelper.Monitor, members, cache )
                                              .Select( e => e.Element( "summary" )!.Value.Trim() )
                                              .ToArray();
        summaries.ShouldBe( new[] { "A documented type.",
                                    "A documented nested type.",
                                    "A documented property.",
                                    "A documented field.",
                                    "A documented event.",
                                    "A documented constructor.",
                                    "A documented method.",
                                    "A documented generic method." } );
        cache.Keys.ShouldHaveSingleItem().ShouldBe( _t.Assembly );
    }

    [Test]
    public void a_missing_xml_file_gives_null()
    {
        // The runtime assemblies have no xml documentation file next to them (it is in the reference packs).
        var cache = new Dictionary<object, object?>();
        XmlDocumentationReader.GetDocumentationFor( TestHelper.Monitor, typeof( DayOfWeek ), cache ).ShouldBeNull();
        cache[typeof( DayOfWeek ).Assembly].ShouldBeNull();
    }
}

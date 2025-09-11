using CK.Core;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.CodeGen.Tests;

public partial class NullableTypeTests
{

#pragma warning disable IDE1006, IDE0044, 0414 // Naming Styles, Add readonly modifier, The field is assigned but its value is never used.

    (int?, string?, Action) ValueTuple1 = (1, null, () => { });
    (int?, string?, Action)? ValueTuple2 = null;
    List<(int?, string?, Action)>? ListValueTuple = null;

    (int? X, string? Y, Action) ValueTupleWithName1 = (1, null, () => { });
    (int? X, string? Y, Action)? ValueTupleWithName2 = null;
    List<(int? X, string? Y, Action)>? ListValueTupleWithName = null;

    Dictionary<(int? X, string? Y, Action), ((int? A, int B), string? C, (float, int D))>? DictionaryWithName = null;
    (int? X, string? Y, Action, int? A, int B, string? C, float, int D, double, object E)? LongTupleWithName = null;
    Dictionary<string, ((int? A, int B), string? C, (float, int D))>? DictionaryNoKeyWithName = null;
#pragma warning restore IDE1006 // Naming Styles

    // http://mustoverride.com/tuples_names/
    // Element names are semantically insignificant except when used directly.
    [Test]
    public void value_tuple_field_name_erasure()
    {
        var v1 = GetNullableTypeTree( "ValueTuple1" );
        var v2 = GetNullableTypeTree( "ValueTuple2" );
        v1.Kind.IsTupleType().ShouldBeTrue();
        v1.Type.ShouldBeSameAs( v2.Type );

        var vL = GetNullableTypeTree( "ListValueTuple" );
        vL.RawSubTypes[0].Kind.IsTupleType().ShouldBeTrue();
        vL.RawSubTypes[0].Type.ShouldBeSameAs( v1.Type );

        var nv1 = GetNullableTypeTree( "ValueTupleWithName1" );
        var nv2 = GetNullableTypeTree( "ValueTupleWithName2" );
        nv1.Kind.IsTupleType().ShouldBeTrue();
        nv1.Type.ShouldBeSameAs( nv2.Type );

        var nvL = GetNullableTypeTree( "ListValueTupleWithName" );
        nvL.RawSubTypes[0].Kind.IsTupleType().ShouldBeTrue();
        nvL.RawSubTypes[0].Type.ShouldBeSameAs( nv1.Type );

        nv1.Type.ShouldBeSameAs( v1.Type );
    }

    [Test]
    public void value_tuple_field_names_from_generic_parameters_is_breadth_first()
    {
        var fieldInfo = GetType()!.GetField( "ListValueTupleWithName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
        Throw.DebugAssert( fieldInfo != null );
        var attrs = fieldInfo.GetCustomAttributes( false );
        var names = attrs.OfType<TupleElementNamesAttribute>().Single();
        names.TransformNames[0].ShouldBe( "X" );
        names.TransformNames[1].ShouldBe( "Y" );
        names.TransformNames[2].ShouldBeNull();

        fieldInfo = GetType()!.GetField( "DictionaryWithName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
        Debug.Assert( fieldInfo != null );
        attrs = fieldInfo.GetCustomAttributes( false );
        names = attrs.OfType<TupleElementNamesAttribute>().Single();

        //Dictionary<(int? X, string? Y, Action), ((int? A, int B), string? C, (float, int D))>
        names.TransformNames.SequenceEqual( new[]
        {
            "X", "Y", null, // Action has no name
            null, // (int? A, int B) has no name.
            "C",
            null, // (float, int D) has no name.
            "A",
            "B",
            null, // float has no name.
            "D"
        } ).ShouldBeTrue();


        fieldInfo = GetType()!.GetField( "LongTupleWithName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
        Debug.Assert( fieldInfo != null );
        attrs = fieldInfo.GetCustomAttributes( false );
        names = attrs.OfType<TupleElementNamesAttribute>().Single();

        // Names are flattened.
        names.TransformNames.SequenceEqual( new[]
        {
            "X",
            "Y",
            null,
            "A",
            "B",
            "C",
            null,
            "D",
            null,
            "E",
            null,
            null,
            null
        } ).ShouldBeTrue();

        fieldInfo = GetType()!.GetField( "DictionaryNoKeyWithName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
        Debug.Assert( fieldInfo != null );
        attrs = fieldInfo.GetCustomAttributes( false );
        names = attrs.OfType<TupleElementNamesAttribute>().Single();
        // Only ValueTuples matter.
        // Dictionary<string, ((int? A, int B), string? C, (float,int D))>
        names.TransformNames.SequenceEqual( new[]
        {
            null, // (int? A, int B) has no name.
            "C",
            null, // (float, int D) has no name.
            "A",
            "B",
            null, // float has no name.
            "D"
        } ).ShouldBeTrue();


    }

    record struct RecordCanBeEmpty();
    record struct SampleRecord( string Name, (int A, string B) Val );

    [Test]
    public void record_struct_has_no_code_generated_attribute()
    {
        var attrs = typeof( SampleRecord ).GetCustomAttributesData();
        attrs.ShouldHaveSingleItem().ShouldSatisfyAllConditions( a =>
        {
            a.AttributeType.Name.ShouldBe( "NullableAttribute" );
            a.AttributeType.Namespace.ShouldBe( "System.Runtime.CompilerServices" );
        } );
        var ctors = typeof( SampleRecord ).GetConstructors();
        ctors.Count().ShouldBe( 1 );
        ctors[0].GetCustomAttributes( true ).ShouldBeEmpty();
    }

    public interface IPocoLike
    {
        (int A, double? B, List<int>? C)? Tuple { get; }
    }

    public interface IPocoLikeRef
    {
        ref (int A, double? B, List<int>? C)? Tuple { get; }
    }

    [TestCase( "ref" )]
    [TestCase( "val" )]
    public void ref_properties_are_handled_by_NullabilityInfoContext( string mode )
    {
        var pTuple = (mode == "ref" ? typeof( IPocoLikeRef ) : typeof( IPocoLike )).GetProperty( "Tuple" );
        Debug.Assert( pTuple != null );
        pTuple.PropertyType.IsByRef.ShouldBe( mode == "ref" );

        var nInfo = (new NullabilityInfoContext()).Create( pTuple );
        CheckCorrectTupleNulabilities( nInfo );
    }

    static void CheckCorrectTupleNulabilities( NullabilityInfo nInfo )
    {
        nInfo.WriteState.ShouldBe( NullabilityState.Unknown );
        nInfo.ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.ElementType.ShouldBeNull( null );
        nInfo.GenericTypeArguments.Count().ShouldBe( 3 );
        nInfo.GenericTypeArguments[0].ReadState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[0].WriteState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[1].ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[1].WriteState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].WriteState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].GenericTypeArguments[0].ReadState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[2].GenericTypeArguments[0].WriteState.ShouldBe( NullabilityState.NotNull );
    }

    [TestCase( "ref" )]
    [TestCase( "val" )]
    public void the_TEMPNullabilityInfo_must_be_temporarily_used( string mode )
    {
        var pTuple = (mode == "ref" ? typeof( IPocoLikeRef ) : typeof( IPocoLike )).GetProperty( "Tuple" );
        Debug.Assert( pTuple != null );
        pTuple.PropertyType.IsByRef.ShouldBe( mode == "ref" );

        var nInfo = (new TEMPNullabilityInfoContext()).Create( pTuple );
        CheckCorrectTupleNulabilities( nInfo );
    }

    static void CheckCorrectTupleNulabilities( TEMPNullabilityInfo nInfo )
    {
        nInfo.WriteState.ShouldBe( NullabilityState.Unknown );
        nInfo.ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.ElementType.ShouldBeNull( null );
        nInfo.GenericTypeArguments.Count().ShouldBe( 3 );
        nInfo.GenericTypeArguments[0].ReadState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[0].WriteState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[1].ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[1].WriteState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].ReadState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].WriteState.ShouldBe( NullabilityState.Nullable );
        nInfo.GenericTypeArguments[2].GenericTypeArguments[0].ReadState.ShouldBe( NullabilityState.NotNull );
        nInfo.GenericTypeArguments[2].GenericTypeArguments[0].WriteState.ShouldBe( NullabilityState.NotNull );
    }
}

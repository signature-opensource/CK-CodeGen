using NUnit.Framework;
using System.Collections.Generic;

namespace CK.CodeGen.Tests;

[TestFixture]
public partial class NullableTypeTests
{

#pragma warning disable CS0414 //  The field 'NullableTypeTests.Vd' is assigned but its value is never used.
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable 0649 // Field 'is never assigned to, and will always have its default value null
#pragma warning disable IDE1006 // Naming Styles


    List<string?> Ta1 = new();
    List<string> Ta2 = new();
    List<string?> Ma = new();

    List<(string?, NullableTypeTests)> Tb1 = new();
    List<(string, NullableTypeTests)> Tb2 = new();
    List<(string?, NullableTypeTests)> Mb = new();

    List<(string, NullableTypeTests?)> Tc1 = new();
    List<(string, NullableTypeTests)> Tc2 = new();
    List<(string, NullableTypeTests?)> Mc = new();

    List<(string?, NullableTypeTests?)> Td1 = new();
    List<(string?, NullableTypeTests)> Td2 = new();
    List<(string?, NullableTypeTests?)> Md = new();

    Dictionary<int, List<(ISet<string?>, IList<NullableTypeTests?>)>> Te1 = new();
    Dictionary<int, List<(ISet<string>?, IList<NullableTypeTests>)>> Te2 = new();
    Dictionary<int, List<(ISet<string?>?, IList<NullableTypeTests?>)>> Me = new();

    (string?, int, NullableTypeTests?, string) Tf1 = new();
    (string, int, NullableTypeTests, string?) Tf2 = new();
    (string?, int, NullableTypeTests?, string?) Mf = new();

    (string?, int, NullableTypeTests, string?, (string, string, int?, NullableTypeTests), string, ISet<(string?, string)>, string, string, string?) Tg1 = new();
    (string, int, NullableTypeTests?, string, (string?, string, int?, NullableTypeTests?), string, ISet<(string, string)>, string, string, string?) Tg2 = new();
    (string?, int, NullableTypeTests?, string?, (string?, string, int?, NullableTypeTests?), string, ISet<(string?, string)>, string, string, string?) Mg = new();

    List<string?> Th1 = new();
    List<string>? Th2 = new();
    List<string?>? Mh = new();


    [TestCase( "Ta1", "Ta2", "Ma" )]
    [TestCase( "Tb1", "Tb2", "Mb" )]
    [TestCase( "Tc1", "Tc2", "Mc" )]
    [TestCase( "Td1", "Td2", "Md" )]
    [TestCase( "Te1", "Te2", "Me" )]
    [TestCase( "Tf1", "Tf2", "Mf" )]
    [TestCase( "Tg1", "Tg2", "Mg" )]
    [TestCase( "Th1", "Th2", "Mh" )]
    public void merging_reference_types_nullabilities( string n1, string n2, string merged )
    {
        var t1 = GetNullableTypeTree( n1 );
        var t2 = GetNullableTypeTree( n2 );
        var tM = GetNullableTypeTree( merged );

        t1.ShouldNotBe( t2 );

        t1.MergeReferenceTypesNullability( t2 ).ShouldBe( tM );
        t2.MergeReferenceTypesNullability( t1 ).ShouldBe( tM );
    }

    decimal? Va1 = new();
    decimal Va2 = new();
    decimal? Va = new();

    (decimal?, int, NullableTypeTests?, string) Vb1 = new();
    (decimal, int, NullableTypeTests, string?) Vb2 = new();
    (decimal?, int, NullableTypeTests?, string?) Vb = new();

    ((long, byte), decimal?)? Vc1 = new();
    ((long?, byte?)?, decimal)? Vc2 = new();
    ((long?, byte?)?, decimal?)? Vc = new();

    (decimal?, int?)? Vd1 = new();
    (decimal, int) Vd2 = new();
    (decimal?, int?)? Vd = new();

    (decimal?, (int?, byte?)?)? Ve1 = new();
    (decimal, (int?, byte)) Ve2 = new();
    (decimal?, (int?, byte?)?)? Ve = new();

    (List<decimal?>, ISet<(int?, byte)?>, ISet<(int?, byte?)>?, ISet<(int, byte?)>?)? Vf1 = new();
    (List<decimal?>, ISet<(int, byte?)>?, ISet<(int, byte)>, ISet<(int, byte)?>?)? Vf2 = new();
    (List<decimal?>, ISet<(int?, byte?)?>?, ISet<(int?, byte?)>?, ISet<(int, byte?)?>?)? Vf = new();

    List<int> Vg1 = new();
    List<byte> Vg2 = new();

    List<float> Vh1 = new();
    List<double> Vh2 = new();

    (decimal?, (int?, long?)?)? Vi1 = new();
    (decimal, (int?, byte)) Vi2 = new();


    [TestCase( "Ta1", "Ta2", "Ma" )]
    [TestCase( "Tb1", "Tb2", "Mb" )]
    [TestCase( "Tc1", "Tc2", "Mc" )]
    [TestCase( "Td1", "Td2", "Md" )]
    [TestCase( "Te1", "Te2", "Me" )]
    [TestCase( "Tf1", "Tf2", "Mf" )]
    [TestCase( "Tg1", "Tg2", "Mg" )]

    [TestCase( "Va1", "Va2", "Va" )]
    [TestCase( "Vb1", "Vb2", "Vb" )]
    [TestCase( "Vc1", "Vc2", "Vc" )]
    [TestCase( "Vd1", "Vd2", "Vd" )]
    [TestCase( "Ve1", "Ve2", "Ve" )]
    [TestCase( "Vf1", "Vf2", "Vf" )]
    [TestCase( "Vg1", "Vg2", null )]
    [TestCase( "Vh1", "Vh2", null )]
    [TestCase( "Vi1", "Vi2", null )]
    public void merging_all_nullabilities( string n1, string n2, string? merged )
    {
        var t1 = GetNullableTypeTree( n1 );
        var t2 = GetNullableTypeTree( n2 );
        t1.ShouldNotBe( t2 );

        var tM = merged != null ? GetNullableTypeTree( merged ) : (NullableTypeTree?)null;

        var m1 = t1.TryMergeNullabilities( t2 );
        m1.ShouldBe( tM );

        var m2 = t2.TryMergeNullabilities( t1 );
        m2.ShouldBe( tM );

    }

}

using CK.Core;
using NUnit.Framework;
using Shouldly;
using System;
using System.Xml.Linq;

namespace CK.CodeGen.Tests;

[TestFixture]
public class DocumentationTextBuilderTests
{
    static string Markdown( string xml, Action<DocumentationTextBuilder>? configure = null )
    {
        var b = new DocumentationTextBuilder();
        configure?.Invoke( b );
        b.AppendSummaryAndRemarks( new[] { XElement.Parse( $"<member>{xml}</member>" ) } );
        return b.ToString().ReplaceLineEndings( "\n" );
    }

    [Test]
    public void summary_and_remarks_become_markdown()
    {
        Markdown( """
            <summary>
            The <c>Name</c> of a <see cref="T:CK.Core.IResourceGroup"/>, see <see cref="M:CK.Core.Foo.Bar(System.Int32)"/>
            and <see cref="M:CK.Core.Foo.#ctor"/> (<paramref name="x"/>, <see langword="null"/>).
            <para>
            A second paragraph.
            </para>
            </summary>
            <remarks>Some remarks.</remarks>
            <param name="x">Ignored.</param>
            """ ).ShouldBe( """
            The `Name` of a IResourceGroup, see Foo.Bar
            and Foo (x, `null`).

            A second paragraph.

            Some remarks.
            """.ReplaceLineEndings( "\n" ) );
    }

    [Test]
    public void the_text_of_a_reference_wins_and_a_handler_can_format_it()
    {
        Markdown( """<summary>See <see cref="T:A.B.C">the C type</see>.</summary>""" ).ShouldBe( "See the C type." );
        var md = new DocumentationTextBuilder()
                    .AppendLinesFromXElement( XElement.Parse( """<summary>See <see cref="P:A.B.C.Name"/>.</summary>""" ),
                                              ( kind, type, member, text ) => $"[{kind}|{type}|{member}]" )
                    .ToString();
        md.ShouldBe( "See [P|A.B.C|Name]." );
    }

    [Test]
    public void unsupported_references_are_kept_or_throw()
    {
        Markdown( """<summary>In <see cref="N:CK.Core"/>.</summary>""" ).ShouldBe( "In CK.Core." );
        Markdown( """<summary>In <see cref="!Unknown"/>.</summary>""" ).ShouldBe( "In ~~!Unknown~~." );
        Should.Throw<CKException>( () => Markdown( """<summary>In <see cref="N:CK.Core"/>.</summary>""", b => b.ThrowOnUnsupportedCodeRef = true ) );
    }

    [Test]
    public void the_gets_or_sets_prefix_can_be_removed()
    {
        var b = new DocumentationTextBuilder();
        using( b.RemoveGetOrSetPrefix() )
        {
            b.AppendSummaryAndRemarks( new[] { XElement.Parse( "<member><summary>Gets or sets the name.</summary></member>" ) } );
        }
        b.ToString().ShouldBe( "The name." );
        b.Reset().AppendSummaryAndRemarks( new[] { XElement.Parse( "<member><summary>Gets the name.</summary></member>" ) } );
        b.ToString().ShouldBe( "Gets the name." );
    }

    [Test]
    public void a_header_and_a_line_prefix()
    {
        var b = new DocumentationTextBuilder( "/**", " * " );
        b.IsEmpty.ShouldBeTrue();
        b.ToString().ShouldBeEmpty();
        b.AppendText( "A", true, true, true, false ).AppendEmptyLine().AppendText( "B", true, true, true, false );
        b.ToString().ReplaceLineEndings( "\n" ).ShouldBe( "/**\n * A\n * \n * B" );
    }
}

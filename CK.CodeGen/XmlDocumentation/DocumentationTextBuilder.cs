using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CK.CodeGen;

/// <summary>
/// Builds a Markdown text from C# Xml documentation (see <see cref="XmlDocumentationReader"/>): &lt;c&gt; and
/// &lt;code&gt; become ` and ``` markers, &lt;para&gt; a blank line, the code references (&lt;see&gt;, &lt;seealso&gt;)
/// are handled by a <see cref="CodeRefHandler"/>, &lt;paramref&gt; and &lt;typeparamref&gt; become their name, the other
/// elements are "transparent". The common white space prefix of the lines is removed and consecutive empty lines are
/// collapsed.
/// <para>
/// The lines may be prefixed (<see cref="DocumentationTextBuilder(string, string)"/>): a TypeScript comment is a
/// "/**" header and " * " prefixed lines, for instance.
/// </para>
/// </summary>
public sealed partial class DocumentationTextBuilder
{
    readonly StringBuilder _b;
    readonly string _header;
    readonly string _linePrefix;
    int _removeGetOrSetPrefix;
    bool _lastLineIsEmpty;
    bool _waitingForNewline;

    /// <summary>
    /// Computes the text of a code reference (typically &lt;see cref="..." /&gt;).
    /// </summary>
    /// <param name="targetKind">The target kind: 'T', 'M', 'P', 'F' or 'E'.</param>
    /// <param name="targetType">The target type in dotted notation (nested types also use a dot).</param>
    /// <param name="targetMember">For 'M', 'P', 'F' or 'E', the member name ("#ctor" for a constructor). Null for 'T'.</param>
    /// <param name="elementText">Optional text defined in the reference.</param>
    /// <returns>The text to insert.</returns>
    public delegate string CodeRefHandler( char targetKind, string targetType, string? targetMember, string? elementText );

    /// <summary>
    /// The default <see cref="CodeRefHandler"/>: the text of the reference when it has one, otherwise the leaf of the
    /// type name, followed by ".Member" for a member (the type name alone for a constructor).
    /// </summary>
    public static readonly CodeRefHandler TextOnly = ( kind, type, member, text ) =>
    {
        if( !string.IsNullOrWhiteSpace( text ) ) return text;
        var leaf = type.Substring( type.LastIndexOf( '.' ) + 1 );
        return member == null || member == "#ctor" ? leaf : leaf + "." + member;
    };

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <param name="header">
    /// The text that starts the result (the first line is on a new line after it). When this builder is <see cref="IsEmpty"/>,
    /// the header is not considered.
    /// </param>
    /// <param name="linePrefix">The prefix of each line (after the header).</param>
    public DocumentationTextBuilder( string header = "", string linePrefix = "" )
    {
        Throw.CheckNotNullArgument( header );
        Throw.CheckNotNullArgument( linePrefix );
        _header = header;
        _linePrefix = linePrefix;
        _b = new StringBuilder( header );
        _waitingForNewline = header.Length > 0;
    }

    /// <summary>
    /// Gets or sets whether a code reference of an unsupported kind (not 'T', 'M', 'P', 'F' or 'E') throws a
    /// <see cref="CKException"/>. By default, its text (without its kind prefix) is kept.
    /// </summary>
    public bool ThrowOnUnsupportedCodeRef { get; set; }

    /// <summary>
    /// Gets whether at least one character has been added (the header doesn't count).
    /// </summary>
    public bool IsEmpty => _b.Length == _header.Length;

    /// <summary>
    /// Resets this builder so it can be reused.
    /// </summary>
    /// <returns>This builder.</returns>
    public DocumentationTextBuilder Reset()
    {
        _lastLineIsEmpty = false;
        _b.Clear().Append( _header );
        _waitingForNewline = _header.Length > 0;
        return this;
    }

    /// <summary>
    /// Gets the text built so far: the header and the lines. The empty string when <see cref="IsEmpty"/>.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => IsEmpty ? string.Empty : _b.ToString();

    /// <summary>
    /// Temporarily removes any "Gets or sets " prefix of the text fragments (the next letter is upper cased): "Gets or
    /// sets the name." becomes "The name.". This applies to <see cref="Append(string?, bool, bool)"/> that is called by
    /// all the other methods.
    /// </summary>
    /// <returns>A disposable that ends the removal.</returns>
    public IDisposable RemoveGetOrSetPrefix()
    {
        ++_removeGetOrSetPrefix;
        return Util.CreateDisposableAction( () => --_removeGetOrSetPrefix );
    }

    /// <summary>
    /// Appends the &lt;summary&gt; and &lt;value&gt; elements of any number of C# Xml documentations, then their
    /// &lt;remarks&gt; after an empty line. Identical blocks are appended once. Each block is processed by
    /// <see cref="AppendLinesFromXElement"/>.
    /// </summary>
    /// <param name="xDoc">The Xml documentation elements.</param>
    /// <param name="codeRef">The code reference handler. Defaults to <see cref="TextOnly"/>.</param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder AppendSummaryAndRemarks( IEnumerable<XElement> xDoc, CodeRefHandler? codeRef = null )
    {
        Throw.CheckNotNullArgument( xDoc );
        foreach( var x in DistinctByValue( xDoc.Elements().Where( e => e.Name.LocalName == "summary" || e.Name.LocalName == "value" ) ) )
        {
            AppendLinesFromXElement( x, codeRef, true, true );
        }
        foreach( var rem in DistinctByValue( xDoc.Elements( "remarks" ) ) )
        {
            AppendEmptyLine();
            AppendLinesFromXElement( rem, codeRef, true, true );
        }
        return this;
    }

    /// <summary>
    /// Filters the elements whose values are the same (ignoring the case and the surrounding white spaces).
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <returns>The first element of each value.</returns>
    public static IEnumerable<XElement> DistinctByValue( IEnumerable<XElement> elements )
    {
        var already = new HashSet<ReadOnlyMemory<char>>( TrimAndIgnoreCase.Instance );
        foreach( var e in elements )
        {
            if( already.Add( e.Value.AsMemory() ) )
            {
                yield return e;
            }
        }
    }

    sealed class TrimAndIgnoreCase : EqualityComparer<ReadOnlyMemory<char>>
    {
        public static readonly TrimAndIgnoreCase Instance = new TrimAndIgnoreCase();

        public override bool Equals( ReadOnlyMemory<char> x, ReadOnlyMemory<char> y )
        {
            return x.Span.Trim().Equals( y.Span.Trim(), StringComparison.OrdinalIgnoreCase );
        }

        public override int GetHashCode( [DisallowNull] ReadOnlyMemory<char> obj )
        {
            return string.GetHashCode( obj.Span, StringComparison.OrdinalIgnoreCase );
        }
    }

    /// <summary>
    /// Appends lines from an Xml element: &lt;c&gt;, &lt;code&gt; are transformed to ` and ``` markdown markers,
    /// &lt;para&gt; introduces a blank line, code references are processed by <paramref name="codeRef"/> and the name
    /// attribute of &lt;paramref&gt;/&lt;typeparamref&gt; is extracted. Any other elements are handled as "transparent"
    /// elements. Consecutive empty lines are collapsed into a single empty line.
    /// </summary>
    /// <param name="e">The documentation element. Ignored when null.</param>
    /// <param name="codeRef">The code reference handler. Defaults to <see cref="TextOnly"/>.</param>
    /// <param name="trimFirstLine">False to keep all leading white spaces.</param>
    /// <param name="startNewLine">False to append the first line to the end of the current last line.</param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder AppendLinesFromXElement( XElement? e, CodeRefHandler? codeRef = null, bool trimFirstLine = true, bool startNewLine = true )
    {
        if( e != null )
        {
            codeRef ??= TextOnly;
            foreach( var n in e.Nodes() )
            {
                // Comments, document type or processing instructions are ignored.
                if( n is XElement c )
                {
                    switch( c.Name.LocalName )
                    {
                        case "code":
                            Append( "```", true );
                            AppendText( c.Value, false, true, true, true );
                            AppendLine( "```", true );
                            trimFirstLine = startNewLine = true;
                            break;
                        case "c":
                            Append( "`", false );
                            AppendText( c.Value, false, true, false, false );
                            Append( "`", false );
                            trimFirstLine = startNewLine = false;
                            break;
                        case "para":
                            AppendEmptyLine();
                            AppendLinesFromXElement( c, codeRef, true, true );
                            trimFirstLine = startNewLine = true;
                            break;
                        case "seealso":
                        case "see":
                            AppendCodeRef( e, c, codeRef );
                            trimFirstLine = startNewLine = false;
                            break;
                        case "paramref":
                        case "typeparamref":
                            Append( (string)c.AttributeRequired( "name" ) );
                            trimFirstLine = startNewLine = false;
                            break;
                        default:
                            // Any other elements are "transparent": their lines are lifted.
                            AppendLinesFromXElement( c, codeRef, true, true );
                            trimFirstLine = startNewLine = true;
                            break;
                    }
                }
                else if( n is XText t )
                {
                    // The line structure of the source is kept between the nodes (words of two lines are never glued): a
                    // text that starts with a line break after another node starts a new line, a text that ends with one
                    // before another node ends its line. The line breaks after the opening tag and before the closing
                    // one are the layout of the Xml.
                    var text = t.Value;
                    bool newLine = startNewLine || (HasContentNode( n, before: true ) && StartsWithLineBreak( text ));
                    bool endLine = HasContentNode( n, before: false ) && EndsWithLineBreak( text );
                    AppendText( text, trimFirstLine, true, newLine, endLine );
                }
            }
        }
        return this;

        static bool HasContentNode( XNode n, bool before )
        {
            for( var x = before ? n.PreviousNode : n.NextNode; x != null; x = before ? x.PreviousNode : x.NextNode )
            {
                if( x is XElement || x is XText ) return true;
            }
            return false;
        }

        static bool StartsWithLineBreak( string text )
        {
            var head = text.AsSpan().TrimStart( " \t" );
            return head.Length > 0 && (head[0] == '\r' || head[0] == '\n');
        }

        static bool EndsWithLineBreak( string text )
        {
            var tail = text.AsSpan().TrimEnd( " \t" );
            return tail.Length > 0 && (tail[^1] == '\r' || tail[^1] == '\n');
        }
    }

    void AppendCodeRef( XElement e, XElement c, CodeRefHandler codeRef )
    {
        var cref = c.Attribute( "cref" )?.Value;
        if( cref == null )
        {
            // A <see langword="null"/> or a <see href="..."/>.
            var text = c.Attribute( "langword" )?.Value ?? (string.IsNullOrWhiteSpace( c.Value ) ? c.Attribute( "href" )?.Value : c.Value);
            if( text != null ) Append( c.Attribute( "langword" ) != null ? $"`{text}`" : text );
            return;
        }
        if( cref[0] == '!' )
        {
            Append( $"~~{cref}~~" );
            return;
        }
        char kind = cref.Length > 1 && cref[1] == ':' ? cref[0] : '\0';
        if( kind != 'T' && kind != 'P' && kind != 'F' && kind != 'M' && kind != 'E' )
        {
            if( ThrowOnUnsupportedCodeRef ) Throw.CKException( $"Unsupported cref '{cref}' in element '{e}'." );
            Append( kind == '\0' ? cref : cref.Substring( 2 ) );
            return;
        }
        string tName;
        string? mName;
        if( kind == 'T' )
        {
            tName = cref.Substring( 2 );
            mName = null;
        }
        else
        {
            int iPar = cref.IndexOf( '(', StringComparison.Ordinal );
            if( iPar < 0 ) iPar = cref.Length;
            int idx = cref.LastIndexOf( '.', iPar - 1 );
            tName = cref.Substring( 2, idx - 2 );
            ++idx;
            mName = cref.Substring( idx, iPar - idx );
        }
        Append( codeRef( kind, tName, mName, c.Value ) );
    }

    /// <summary>
    /// Appends multiple lines at once, removing their common white spaces prefix.
    /// See <see cref="AppendText(string, bool, bool, bool, bool)"/> to append a block of text.
    /// </summary>
    /// <param name="lines">Documentation lines to add.</param>
    /// <param name="startNewLine">
    /// True to start a new line.
    /// False to append the first line to the end of the current last line.
    /// </param>
    /// <param name="endWithNewline">True to end the current line after the fragment.</param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder Append( IEnumerable<string> lines, bool startNewLine, bool endWithNewline )
    {
        Throw.CheckNotNullArgument( lines );
        foreach( var l in lines )
        {
            Append( l, startNewLine, false );
            startNewLine = true;
        }
        _waitingForNewline |= endWithNewline;
        return this;
    }

    /// <summary>
    /// Appends a multi lines text, removing common white spaces prefix from each line.
    /// </summary>
    /// <param name="text">Text to add.</param>
    /// <param name="trimFirstLine">True to remove all leading white spaces.</param>
    /// <param name="trimLastLines">True to ignore any empty last lines.</param>
    /// <param name="startNewLine">
    /// True to start a new line.
    /// False to append the first line of the text to the end of the current last line.
    /// </param>
    /// <param name="endWithNewline">True to end the current line after the fragment.</param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder AppendText( string text, bool trimFirstLine, bool trimLastLines, bool startNewLine, bool endWithNewline )
    {
        if( text != null )
        {
            var l = text.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries );
            if( l.Length > 0 )
            {
                if( startNewLine )
                {
                    int p = ComputeSpacePrefix( l, false );
                    AppendLines( l, false, p, trimFirstLine );
                }
                else
                {
                    // In "inline" mode (when startNewLine is false), we normalize
                    // whitespaces to be one.
                    // If trimFirstLine is specified, we remove all the white spaces.
                    var firstLine = l[0];
                    int p = ComputeSpacePrefixLine( firstLine );
                    if( p >= 0 )
                    {
                        if( p > 0 && !trimFirstLine ) --p;
                        // The next lines (if any) start their own line: a single line fragment doesn't end its line.
                        Append( firstLine.Substring( p ), false, false );
                    }
                    if( l.Length > 1 )
                    {
                        AppendLines( l, true, ComputeSpacePrefix( l, true ), false );
                    }
                }
                if( trimLastLines ) _lastLineIsEmpty = false;
                _waitingForNewline |= endWithNewline;
            }
        }
        return this;

        static int ComputeSpacePrefix( string[] lines, bool skipFirstLine )
        {
            Debug.Assert( !skipFirstLine || lines.Length > 1 );
            int spacePrefix = 0;
            for(; ; )
            {
                int hasContent = lines.Length;
                bool skip = skipFirstLine;
                if( skip ) --hasContent;
                foreach( var line in lines )
                {
                    if( skip )
                    {
                        skip = false;
                        continue;
                    }
                    if( spacePrefix >= line.Length ) --hasContent;
                    else if( !char.IsWhiteSpace( line, spacePrefix ) ) return spacePrefix;
                }
                if( hasContent == 0 ) return -1;
                ++spacePrefix;
            }
        }

        static int ComputeSpacePrefixLine( string line )
        {
            for( int i = 0; i < line.Length; ++i )
            {
                if( !Char.IsWhiteSpace( line, i ) ) return i;
            }
            return -1;
        }

        void AppendLines( string[] lines, bool skipFirstLine, int spacePrefix, bool trimFirstLine )
        {
            if( spacePrefix >= 0 )
            {
                foreach( var line in lines )
                {
                    if( skipFirstLine )
                    {
                        skipFirstLine = false;
                        continue;
                    }
                    if( spacePrefix < line.Length )
                    {
                        var l = line.Substring( spacePrefix );
                        if( trimFirstLine ) l = l.TrimStart();

                        Append( l, true, false );
                    }
                    else
                    {
                        AppendEmptyLine();
                    }
                    trimFirstLine = false;
                }
            }
        }
    }

    /// <summary>
    /// Appends an empty line.
    /// Multiple empty lines are collapsed.
    /// </summary>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder AppendEmptyLine() => Append( String.Empty, true, true );

    /// <summary>
    /// Appends a text fragment that must not contain newlines and ends it with a new line.
    /// </summary>
    /// <param name="lineFragment">A line or some text without newlines inside.</param>
    /// <param name="startNewLine">
    /// True to start a new line.
    /// False to append the fragment to the end of the current last line if any.
    /// </param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder AppendLine( string? lineFragment, bool startNewLine ) => Append( lineFragment, startNewLine, true );

    /// <summary>
    /// Appends a text fragment that must not contain newlines.
    /// </summary>
    /// <param name="lineFragment">A line or some text without newlines inside.</param>
    /// <param name="startNewLine">
    /// True to start a new line.
    /// False to append the fragment to the end of the current last line if any.
    /// </param>
    /// <param name="endWithNewline">True to end the current line after the fragment.</param>
    /// <returns>This builder to enable fluent syntax.</returns>
    public DocumentationTextBuilder Append( string? lineFragment, bool startNewLine = false, bool endWithNewline = false )
    {
        if( String.IsNullOrEmpty( lineFragment ) )
        {
            // Adding an empty string is void except if
            // it implies newlines.
            if( startNewLine && !_lastLineIsEmpty )
            {
                _lastLineIsEmpty = true;
                _waitingForNewline = true;
            }
        }
        else
        {
            if( _waitingForNewline || startNewLine )
            {
                // The very first line of a builder without header has no newline before it.
                bool atStart = _b.Length == 0;
                if( _lastLineIsEmpty && !atStart )
                {
                    _b.Append( Environment.NewLine ).Append( _linePrefix );
                }
                if( !atStart ) _b.Append( Environment.NewLine );
                _b.Append( _linePrefix );
                _waitingForNewline = false;
            }
            if( _removeGetOrSetPrefix > 0 )
            {
                lineFragment = RemoveGetsOrSetsPrefix( lineFragment );
            }
            _b.Append( lineFragment );
            _lastLineIsEmpty = false;
        }
        _waitingForNewline |= endWithNewline;
        return this;
    }

    static string RemoveGetsOrSetsPrefix( string text )
    {
        var m = GetsOrSetsRegex().Match( text );
        if( m.Success && m.Length < text.Length )
        {
            char initial = text[m.Length];
            if( char.IsLower( initial ) )
            {
                initial = char.ToUpper( initial, CultureInfo.InvariantCulture );
                text = initial + text.Substring( m.Length + 1 );
            }
            else
            {
                text = text.Substring( m.Length );
            }
        }
        return text;
    }

    [GeneratedRegex( @"^\s*Gets?(\s+(or\s+)?sets?)?\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant )]
    private static partial Regex GetsOrSetsRegex();
}

using System;
using System.Collections.Generic;
using System.Text;
using CK.Core;
using System.Globalization;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using CK.CodeGen.SimpleParser;
using System.Diagnostics;

namespace CK.CodeGen;

static class StringMatcherExtensions
{
    internal static bool TryMatchCSharpIdentifier( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out string? identifier, bool skipAtSign = false )
    {
        identifier = null;
        if( head.IsEmpty ) return false;
        var savedHead = head;
        bool at = head.TryMatch( '@' );
        if( IsValidIdentifierStart( head[0] ) )
        {
            int len = head.Length;
            int i = 1;
            while( --len > 0 && IsValidIdentifierChar( head[i] ) ) ++i;
            identifier = new string( at && !skipAtSign ? savedHead.Slice( 0, i + 1 ) : head.Slice( 0, i ) );
            head = head.Slice( i );
            return true;
        }
        head = savedHead;
        return false;
    }

    static bool EatRawCode( this ref ReadOnlySpan<char> head, StringBuilder b, bool stopOnComma, bool removeWhiteSpaces = true )
    {
        int bPos = b.Length;
        int depth = 0;
        while( !head.IsEmpty
                && (depth != 0 || (head[0] != ')' && head[0] != ']' && head[0] != '}' && (!stopOnComma || head[0] != ','))) )
        {
            if( head[0] == '(' || head[0] == '[' || head[0] == '{' )
            {
                ++depth;
                b.Append( head[0] );
                head = head.Slice( 1 );
            }
            else if( head[0] == ')' || head[0] == ']' || head[0] == '}' )
            {
                --depth;
                b.Append( head[0] );
                head = head.Slice( 1 );
            }
            else if( head.TryMatchCSharpIdentifier( out var id, skipAtSign: false ) )
            {
                b.Append( id );
            }
            else if( head.TryMatchCSharpString( out var str ) )
            {
                b.Append( str );
            }
            else
            {
                if( !(removeWhiteSpaces && Char.IsWhiteSpace( head[0] )) ) b.Append( head[0] );
                head = head.Slice( 1 );
            }
        }
        return b.Length > bPos;
    }

    static bool TryMatchCSharpString( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out string? s )
    {
        if( head.StartsWith( "$@\"" ) || head.StartsWith( "@$\"" ) )
        {
            return head.EatVerbatimString( 3, out s );
        }
        if( head.StartsWith( "@\"" ) )
        {
            return head.EatVerbatimString( 2, out s );
        }
        if( head[0] == '"' )
        {
            return head.EatString( out s, '"' );
        }
        if( head[0] == '\'' )
        {
            return head.EatString( out s, '\'' );
        }
        s = null;
        return false;
    }

    static bool EatString( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out string? s, char mark )
    {
        var start = head;
        head = head.Slice( 1 );
        while( !head.IsEmpty )
        {
            if( head[0] == mark )
            {
                head = head.Slice( 1 );
                s = new string( start.Slice( 0, start.Length - head.Length ) );
                return true;
            }
            head = head.Slice( head[0] == '\\' ? 2 : 1 );
        }
        s = null;
        return false;
    }

    static bool EatVerbatimString( this ref ReadOnlySpan<char> head, int declLength, [NotNullWhen( true )] out string? s )
    {
        var start = head;
        head = head.Slice( declLength );
        while( !head.IsEmpty )
        {
            if( head[0] == '"' )
            {
                head = head.Slice( 1 );
                if( head.IsEmpty ) break;
                if( head[0] == '"' )
                {
                    head = head.Slice( 1 );
                    continue;
                }
                s = new string( start.Slice( 0, start.Length - head.Length ) );
                return true;
            }
            head = head.Slice( 1 );
        }
        s = null;
        return false;
    }

    internal static bool MatchPotentialAttributes( this ref ReadOnlySpan<char> head, out AttributeCollection? attributes )
    {
        attributes = null;
        while( head.TryMatchAttribute( out var a ) )
        {
            if( attributes == null ) attributes = new AttributeCollection();
            attributes.Ensure( a );
            head.SkipWhiteSpacesAndJSComments();
        }
        return !head.IsEmpty;
    }

    static CodeAttributeTarget MapAttributeTarget( string s )
    {
        return s switch
        {
            "assembly" => CodeAttributeTarget.Assembly,
            "module" => CodeAttributeTarget.Module,
            "field" => CodeAttributeTarget.Field,
            "event" => CodeAttributeTarget.Event,
            "method" => CodeAttributeTarget.Method,
            "param" => CodeAttributeTarget.Param,
            "property" => CodeAttributeTarget.Property,
            "return" => CodeAttributeTarget.Return,
            "type" => CodeAttributeTarget.Type,
            _ => CodeAttributeTarget.None,
        };
    }

    internal static bool TryMatchAttribute( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out AttributeSetDefinition? attr )
    {
        attr = null;
        if( !head.TryMatch( '[' ) ) return false;
        head.SkipWhiteSpacesAndJSComments();
        if( !head.TryMatchCSharpIdentifier( out string? targetOrName ) ) return false; // head.AddError( "Attribute definition expected." );
        var target = MapAttributeTarget( targetOrName );
        if( target != CodeAttributeTarget.None )
        {
            if( !head.TryMatch( ':' ) ) return false;
            targetOrName = null;
        }
        List<AttributeDefinition> attributes = new List<AttributeDefinition>();
        do
        {
            head.SkipWhiteSpacesAndJSComments();
            if( !head.MatchTypeName( out TypeName? name, targetOrName ) ) return false; // head.AddError( "Attribute definition expected." );
            targetOrName = null;
            if( name.Name.EndsWith( "Attribute", StringComparison.Ordinal ) )
            {
                name = new TypeName( name.Name.Remove( name.Name.Length - 9 ), name.GenericParameters );
            }
            var bAttrValue = new StringBuilder();
            List<string> values = new List<string>();
            head.SkipWhiteSpacesAndJSComments();
            if( head.TryMatch( '(' ) )
            {
                head.SkipWhiteSpacesAndJSComments();
                while( !head.TryMatch( ')' ) )
                {
                    if( !head.EatRawCode( bAttrValue, true, true ) ) return false; // head.SetError( "Values expected." );
                    values.Add( bAttrValue.ToString() );
                    bAttrValue.Clear();
                    // Allow training comma. Don't care.
                    if( head.TryMatch( ',' ) ) head.SkipWhiteSpacesAndJSComments();
                }
            }
            attributes.Add( new AttributeDefinition( name, values ) );
            head.SkipWhiteSpacesAndJSComments();
        }
        while( head.TryMatch( ',' ) );
        head.SkipWhiteSpacesAndJSComments();
        if( !head.TryMatch( ']' ) ) return false;
        attr = new AttributeSetDefinition( target, attributes );
        return true;
    }

    internal static bool MatchMethodDefinition( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out FunctionDefinition? mDef, out bool hasCodeOpener )
    {
        mDef = null;
        hasCodeOpener = false;

        if( !head.MatchPotentialAttributes( out var attributes ) ) return false;

        var startName = CollectModifiersUntilIdentifier( ref head, out var modifiers );
        modifiers = modifiers.NormalizeMemberProtection();

        head.SkipWhiteSpacesAndJSComments();
        if( !head.MatchExtendedTypeName( out var returnType, startName ) ) return false;

        head.SkipWhiteSpacesAndJSComments();
        bool isIndexer = false;
        TypeName? methodName;
        if( head.TryMatch( '(' ) )
        {
            if( returnType.IsTuple ) return false; // head.SetError( $"Invalid syntax: unexpected tuple {returnType}." );
            methodName = returnType.TypeName;
            returnType = null;
        }
        else
        {
            if( !head.MatchTypeName( out methodName ) ) return false;
            head.SkipWhiteSpacesAndJSComments();
            if( !head.TryMatch( '(' ) && !(isIndexer = head.TryMatch( '[' )) ) return false; // head.SetError( "Expected '[' or '('." ); 
        }
        Throw.DebugAssert( methodName != null );
        var buffer = new StringBuilder();
        head.SkipWhiteSpacesAndJSComments();
        List<FunctionDefinition.Parameter> parameters = new List<FunctionDefinition.Parameter>();
        while( !head.TryMatch( isIndexer ? ']' : ')' ) )
        {
            do
            {
                head.SkipWhiteSpacesAndJSComments();
                if( !head.MatchPotentialAttributes( out var pAttr ) ) return false;

                FunctionDefinition.ParameterModifier mod = FunctionDefinition.ParameterModifier.None;
                if( head.TryMatchCSharpIdentifier( out var pTypeStart ) )
                {
                    switch( pTypeStart )
                    {
                        case "this": mod = FunctionDefinition.ParameterModifier.This; pTypeStart = null; break;
                        case "params": mod = FunctionDefinition.ParameterModifier.Params; pTypeStart = null; break;
                        case "scoped":
                            mod = FunctionDefinition.ParameterModifier.Scoped;
                            head.SkipWhiteSpacesAndJSComments();
                            // scoped must be followed by ref, but we simply allow it here.
                            if( head.TryMatch( "ref" ) ) mod |= FunctionDefinition.ParameterModifier.Ref;
                            pTypeStart = null;
                            break;
                        case "ref": mod = FunctionDefinition.ParameterModifier.Ref; pTypeStart = null; break;
                        case "out": mod = FunctionDefinition.ParameterModifier.Out; pTypeStart = null; break;
                        case "in": mod = FunctionDefinition.ParameterModifier.In; pTypeStart = null; break;
                    }
                }
                head.SkipWhiteSpacesAndJSComments();
                if( !head.MatchExtendedTypeName( out var pType, pTypeStart ) ) return false;
                head.SkipWhiteSpacesAndJSComments();
                if( !head.TryMatchCSharpIdentifier( out var pName ) ) return false;
                head.SkipWhiteSpacesAndJSComments();
                string? defVal = null;
                if( head.TryMatch( '=' ) )
                {
                    if( !head.EatRawCode( buffer, true, true ) ) return false; // head.SetError( "Unable to read default value." );
                    defVal = buffer.ToString();
                    buffer.Clear();
                }
                else head.SkipWhiteSpacesAndJSComments();
                parameters.Add( new FunctionDefinition.Parameter( pAttr, mod, pType, pName, defVal ) );
            }
            while( head.TryMatch( ',' ) );
        }
        var thisOrBaseCall = FunctionDefinition.CallConstructor.None;
        string? thisOrBaseCallParameter = null;
        head.SkipWhiteSpacesAndJSComments();
        if( returnType == null )
        {
            if( head.TryMatch( ':' ) )
            {
                head.SkipWhiteSpacesAndJSComments();
                if( head.TryMatch( "this", StringComparison.Ordinal ) ) thisOrBaseCall = FunctionDefinition.CallConstructor.This;
                else if( head.TryMatch( "base", StringComparison.Ordinal ) ) thisOrBaseCall = FunctionDefinition.CallConstructor.Base;
                if( thisOrBaseCall != FunctionDefinition.CallConstructor.None )
                {
                    head.SkipWhiteSpacesAndJSComments();
                    if( !head.TryMatch( '(' ) ) return false; // head.SetError( "this(...) or base(...) : missing '('." );
                    head.EatRawCode( buffer, false, false );
                    thisOrBaseCallParameter = buffer.ToString();
                    head.SkipWhiteSpacesAndJSComments();
                    if( !head.TryMatch( ')' ) ) return false; // head.SetError( "this(...) or base(...) : missing ')'." );
                    head.SkipWhiteSpacesAndJSComments();
                }
                else return false; // head.SetError( "this(...) or base(...) expected." );
            }
        }
        buffer.Clear();
        List<TypeParameterConstraint>? wheres;
        if( !head.MatchWhereConstraints( out hasCodeOpener, out wheres ) ) return false;
        mDef = new FunctionDefinition( attributes, modifiers, returnType, methodName, thisOrBaseCall, thisOrBaseCallParameter, isIndexer, parameters, wheres, buffer );
        return true;
    }

    #region TypeDefinition

    internal static string? CollectModifiersUntilIdentifier( this ref ReadOnlySpan<char> head, out Modifiers modifiers )
    {
        modifiers = Modifiers.None;
        string? id;
        while( head.TryMatchCSharpIdentifier( out id )
               && ModifiersExtension.ParseAndCombine( ref modifiers, id ) )
        {
            head.SkipWhiteSpacesAndJSComments();
        }
        return id;
    }

    internal static bool MatchTypeKey( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out string? key )
    {
        key = null;
        if( !head.MatchPotentialAttributes( out var attributes ) ) return false;
        string? header = head.CollectModifiersUntilIdentifier( out var modifiers );
        if( header == "class" || header == "struct" || header == "interface" || header == "enum" )
        {
            header = null;
        }
        head.SkipWhiteSpacesAndJSComments();
        if( !head.MatchTypeName( out var name, header ) ) return false;
        key = name.Key;
        return true;
    }

    internal static bool MatchTypeDefinition( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out TypeDefinition? typeDef, bool isNestedType, out bool hasCodeOpener )
    {
        typeDef = null;
        hasCodeOpener = false;

        if( !head.MatchPotentialAttributes( out var attributes ) ) return false;

        TypeDefinition.TypeKind kind;
        switch( CollectModifiersUntilIdentifier( ref head, out var modifiers ) )
        {
            case "class": kind = TypeDefinition.TypeKind.Class; break;
            case "struct": kind = TypeDefinition.TypeKind.Struct; break;
            case "interface": kind = TypeDefinition.TypeKind.Interface; break;
            case "enum": kind = TypeDefinition.TypeKind.Enum; break;
            default: return false; // head.SetError( "Expected: class, struct, interface or enum." );
        }
        modifiers = modifiers.NormalizeForType();
        if( isNestedType ) modifiers = modifiers.NormalizeMemberProtection();
        else modifiers = modifiers.NormalizeNamespaceProtection();

        head.SkipWhiteSpacesAndJSComments();
        if( !head.MatchTypeName( out var name ) ) return false;
        List<ExtendedTypeName>? baseTypes = null;
        head.SkipWhiteSpacesAndJSComments();
        if( head.TryMatch( ':' ) )
        {
            head.SkipWhiteSpacesAndJSComments();
            if( !head.MatchBaseTypesOrConstraints( out baseTypes ) ) return false;
        }
        head.SkipWhiteSpacesAndJSComments();
        if( !head.MatchWhereConstraints( out hasCodeOpener, out List<TypeParameterConstraint>? wheres ) ) return false;
        typeDef = new TypeDefinition( attributes, modifiers, kind, name, baseTypes, wheres );
        return true;
    }

    static bool MatchWhereConstraints( this ref ReadOnlySpan<char> head, out bool hasCodeOpener, out List<TypeParameterConstraint>? wheres )
    {
        hasCodeOpener = false;
        wheres = null;
        while( !head.IsEmpty && !(hasCodeOpener = (head[0] == '{' || head[0] == '=')) )
        {
            head.SkipWhiteSpacesAndJSComments();
            if( !head.MatchTypeParameterConstraint( out var c ) ) return false;
            if( wheres == null ) wheres = new List<TypeParameterConstraint>();
            else if( wheres.Any( x => x.ParameterName == c.ParameterName ) ) return false; // head.SetError( $"Duplicate where constraint: where {c.ParameterName}." );
            wheres.Add( c );
            head.SkipWhiteSpacesAndJSComments();
        }
        if( hasCodeOpener )
        {
            // If we stopped on '{', forwards the head.
            head.TryMatch( '{' );
            hasCodeOpener = !head.IsEmpty;
        }
        return true;
    }

    /// <summary>
    /// BaseTypeOrConstraint => TypeName | new()
    /// The "new()" is becomes the <see cref="TypeName.Name"/> of a pseudo type name.
    /// </summary>
    static bool MatchBaseTypeOrConstraint( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out ExtendedTypeName? t )
    {
        t = null;
        if( !head.TryMatchCSharpIdentifier( out var baseName ) ) return false; // @this.SetError( "Expected identifier." );
        if( baseName == "new" )
        {
            head.SkipWhiteSpacesAndJSComments();
            if( head.TryMatch( '(' ) )
            {
                head.SkipWhiteSpacesAndJSComments();
                if( head.TryMatch( ')' ) )
                {
                    t = new ExtendedTypeName( new TypeName( "new()", null ) );
                    return true;
                }
            }
            return false; // @this.SetError( "Invalid new() constraint." );
        }
        head.SkipWhiteSpacesAndJSComments();
        return head.MatchExtendedTypeName( out t, baseName );
    }

    /// <summary>
    /// BaseTypesOrConstraints => comma separated MatchBaseTypeOrConstraint.
    /// </summary>
    static bool MatchBaseTypesOrConstraints( this ref ReadOnlySpan<char> head, out List<ExtendedTypeName> types )
    {
        types = new List<ExtendedTypeName>();
        do
        {
            head.SkipWhiteSpacesAndJSComments();
            if( !head.MatchBaseTypeOrConstraint( out var t ) ) return false; // head.AddError( "Expected base type.", true );
            types.Add( t );
            head.SkipWhiteSpacesAndJSComments();
        }
        while( head.TryMatch( ',' ) );
        return true;
    }

    /// <summary>
    /// TypeParameterConstraint => where : BaseTypesOrConstraints
    /// </summary>
    static bool MatchTypeParameterConstraint( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out TypeParameterConstraint? c )
    {
        c = null;
        if( !head.TryMatchCSharpIdentifier( out var name ) || name != "where" ) return false; // head.SetError( "Expected where constraint." );
        head.SkipWhiteSpacesAndJSComments();
        if( !head.TryMatchCSharpIdentifier( out name ) ) return false;
        head.SkipWhiteSpacesAndJSComments();
        if( !head.TryMatch( ':' ) ) return false;
        head.SkipWhiteSpacesAndJSComments();
        if( !head.MatchBaseTypesOrConstraints( out var baseTypes ) ) return false;
        c = new TypeParameterConstraint( name, baseTypes );
        return true;
    }

    #endregion

    #region TypeName

    /// <summary>
    /// Relaxed syntax here: we allow empty or single-field tuples (this is not valid)
    /// and an ending comma in the list.
    /// </summary>
    /// <param name="head">This matcher.</param>
    /// <param name="type">The tuple type name on success.</param>
    /// <returns>True on success, false on error.</returns>
    internal static bool TryMatchTupleTypeName( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out TupleTypeName? type )
    {
        type = null;
        if( !head.TryMatch( '(' ) ) return false;
        List<TupleTypeName.Field> fields = new List<TupleTypeName.Field>();
        while( !head.TryMatch( ')' ) )
        {
            ExtendedTypeName? fType = null;
            string? fName = null;
            if( !head.MatchExtendedTypeName( out fType ) ) return false;
            head.SkipWhiteSpacesAndJSComments();
            if( head.TryMatchCSharpIdentifier( out fName ) ) head.SkipWhiteSpacesAndJSComments();
            fields.Add( new TupleTypeName.Field( fType, fName ) );
            if( head.TryMatch( ',' ) ) head.SkipWhiteSpacesAndJSComments();
        }
        head.SkipWhiteSpacesAndJSComments();
        type = new TupleTypeName( fields );
        return true;
    }

    internal static bool MatchExtendedTypeName( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out ExtendedTypeName? type, string? knownName = null )
    {
        type = null;
        TupleTypeName? tuple = null;
        TypeName? regularType = null;
        if( knownName != null || !head.TryMatchTupleTypeName( out tuple ) )
        {
            if( head.MatchTypeName( out regularType, knownName ) )
            {
                if( regularType.GenericParameters.Count == 1 && (regularType.Name == "Nullable" || regularType.Name == "System.Nullable") )
                {
                    type = regularType.GenericParameters[0].Type.WithNullable( true );
                }
            }
        }
        if( type == null )
        {
            // Nullable<Nullable<...>> cannot exist.
            bool isNullable = head.TryMatch( '?' );
            if( isNullable ) head.SkipWhiteSpacesAndJSComments();
            if( tuple != null ) type = new ExtendedTypeName( tuple, isNullable );
            else if( regularType != null ) type = new ExtendedTypeName( regularType, isNullable );
            else return false;
        }
        List<int>? arrayDim = null;
        while( head.TryMatch( '[' ) )
        {
            if( arrayDim == null ) arrayDim = new List<int>();
            int dim = 0;
            while( head.TryMatch( ',' ) )
            {
                ++dim;
                head.SkipWhiteSpacesAndJSComments();
            }
            if( !head.TryMatch( ']' ) ) return false; // head.SetError( "Closing ']' array." );
            arrayDim.Add( dim );
            head.SkipWhiteSpacesAndJSComments();
        }
        if( arrayDim != null )
        {
            bool isNullable = head.TryMatch( '?' );
            if( isNullable ) head.SkipWhiteSpacesAndJSComments();
            type = new ExtendedTypeName( type, arrayDim, isNullable );
        }
        return true;
    }

    internal static bool MatchTypeName( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out TypeName? type, string? knownName = null )
    {
        type = null;
        if( knownName != null
            || head.TryMatchCSharpIdentifier( out knownName ) )
        {
            if( knownName == "global" )
            {
                if( !head.TryMatch( "::" ) ) return false;
                if( !head.TryMatchCSharpIdentifier( out var sub ) ) return false;
                knownName += "::" + sub;
            }
            List<TypeName.GenParam>? genArgs = null;
            head.SkipWhiteSpacesAndJSComments();
            while( head.TryMatch( '.' ) )
            {
                head.SkipWhiteSpacesAndJSComments();
                if( !head.TryMatchCSharpIdentifier( out var sub ) ) return false;
                knownName += '.' + sub;
                head.SkipWhiteSpacesAndJSComments();
            }
            if( head.TryMatch( '<' ) )
            {
                genArgs = new List<TypeName.GenParam>();
                for(; ; )
                {
                    head.SkipWhiteSpacesAndJSComments();
                    if( head.TryMatch( ',' ) )
                    {
                        genArgs.Add( TypeName.GenParam.Empty );
                        continue;
                    }
                    if( head.TryMatch( '>' ) )
                    {
                        // Handles open generic definition like "G<>" or "G<,>".
                        genArgs.Add( TypeName.GenParam.Empty );
                        head.SkipWhiteSpacesAndJSComments();
                        break;
                    }
                    if( !MatchGenParam( ref head, out var genArg ) ) return false; // head.AddError( "Expected generic type parameter." );
                    genArgs.Add( genArg.Value );
                    head.SkipWhiteSpacesAndJSComments();
                    if( head.TryMatch( '>' ) )
                    {
                        head.SkipWhiteSpacesAndJSComments();
                        break;
                    }
                    if( head.TryMatch( ',' ) ) continue;
                }
            }
            type = new TypeName( knownName, genArgs );
            return true;
        }
        return false; // head.SetError( "Type name." );
    }

    static bool MatchGenParam( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out TypeName.GenParam? genArg )
    {
        genArg = null;
        var v = TypeName.GenParam.Variance.None;
        if( head.TryMatchCSharpIdentifier( out string? nameOrVariance ) )
        {
            if( nameOrVariance == "out" )
            {
                v = TypeName.GenParam.Variance.Out;
                nameOrVariance = null;
                head.SkipWhiteSpacesAndJSComments();
            }
            else if( nameOrVariance == "in" )
            {
                v = TypeName.GenParam.Variance.In;
                nameOrVariance = null;
                head.SkipWhiteSpacesAndJSComments();
            }
        }
        if( !head.MatchExtendedTypeName( out var gT, nameOrVariance ) ) return false;
        genArg = new TypeName.GenParam( v, gT );
        return true;
    }

    // This is adapted from: https://stackoverflow.com/questions/1829679/how-to-determine-if-a-string-is-a-valid-variable-name
    // This has been (heavily) simplified: forgetting about surrogate pairs and UnicodeCategory.Format stuff.
    static bool IsValidIdentifierStart( char c )
    {
        return c == '_'
                || char.IsLetter( c )
                || char.GetUnicodeCategory( c ) == UnicodeCategory.LetterNumber;
    }

    static bool IsValidIdentifierChar( char c )
    {
        if( c == '_' || (c >= '0' && c <= '9') || char.IsLetter( c ) ) return true;

        switch( char.GetUnicodeCategory( c ) )
        {
            case UnicodeCategory.LetterNumber:
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.ConnectorPunctuation:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
                return true;
            default:
                return false;
        }
    }

}
#endregion

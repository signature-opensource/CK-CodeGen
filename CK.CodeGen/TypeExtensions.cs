using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CK.CodeGen;

/// <summary>
/// Provides <see cref="ToGlobalTypeName(Type?, bool)"/> extension method.
/// </summary>
/// <remarks>
/// A ConcurrentDictionary caches the computed strings.
/// </remarks>
public static class TypeExtensions
{

    readonly struct KeyType : IEquatable<KeyType>
    {
        public readonly Type Type;
        public readonly bool Declaration;

        public bool Equals( KeyType other ) => Type == other.Type && Declaration == other.Declaration;

        public override int GetHashCode() => Declaration ? -Type.GetHashCode() : Type.GetHashCode();

        public KeyType( Type t, bool d )
        {
            Type = t;
            Declaration = d;
        }

        public override bool Equals( object? obj ) => obj is KeyType k && Equals( k );
    }

    static readonly ConcurrentDictionary<KeyType, string> _names = new ConcurrentDictionary<KeyType, string>();

    /// <summary>
    /// Gets the exact C# type full name prefixed by <c>global::</c>.
    /// Handles generic definition (either opened or closed).
    /// <para>This can be called on a null reference: "null" is returned.</para>
    /// <para>
    /// The <paramref name="typeDeclaration"/> parameter applies to open generics:
    /// When true (the default), typeof( Dictionary&lt;,&gt;.KeyCollection )
    /// will result in "System.Collections.Generic.Dictionary&lt;TKey,TValue&gt;.KeyCollection".
    /// When sets to false, it will be "System.Collections.Generic.Dictionary&lt;,&gt;.KeyCollection".
    /// </para>
    /// </summary>
    /// <remarks>
    /// Value tuples are expressed with the (lovely,brackets).
    /// </remarks>
    /// <param name="this">This type.</param>
    /// <param name="typeDeclaration">False to not include generic parameter names in the output (typically for typeof syntax).</param>
    /// <returns>The C# full type name.</returns>
    public static string ToGlobalTypeName( this Type? @this, bool typeDeclaration = true )
    {
        return @this == null
                ? "null"
                : _names.GetOrAdd( new KeyType( @this, typeDeclaration ),
                                   k => Append( new StringBuilder(), k.Type, typeDeclaration ).ToString() );

        static StringBuilder Append( StringBuilder b, Type t, bool typeDeclaration )
        {
            if( t.IsGenericParameter ) return typeDeclaration ? b.Append( t.Name ) : b;
            if( CK.Core.TypeExtensions.TypeAliases.TryGetValue( t, out var alias ) )
            {
                b.Append( alias );
                return b;
            }
            if( t.IsArray )
            {
                return b.Append( ToGlobalTypeName( t.GetElementType()!, typeDeclaration ) )
                        .Append( '[' ).Append( ',', t.GetArrayRank() - 1 ).Append( ']' );
            }
            if( t.IsByRef )
            {
                return b.Append( ToGlobalTypeName( t.GetElementType()!, typeDeclaration ) ).Append( '&' );
            }
            if( t.IsPointer )
            {
                int stars = 0;
                while( t.IsPointer )
                {
                    stars++;
                    t = t.GetElementType()!;
                }
                return b.Append( ToGlobalTypeName( t, typeDeclaration ) )
                        .Append( new string( '*', stars ) );
            }
            var pathTypes = new Stack<Type>();
            pathTypes.Push( t );
            Type? decl = t.DeclaringType;
            while( decl != null )
            {
                pathTypes.Push( decl );
                decl = decl.DeclaringType;
            }
            var allGenArgs = new Queue<Type>( t.GetGenericArguments() );
            for( int iType = 0; pathTypes.Count > 0; iType++ )
            {
                Type theT = pathTypes.Pop();
                string n;
                if( iType == 0 ) n = "global::" + theT.FullName!;
                else
                {
                    n = theT.Name;
                    b.Append( '.' );
                }
                int idxTick = n.IndexOf( '`', StringComparison.Ordinal ) + 1;
                if( idxTick > 0 )
                {
                    int endNbParam = idxTick;
                    while( endNbParam < n.Length && Char.IsDigit( n, endNbParam ) ) endNbParam++;
                    int nbParams = int.Parse( n.AsSpan( idxTick, endNbParam - idxTick ), NumberStyles.Integer, NumberFormatInfo.InvariantInfo );
                    Throw.DebugAssert( nbParams > 0 );
                    var tName = n.Substring( 0, idxTick - 1 );
                    bool isValueTuple = tName == "global::System.ValueTuple";
                    Type subType = allGenArgs.Dequeue();
                    bool isNullableValue = !isValueTuple && tName == "global::System.Nullable" && !subType.IsGenericTypeParameter;
                    if( isValueTuple )
                    {
                        b.Append( '(' );
                    }
                    else if( !isNullableValue )
                    {
                        b.Append( tName );
                        b.Append( '<' );
                    }
                    --nbParams;
                    int iGen = 0;
                    for(; ; )
                    {
                        if( iGen > 0 ) b.Append( ',' );
                        b.Append( ToGlobalTypeName( subType, typeDeclaration ) );
                        if( iGen++ == nbParams ) break;
                        subType = allGenArgs.Dequeue();
                        // Long Value Tuple handling here only if useValueTupleParentheses is true.
                        // This lift the rest content, skipping the rest 8th slot itself.
                        if( iGen == 7 && isValueTuple )
                        {
                            Throw.DebugAssert( subType.Name.StartsWith( "ValueTuple", StringComparison.Ordinal ) );
                            Throw.DebugAssert( allGenArgs.Count == 0 );
                            var rest = subType.GetGenericArguments();
                            subType = rest[0];
                            nbParams = rest.Length - 1;
                            for( int i = 1; i < rest.Length; ++i ) allGenArgs.Enqueue( rest[i] );
                            iGen = 0;
                            b.Append( ',' );
                        }
                    }
                    b.Append( isNullableValue ? '?' : (isValueTuple ? ')' : '>') );
                }
                else b.Append( n );
            }
            return b;
        }
    }
}

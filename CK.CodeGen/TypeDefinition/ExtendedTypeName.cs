using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.CodeGen;

/// <summary>
/// Unifies <see cref="TypeName"/> and <see cref="TupleTypeName"/> (simple algebraic data type) and handles
/// array definition (jagged, and/or multi-dimensional array) of such types if <see cref="ArrayDimensions"/> is not empty.
/// </summary>
public class ExtendedTypeName
{
    /// <summary>
    /// Gets an empty type name instance.
    /// </summary>
    public static readonly ExtendedTypeName Empty = new ExtendedTypeName( TypeName.Empty );

    /// <summary>
    /// Initializes a new <see cref="ExtendedTypeName"/> for a  <see cref="TypeName"/>
    /// with a raw, non analyzed, type name.
    /// </summary>
    /// <param name="rawTypeName">The future raw <see cref="TypeName.Name"/>.</param>
    /// <param name="isNullable">True to specify an explicit nullable type.</param>
    public ExtendedTypeName( string rawTypeName, bool isNullable = false )
        : this( new TypeName( rawTypeName ), isNullable )
    {
    }

    /// <summary>
    /// Initializes a new <see cref="ExtendedTypeName"/> for a  <see cref="TupleTypeName"/>.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <param name="isNullable">True to specify an explicit nullable type.</param>
    public ExtendedTypeName( TupleTypeName tuple, bool isNullable = false )
    {
        TupleTypeName = tuple ?? throw new ArgumentNullException( nameof( tuple ) );
        IsNullable = isNullable;
        ArrayDimensions = Array.Empty<int>();
    }

    /// <summary>
    /// Initializes a new <see cref="ExtendedTypeName"/> for a <see cref="TypeName"/>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="isNullable">True to specify an explicit nullable type.</param>
    public ExtendedTypeName( TypeName type, bool isNullable = false )
    {
        TypeName = type ?? throw new ArgumentNullException( nameof( type ) );
        IsNullable = isNullable;
        ArrayDimensions = Array.Empty<int>();
    }

    /// <summary>
    /// Initializes a new <see cref="ExtendedTypeName"/> that defines an array of <see cref="ExtendedTypeName"/>.
    /// </summary>
    /// <param name="type">The item array type.</param>
    /// <param name="arrayDimensions">An optional list of array dimensions. See <see cref="ArrayDimensions"/>.</param>
    /// <param name="isNullable">True to specify an explicit nullable type.</param>
    public ExtendedTypeName( ExtendedTypeName type, IReadOnlyList<int> arrayDimensions, bool isNullable = false )
    {
        ItemArrayType = type ?? throw new ArgumentNullException( nameof( type ) );
        ArrayDimensions = arrayDimensions;
        IsNullable = isNullable;
    }

    /// <summary>
    /// Gets the type name or null if this is a <see cref="TupleTypeName"/> or <see cref="IsArray"/> is true.
    /// </summary>
    public TypeName? TypeName { get; }

    /// <summary>
    /// Gets the type name or null if this is a <see cref="TupleTypeName"/> or <see cref="IsArray"/> is true.
    /// </summary>
    public ExtendedTypeName? ItemArrayType { get; }

    /// <summary>
    /// Gets the tuple type name or null if this is a <see cref="TypeName"/>.
    /// </summary>
    public TupleTypeName? TupleTypeName { get; }

    /// <summary>
    /// Gets whether this is a tuple.
    /// </summary>
    [MemberNotNullWhen( true, nameof( TupleTypeName ) )]
    public bool IsTuple => TupleTypeName != null;

    /// <summary>
    /// Gets whether this <see cref="ArrayDimensions"/> has at least one dimension.
    /// </summary>
    [MemberNotNullWhen( true, nameof( ItemArrayType ) )]
    public bool IsArray => ItemArrayType != null;

    /// <summary>
    /// Gets the number of arrays (this defines a jagged array when there is two or more
    /// numbers in this list) and for each of them, its "dimension" (the number of ',' commas
    /// inside): 0 for a standard one-dimensional array, 1 for a two-dimensional one, etc.
    /// </summary>
    public IReadOnlyList<int> ArrayDimensions { get; }

    /// <summary>
    /// Gets whether this type is nullable (its name must be suffixed by '?').
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Returns this or a new extended type name with the given nullable flag/
    /// </summary>
    /// <param name="isNullable">Whether the type must be nullable.</param>
    /// <returns>This or a new extended type name.</returns>
    public ExtendedTypeName WithNullable( bool isNullable = true )
    {
        if( IsNullable == isNullable ) return this;
        return TupleTypeName != null
                ? new ExtendedTypeName( TupleTypeName, isNullable )
                : new ExtendedTypeName( TypeName!, isNullable );
    }

    /// <summary>
    /// Writes this ExtendedTypeName into the provided StringBuilder.
    /// </summary>
    /// <param name="b">The target.</param>
    /// <param name="nameReplacer">Optional naked type name replacer function.</param>
    /// <returns>The StringBuilder to enable fluent syntax.</returns>
    public StringBuilder Write( StringBuilder b, Func<string, string>? nameReplacer = null )
    {
        Throw.CheckNotNullArgument( b );
        if( IsTuple )
        {
            Throw.DebugAssert( TupleTypeName != null );
            TupleTypeName.Write( b, nameReplacer );
        }
        else if( TypeName != null )
        {
            TypeName.Write( b, nameReplacer );
        }
        else
        {
            Throw.DebugAssert( ItemArrayType != null );
            ItemArrayType.Write( b, nameReplacer );
        }
        foreach( int d in ArrayDimensions )
        {
            b.Append( '[' ).Append( ',', d ).Append( ']' );
        }
        if( IsNullable ) b.Append( '?' );
        return b;
    }

    /// <summary>
    /// Overridden to return the <see cref="Write"/> result.
    /// </summary>
    /// <returns>The type string.</returns>
    public override string ToString() => Write( new StringBuilder() ).ToString();

}

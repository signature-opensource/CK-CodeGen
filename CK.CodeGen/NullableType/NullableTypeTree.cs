using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.CodeGen;

/// <summary>
/// Full immutable representation of a Nullable Type with its <see cref="RawSubTypes"/>.
/// </summary>
public readonly struct NullableTypeTree : IEquatable<NullableTypeTree>
{
    readonly NullableTypeTree[] _rawSubTypes;

    class FixDictionaryBuilder : INullableTypeTreeBuilder
    {
        public static readonly INullableTypeTreeBuilder Instance = new FixDictionaryBuilder();

        public NullableTypeTree Create( Type t, NullabilityTypeKind kind, NullableTypeTree[] subTypes, Type[]? genericArguments = null )
        {
            if( genericArguments?.Length == 2 )
            {
                var tGen = t.GetGenericTypeDefinition();
                if( tGen == typeof( IDictionary<,> ) || tGen == typeof( Dictionary<,> ) )
                {
                    var tKey = subTypes[0];
                    if( tKey.Kind.IsNullable() && tKey.Kind.IsReferenceType() )
                    {
                        subTypes[0] = tKey.ToAbnormalNull();
                    }
                }
            }
            return new NullableTypeTree( t, kind, subTypes );
        }
    }

    /// <summary>
    /// Gets or sets a builder that will be used by <see cref="NullabilityTypeExtensions.GetNullableTypeTree(Type, INullableTypeTreeBuilder?)"/>
    /// when no other builder is specified.
    /// This defaults to a builder that ensures that the key of <see cref="Dictionary{TKey, TValue}"/> and <see cref="IDictionary{TKey, TValue}"/>
    /// is not nullable.
    /// </summary>
    public static INullableTypeTreeBuilder ObliviousDefaultBuilder { get; set; } = FixDictionaryBuilder.Instance;

    /// <summary>
    /// Gets the type.
    /// When this <see cref="Kind"/> is a <see cref="NullablityTypeKindExtension.IsNullableValueType(NullabilityTypeKind)"/> (a <see cref="Nullable{T}"/>),
    /// then this type is the inner type, not the Nullable generic type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The <see cref="NullabilityTypeKind"/> for this <see cref="Type"/>.
    /// </summary>
    public NullabilityTypeKind Kind { get; }

    /// <summary>
    /// The subordinates types if any. Can be generic parameters of the <see cref="Type"/> or the item type of an array.
    /// This "raw" types are the direct children: for <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> only
    /// the 8 types appear (including the last value tuple that can be a singleton).
    /// </summary>
    public IReadOnlyList<NullableTypeTree> RawSubTypes => _rawSubTypes;

    /// <summary>
    /// The subordinates types if any. This flattens <see cref="RawSubTypes"/> if <see cref="IsLongValueTuple"/> is true.
    /// </summary>
    public readonly IEnumerable<NullableTypeTree> SubTypes => IsLongValueTuple
                                                                ? RawSubTypes.Take( 7 ).Concat( RawSubTypes[7].SubTypes )
                                                                : RawSubTypes;

    /// <summary>
    /// Gets whether this is a <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/>: TRest is a value tuple that
    /// is the singleton <see cref="ValueTuple{T}"/> when the actual value tuple contains exactly 8 parameters.
    /// </summary>
    public bool IsLongValueTuple => Kind.IsTupleType() && RawSubTypes.Count == 8;

    /// <summary>
    /// Gets whether this top <see cref="Type"/> is a "normal null".
    /// We consider "Null Normality" as simply being <c>Kind.IsReferenceType() == Kind.IsNullable()</c>: a reference type
    /// is "normally nullable" and a value type is "normally not nullable".
    /// <para>
    /// This acts as a normalized form that can avoid doubling a cache of <see cref="NullableTypeTree"/> information when
    /// the cached information is easily computed to the opposite "not normally nullable" form (like adding or removing a '?'
    /// after a type name).
    /// </para>
    /// </summary>
    public bool IsNormalNull => Kind.IsReferenceType() == Kind.IsNullable();

    /// <summary>
    /// Returns this full <see cref="NullableTypeTree"/> with its top <see cref="Type"/> in <see cref="IsNormalNull"/> form:
    /// <list type="bullet">
    /// <item>
    /// If this is a nullable reference type or a non nullable value type, this is returned unchanged.
    /// </item>
    /// <item>
    /// If this is a non-nullable reference type, a new tree with the same <see cref="Type"/> and a <see cref="NullabilityTypeKind.IsNullable"/> kind
    /// is returned.
    /// </item>
    /// <item>
    /// If this is a nullable value type, a new tree with the (already not nullable) <see cref="Type"/> and a non <see cref="NullabilityTypeKind.IsNullable"/> kind
    /// is returned.
    /// </item>
    /// </list>
    /// This doesn't change the type tree in depth, only this top Type/Kind is concerned by this normalization.
    /// </summary>
    /// <returns>This or a normalized tree.</returns>
    public NullableTypeTree ToNormalNull()
    {
        if( Kind.IsReferenceType() )
        {
            return Kind.IsNullable()
                    ? this
                    : new NullableTypeTree( Type, Kind | NullabilityTypeKind.IsNullable, _rawSubTypes );
        }
        return Kind.IsNullable()
                ? new NullableTypeTree( Type, Kind & ~NullabilityTypeKind.IsNullable, _rawSubTypes )
                : this;
    }

    /// <summary>
    /// Mirror of <see cref="ToNormalNull"/> with the same limitation: this doesn't change the type tree in depth,
    /// only this top Type/Kind is concerned by this normalization.
    /// </summary>
    /// <returns>This or an "abnormal" tree.</returns>
    public NullableTypeTree ToAbnormalNull()
    {
        if( Kind.IsReferenceType() )
        {
            return Kind.IsNullable()
                    ? new NullableTypeTree( Type, Kind & ~NullabilityTypeKind.IsNullable, _rawSubTypes )
                    : this;
        }
        return Kind.IsNullable() || Type == typeof( void )
                ? this
                : new NullableTypeTree( Type, Kind | NullabilityTypeKind.IsNullable, _rawSubTypes );
    }

    /// <summary>
    /// Returns a non nullable reference or value type.
    /// </summary>
    /// <returns>A non nullable tree.</returns>
    public NullableTypeTree ToNonNullable() => Kind.IsReferenceType() ? ToAbnormalNull() : ToNormalNull();

    /// <summary>
    /// Returns a nullable reference or value type.
    /// </summary>
    /// <returns>A nullable tree.</returns>
    public NullableTypeTree ToNullable() => Kind.IsReferenceType() ? ToNormalNull() : ToAbnormalNull();

    /// <summary>
    /// Returns a <see cref="NullableTypeTree"/> with a changed primary <see cref="Type"/> and/or <see cref="Kind"/> but
    /// with exactly the same <see cref="RawSubTypes"/> as this one.
    /// This must be used with care (no check is done on the changed type): this is typically useful
    /// to change a <see cref="IList{T}"/> to a <see cref="List{T}"/> and preserve the nullability
    /// information.
    /// </summary>
    /// <param name="newType">The new type (when not null).</param>
    /// <param name="newKind">The new kind  (when not null).</param>
    /// <returns>A new tree with the same nullability information.</returns>
    public NullableTypeTree With( Type? newType = null, NullabilityTypeKind? newKind = null )
    {
        return new NullableTypeTree( newType ?? Type, newKind ?? Kind, _rawSubTypes );
    }

    /// <summary>
    /// Returns a <see cref="NullableTypeTree"/> with a changed (or not) subordinated <see cref="SubTypes"/> but
    /// always with exactly the same <see cref="Kind"/> and <see cref="Type"/> as this one.
    /// This must be used with care (no check is done on the updated subordinated type).
    /// </summary>
    /// <param name="idx">Index in the <see cref="SubTypes"/> that must be set.</param>
    /// <param name="newOne">The new subordinated type.</param>
    /// <returns>A new tree with a true flag or the same tree with a false flag.</returns>
    /// <remarks>
    /// Returning the HasChanged flag avoids another comparison (that is in depth) to know if the
    /// update have had any effect.
    /// </remarks>
    public (NullableTypeTree Result, bool HasChanged) WithSubTypeAt( int idx, NullableTypeTree newOne )
    {
        Throw.CheckOutOfRangeArgument( idx >= 0 );
        var s = _rawSubTypes;
        if( !IsLongValueTuple || idx < 7 )
        {
            Throw.CheckOutOfRangeArgument( idx < _rawSubTypes.Length );
            if( _rawSubTypes[idx] != newOne )
            {
                s = (NullableTypeTree[])_rawSubTypes.Clone();
                s[idx] = newOne;
            }
        }
        else
        {
            var n = s[7].WithSubTypeAt( idx - 7, newOne );
            if( n.HasChanged )
            {
                s = (NullableTypeTree[])_rawSubTypes.Clone();
                s[7] = n.Result;
            }
        }
        return (new NullableTypeTree( Type, Kind, s ), s != _rawSubTypes);
    }

    /// <summary>
    /// Returns a transformed <see cref="NullableTypeTree"/> by a depth-first traversal.
    /// This must be used with care (no check is done on the changed types).
    /// </summary>
    /// <param name="transformer">A function that can transform all new subordinated types and returns null for unchanged type.</param>
    /// <param name="subTypesOnly">True to apply the transformer only on subordinated types, skipping this one.</param>
    /// <returns>A new tree.</returns>
    /// <remarks>
    /// Returning the HasChanged flag avoids another comparison (that is in depth) to know if the
    /// update have had any effect.
    /// </remarks>
    public (NullableTypeTree Result, bool HasChanged) Transform( Func<NullableTypeTree, NullableTypeTree?> transformer, bool subTypesOnly = false )
    {
        Throw.CheckNotNullArgument( transformer );
        var s = _rawSubTypes;
        for( int idx = 0; idx < _rawSubTypes.Length; ++idx )
        {
            var applied = s[idx].Transform( transformer );
            if( applied.HasChanged )
            {
                if( _rawSubTypes == s ) s = (NullableTypeTree[])_rawSubTypes.Clone();
                s[idx] = applied.Result;
            }
            var newOne = transformer( s[idx] );
            if( newOne != null )
            {
                if( _rawSubTypes == s ) s = (NullableTypeTree[])_rawSubTypes.Clone();
                s[idx] = newOne.Value;
            }
        }
        bool hasChanged = s != _rawSubTypes;
        var t = new NullableTypeTree( Type, Kind, s );
        if( !subTypesOnly )
        {
            var tThis = transformer( t );
            if( tThis != null )
            {
                t = tThis.Value;
                hasChanged = true;
            }
        }
        return (t, hasChanged);
    }

    /// <summary>
    /// Initializes a new <see cref="NullableTypeTree"/>.
    /// No checks are done on the parameters (except ArgumentNullException and the fact that <paramref name="t"/> must not be Nullable&lt;&gt;):
    /// they must be coherent otherwise behavior is undefined.
    /// </summary>
    /// <param name="t">The Type.</param>
    /// <param name="k">The <see cref="NullabilityTypeKind"/>.</param>
    /// <param name="subTypes">The sub types (generic parameters or array element).</param>
    public NullableTypeTree( Type t, NullabilityTypeKind k, NullableTypeTree[] subTypes )
    {
        if( t == null ) throw new ArgumentNullException( nameof( t ) );
        if( subTypes == null ) throw new ArgumentNullException( nameof( subTypes ) );
        if( t.IsGenericType && t.GetGenericTypeDefinition() == typeof( Nullable<> ) ) throw new ArgumentException( "Cannot be a Nullable<>.", nameof( t ) );
        Type = t;
        Kind = k;
        _rawSubTypes = subTypes;
    }

    /// <summary>
    /// Merges nullabilities of reference types only: the final types are not changed by this method.
    /// The other <see cref="Type"/> must be the same type as this one (otherwise an <see cref="ArgumentException"/> is thrown).
    /// Nullable reference type wins: this implements a kind of "generalization" for contravariance. 
    /// </summary>
    /// <param name="other">The other type information.</param>
    /// <returns>This or a new NullableTypeTree.</returns>
    public NullableTypeTree MergeReferenceTypesNullability( NullableTypeTree other )
    {
        if( Type != other.Type ) throw new ArgumentException( $"Nullability informations can only be merged for the same type. '{Type.ToCSharpName()}' is not the same as '{other.Type.ToCSharpName()}'.", nameof( other ) );
        Throw.DebugAssert( RawSubTypes.Count == other.RawSubTypes.Count );

        NullableTypeTree[]? subTypes = null;
        for( int i = 0; i < RawSubTypes.Count; ++i )
        {
            var t = RawSubTypes[i];
            Throw.DebugAssert( t.Type == other.RawSubTypes[i].Type );
            var m = t.MergeReferenceTypesNullability( other.RawSubTypes[i] );
            if( subTypes == null
                && ((m.Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) != (t.Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable))
                    || m.RawSubTypes != t.RawSubTypes) )
            {
                subTypes = new NullableTypeTree[RawSubTypes.Count];
                for( int j = 0; j < i; j++ ) subTypes[j] = RawSubTypes[j];
            }
            if( subTypes != null ) subTypes[i] = m;
        }
        var kind = Kind;
        if( !Type.IsValueType )
        {
            kind = Kind | (other.Kind & (NullabilityTypeKind.IsNullable | NullabilityTypeKind.NRTFullNullable | NullabilityTypeKind.NRTFullNonNullable));
        }
        return new NullableTypeTree( Type, kind, subTypes ?? _rawSubTypes );
    }

    /// <summary>
    /// Merges nullabilities of reference and value types: the final types can be changed (when nullable value types are concerned).
    /// The other <see cref="Type"/> must be the "same type regardless of nullability" as this one (otherwise an <see cref="ArgumentException"/> is thrown).
    /// Nullable type wins: this implements a kind of "generalization" for contravariance.
    /// <para>
    /// Since "equality" of the two candidate types cannot be easily tested (this would imply to do a good part of what this method does),
    /// this returns a null NullableTypeTree if the two "types regardless of nullability" are different.
    /// </para>
    /// </summary>
    /// <param name="other">The other type information.</param>
    /// <returns>A NullableTypeTree or null if the two types are different (regardless of nullability).</returns>
    public NullableTypeTree? TryMergeNullabilities( NullableTypeTree other )
    {
        if( Kind == NullabilityTypeKind.None ) throw new InvalidOperationException( "Unitialized NullableTypeTree." );
        if( other.Kind == NullabilityTypeKind.None ) throw new ArgumentException( "Unitialized NullableTypeTree.", nameof( other ) );
        if( RawSubTypes.Count != other.RawSubTypes.Count )
        {
            // Not the same number of subtypes. Types are really different.
            return null;
        }
        // Prepare the work by checking the type and choosing the winner at this level.
        var kind = Kind;
        // The type will be used only if subTypes have not changed.
        var type = Type;
        // Regardless of their exact types, the two types must be both generic or not. And if they are generics,
        // they must share the same definition.
        Type? genDef = null;
        if( type.IsGenericType )
        {
            if( !other.Type.IsGenericType )
            {
                return null;
            }
            genDef = type.GetGenericTypeDefinition();
            if( genDef != other.Type.GetGenericTypeDefinition() )
            {
                return null;
            }
            // For generic types cannot conclude anything more here about "equality" without recursively processing the subTypes:
            // a List<(string,(int?,int?)> is not the same type as a List<(string,(int,int))>
            // so challenging the types here is useless.
        }
        else
        {
            // For non-generic types, the types must be the same (Nullable<T> is lifted by the NullableTypeTree).
            if( Type != other.Type ) return null;
        }

        if( type.IsValueType )
        {
            // If this is not a nullable value type, choose the other one,
            // whatever it may be.
            if( !kind.IsNullable() ) kind = other.Kind;
        }
        else
        {
            kind = Kind | (other.Kind & (NullabilityTypeKind.IsNullable | NullabilityTypeKind.NRTFullNullable | NullabilityTypeKind.NRTFullNonNullable));
        }

        // Merging the subTypes.
        bool atLeastOneActualSubTypeDifferFromThis = false;
        bool atLeastOneActualSubTypeDifferFromOther = false;
        NullableTypeTree[]? subTypes = null;
        for( int i = 0; i < RawSubTypes.Count; ++i )
        {
            var t = RawSubTypes[i];
            var m = t.TryMergeNullabilities( other.RawSubTypes[i] );
            if( m == null )
            {
                // One of the subtypes differ: types are really different.
                return null;
            }
            Throw.DebugAssert( m.Value.Kind != NullabilityTypeKind.None );
            if( subTypes == null
                && ((m.Value.Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) != (t.Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable))
                    || m.Value.RawSubTypes != t.RawSubTypes) )
            {
                subTypes = new NullableTypeTree[RawSubTypes.Count];
                for( int j = 0; j < i; j++ )
                {
                    var tJ = RawSubTypes[j];
                    subTypes[j] = tJ;
                    var tJo = other.RawSubTypes[j];
                    atLeastOneActualSubTypeDifferFromOther |= (tJ.Type != tJo.Type || tJ.Kind.IsNullableValueType() != tJo.Kind.IsNullableValueType());
                }
            }
            if( subTypes != null )
            {
                subTypes[i] = m.Value;
                atLeastOneActualSubTypeDifferFromThis |= (m.Value.Type != t.Type || m.Value.Kind.IsNullableValueType() != t.Kind.IsNullableValueType());
                atLeastOneActualSubTypeDifferFromOther |= (m.Value.Type != other.RawSubTypes[i].Type || m.Value.Kind.IsNullableValueType() != other.RawSubTypes[i].Kind.IsNullableValueType());
            }
        }
        // If subTypes have not changed, there's nothing to do, but if they have changed and the
        // types are generics, then a new generic type may be needed based on the subTypes.
        if( genDef != null && subTypes != null )
        {
            if( !atLeastOneActualSubTypeDifferFromOther ) type = other.Type;
            else if( !atLeastOneActualSubTypeDifferFromThis ) type = Type;
            else
            {
                type = genDef.MakeGenericType( subTypes.Select( t => t.Kind.IsNullableValueType() ? typeof( Nullable<> ).MakeGenericType( t.Type ) : t.Type ).ToArray() );
            }
        }
        return new NullableTypeTree( type, kind, subTypes ?? _rawSubTypes );
    }

    /// <summary>
    /// See <see cref="Equals(NullableTypeTree)"/>.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True if they are the same, false otherwise.</returns>
    public override bool Equals( object? obj ) => obj is NullableTypeTree tree && Equals( tree );

    /// <summary>
    /// Implements a strict equality except that <see cref="NullabilityTypeKind.NRTFullNullable"/> and <see cref="NullabilityTypeKind.NRTFullNonNullable"/> bits
    /// are ignored (these bits are implementation details).
    /// </summary>
    /// <param name="other">The other nullable type tree.</param>
    /// <returns>True if they are the same, false otherwise.</returns>
    public bool Equals( NullableTypeTree other )
    {
        return (Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) == (other.Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable))
                && Type == other.Type
                && _rawSubTypes.SequenceEqual( other._rawSubTypes );
    }

    /// <summary>
    /// See <see cref="Equals(NullableTypeTree)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        HashCode c = new HashCode();
        c.Add( Type );
        c.Add( (byte)(Kind & ~(NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) );
        foreach( var t in RawSubTypes ) c.Add( t.GetHashCode() );
        return c.ToHashCode();
    }

    /// <summary>
    /// Produces a string description of this <see cref="NullableTypeTree"/>.
    /// </summary>
    /// <param name="b">The string builder to use.</param>
    /// <param name="withNamespace">True to include the types' namespace and enclosing types (non generic) if any.</param>
    /// <returns>The string builder.</returns>
    public StringBuilder ToString( StringBuilder b, bool withNamespace = false )
    {
        Throw.CheckNotNullArgument( b );
        if( Type.IsArray )
        {
            RawSubTypes[0].ToString( b, withNamespace );
            b.Append( '[' ).Append( ',', Type.GetArrayRank() - 1 ).Append( ']' );
        }
        else
        {
            if( (Kind & NullabilityTypeKind.IsGenericType) != 0 )
            {
                bool isTuple = Type.IsValueTuple();
                if( isTuple )
                {
                    b.Append( '(' );
                }
                else
                {
                    var n = CodeWriterExtensions.GetTypeAlias( Type );
                    if( n == null )
                    {
                        n = Type.Name;
                        int idx = n.IndexOf( '`', StringComparison.Ordinal );
                        if( idx > 0 ) n = n.Substring( 0, idx );
                        if( withNamespace ) DumpNamespace( b, Type );
                    }
                    b.Append( n ).Append( '<' );
                }
                bool atLeastOne = false;
                foreach( var t in SubTypes )
                {
                    if( atLeastOne ) b.Append( ',' );
                    else atLeastOne = true;
                    t.ToString( b, withNamespace );
                }
                b.Append( isTuple ? ')' : '>' );
            }
            else
            {
                var n = CodeWriterExtensions.GetTypeAlias( Type );
                if( n == null )
                {
                    if( withNamespace ) DumpNamespace( b, Type );
                    n = Type.Name;
                }
                b.Append( n );
            }
        }
        if( Kind.IsNullable() ) b.Append( '?' );
        return b;

        static void DumpNamespace( StringBuilder b, Type t, bool withName = false )
        {
            if( t.DeclaringType != null ) DumpNamespace( b, t.DeclaringType, true );
            else b.Append( t.Namespace ).Append( '.' );
            if( withName ) b.Append( t.Name ).Append( '.' );
        }

    }

    /// <summary>
    /// Produces a string description of this <see cref="NullableTypeTree"/>.
    /// </summary>
    /// <param name="withNamespace">True to include the types' namespace and enclosing types (non generic) if any.</param>
    /// <returns>The type name.</returns>
    public string ToString( bool withNamespace ) => ToString( new StringBuilder(), withNamespace ).ToString();

    /// <summary>
    /// Calls <see cref="ToString(StringBuilder,bool)"/> (without namespaces) and returns the result.
    /// </summary>
    /// <returns>The type name without namespace nor nesting types.</returns>
    public override string ToString() => ToString( false );

    public static bool operator ==( NullableTypeTree x, NullableTypeTree y ) => x.Equals( y );

    public static bool operator !=( NullableTypeTree x, NullableTypeTree y ) => !x.Equals( y );
}


using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.CodeGen;


/// <summary>
/// Captures a type nullability information.
/// See https://github.com/dotnet/roslyn/blob/master/docs/features/nullable-metadata.md
/// <para>
/// The extension methods <see cref="NullabilityTypeExtensions.GetNullabilityInfo(System.Reflection.ParameterInfo)"/>, <see cref="NullabilityTypeExtensions.GetNullabilityInfo(System.Reflection.ParameterInfo)"/>
/// or <see cref="NullabilityTypeExtensions.GetNullabilityInfo(System.Reflection.FieldInfo)"/> retrieve this information.
/// </para>
/// </summary>
public readonly struct NullabilityTypeInfo : IEquatable<NullabilityTypeInfo>
{
    readonly byte[]? _profile;

    /// <summary>
    /// Gets the root <see cref="NullabilityTypeKind"/>.
    /// </summary>
    public NullabilityTypeKind Kind { get; }

    /// <summary>
    /// Gets the full nullable profile or an empty span if there is no complex NRT marker.
    /// When not empty, it starts with the nullable indicator of the root type. 
    /// </summary>
    public ReadOnlySpan<byte> NullableProfile => _profile ?? ReadOnlySpan<byte>.Empty;

    /// <summary>
    /// Gets whether this profile has been obtained from the declaring type's NullableContextAttribute
    /// rather than the NullableAttribute.
    /// This does not participate in equality.
    /// </summary>
    public bool FromContext { get; }

    /// <summary>
    /// Initializes a new <see cref="NullabilityTypeInfo"/>.
    /// </summary>
    /// <param name="kind">The <see cref="Kind"/>.</param>
    /// <param name="nullableProfile">The optional <see cref="NullableProfile"/>.</param>
    /// <param name="fromContext">See <see cref="FromContext"/>.</param>
    public NullabilityTypeInfo( NullabilityTypeKind kind, byte[]? nullableProfile, bool fromContext )
    {
        Kind = kind;
        _profile = nullableProfile;
        FromContext = fromContext;
    }

    /// <summary>
    /// Equality is based on an exact match of <see cref="Kind"/> and <see cref="NullableProfile"/>
    /// but also handles the case when one of the two is oblivious of NRT and the other is <see cref="NullablityTypeKindExtension.IsNRTFullNullable(NullabilityTypeKind)"/>.
    /// </summary>
    /// <param name="other">The other info.</param>
    /// <returns>True if this is equal to other, false otherwise.</returns>
    public bool Equals( NullabilityTypeInfo other )
    {
        Throw.DebugAssert( "If Kind are equals then both have a profile or not.", Kind != other.Kind || ((_profile == null) == (other._profile == null)) );
        // Strict equality.
        if( Kind == other.Kind
            && (_profile == null || _profile.SequenceEqual( other._profile! )) )
        {
            return true;
        }
        // Basic type properties must be the same.
        if( (Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) != (other.Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) )
        {
            return false;
        }
        // If this is a Full NRT nullable and the other is not NRT aware it is necessarily nullable since this is a reference type and
        // other has the same basic type kind, it is nullable by default.
        if( Kind.IsNRTFullNullable() && !other.Kind.IsNRTAware() )
        {
            Throw.DebugAssert( other.Kind.IsReferenceType() && other.Kind.IsNullable() );
            return true;
        }
        // Reverse the previous check.
        if( other.Kind.IsNRTFullNullable() && !Kind.IsNRTAware() )
        {
            Throw.DebugAssert( Kind.IsReferenceType() && Kind.IsNullable() );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Overridden to call <see cref="Equals(NullabilityTypeInfo)"/>.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True if this is equal to other, false otherwise.</returns>
    public override bool Equals( object? obj ) => obj is NullabilityTypeInfo o ? Equals( o ) : false;

    /// <summary>
    /// Overridden to combine  <see cref="Kind"/> and <see cref="NullableProfile"/>, excluding NRT flags from kind.
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => HashCode.Combine( (Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)), _profile );

    /// <summary>
    /// Generates a full annotations list starting with the one applicable to the root type.
    /// This is the <see cref="NullableProfile"/> if it is not empty, and at most 10000 identical bytes
    /// corresponding to the optimized root annotation otherwise.
    /// </summary>
    /// <returns>The annotation list.</returns>
    public IEnumerable<byte> GenerateAnnotations() => _profile ?? OptimizedAnnotations();

    IEnumerable<byte> OptimizedAnnotations()
    {
        Throw.DebugAssert( _profile == null );
        byte thisOne = 0;
        if( Kind.IsNRTFullNonNullable() ) thisOne = 1;
        else if( Kind.IsNRTFullNullable() ) thisOne = 2;
        for( int i = 0; i < 10000; ++i ) yield return thisOne;
    }

    /// <summary>
    /// Gets a readable string.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString()
    {
        string s = Kind.ToStringClear();
        if( _profile != null )
        {
            s += " - ";
            foreach( var b in _profile ) s += (char)('0' + b);
        }
        return s;
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static bool operator ==( NullabilityTypeInfo left, NullabilityTypeInfo right ) => left.Equals( right );

    public static bool operator !=( NullabilityTypeInfo left, NullabilityTypeInfo right ) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

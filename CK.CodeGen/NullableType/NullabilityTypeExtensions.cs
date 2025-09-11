using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.CodeGen;

/// <summary>
/// Extends Type with helper methods.
/// </summary>
public static partial class NullabilityTypeExtensions
{
    /// <summary>
    /// Gets the <see cref="NullabilityTypeKind"/> for a type.
    /// A reference type cannot be, by itself, a <see cref="NullabilityTypeKind.NonNullableReferenceType"/>: this
    /// method always return <see cref="NullabilityTypeKind.NullableReferenceType"/> or <see cref="NullabilityTypeKind.NullableGenericReferenceType"/> for
    /// classes and interfaces.
    /// </summary>
    /// <param name="this">This type.</param>
    /// <remarks>
    /// <para>
    /// <c>typeof(List&lt;string?&gt;)</c> is valid but <c>typeof(List&lt;string?&gt;?)</c>
    /// cannot compile and this makes sense: the "outer", "root" nullability depends on the usage of the type: non nullable reference types can be obtained
    /// via a <see cref="ParameterInfo"/> or a <see cref="PropertyInfo"/> that "references" their type.
    /// <para>
    /// </para>
    /// However, <c>typeof(List&lt;string?&gt;)</c> could have been a <see cref="NullabilityTypeKind.NRTFullNullable"/>, but it is not, it is actually
    /// oblivious to nullable: both <c>typeof(List&lt;string?&gt;)</c> and <c>typeof(List&lt;string&gt;)</c> are marked with a single 0 byte.
    /// </para>
    /// </remarks>
    /// <returns>The nullability kind.</returns>
    public static NullabilityTypeKind GetNullabilityKind( this Type @this )
    {
        Throw.CheckNotNullArgument( @this );
        if( @this.IsInterface || @this.IsClass )
        {
            return @this.IsGenericType ? NullabilityTypeKind.NullableGenericReferenceType : NullabilityTypeKind.NullableReferenceType;
        }
        if( @this.IsValueType )
        {
            Type? inner;
            if( @this.IsGenericType && (inner = Nullable.GetUnderlyingType( @this )) != null )
            {
                if( !inner.IsGenericType ) return NullabilityTypeKind.NullableValueType;
                return inner.IsValueTuple() ? NullabilityTypeKind.NullableTupleType : NullabilityTypeKind.NullableGenericValueType;
            }
            if( !@this.IsGenericType ) return NullabilityTypeKind.IsValueType;
            return @this.IsValueTuple() ? NullabilityTypeKind.NonNullableTupleType : NullabilityTypeKind.NonNullableGenericValueType;
        }
        if( @this.IsGenericTypeParameter )
        {
            return NullabilityTypeKind.IsGenericParameter;
        }
        throw new ArgumentException( $"What's this type that is not an interface, a class, a value type or a generic type parameter?: {@this.AssemblyQualifiedName}", nameof( @this ) );
    }

    /// <summary>
    /// Gets the <see cref="NullabilityTypeInfo"/> for a parameter.
    /// <param name="this">This parameter.</param>
    /// </summary>
    /// <returns>The nullability info for the parameter.</returns>
    [DebuggerStepThrough]
    public static NullabilityTypeInfo GetNullabilityInfo( this ParameterInfo @this )
    {
        return GetNullabilityInfo( @this.ParameterType, @this.Member, @this.CustomAttributes );
    }

    /// <summary>
    /// Creates a <see cref="NullableTypeTree"/> for a parameter's type.
    /// The type must not be a nested type in a generic (its <see cref="Type.DeclaringType"/> must be null or not be a generic type)
    /// otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="this">This parameter.</param>
    /// <param name="builder">Optional builder.</param>
    /// <returns>The nullable tree for the parameter' type.</returns>
    [DebuggerStepThrough]
    public static NullableTypeTree GetNullableTypeTree( this ParameterInfo @this, INullableTypeTreeBuilder? builder = null )
    {
        var info = GetNullabilityInfo( @this );
        return GetNullableTypeTree( @this.ParameterType, info, builder );
    }

    /// <summary>
    /// Gets the <see cref="NullabilityTypeInfo"/> for a property.
    /// </summary>
    /// <param name="this">This property.</param>
    /// <returns>The nullability info for the parameter.</returns>
    [DebuggerStepThrough]
    public static NullabilityTypeInfo GetNullabilityInfo( this PropertyInfo @this )
    {
        return GetNullabilityInfo( @this.PropertyType, @this.DeclaringType, @this.CustomAttributes );
    }

    /// <summary>
    /// Creates a <see cref="NullableTypeTree"/> for a property's type.
    /// The type must not be a nested type in a generic (its <see cref="Type.DeclaringType"/> must be null or not be a generic type)
    /// otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="this">This property.</param>
    /// <param name="builder">Optional builder.</param>
    /// <returns>The nullable tree for the property's type.</returns>
    [DebuggerStepThrough]
    public static NullableTypeTree GetNullableTypeTree( this PropertyInfo @this, INullableTypeTreeBuilder? builder = null )
    {
        var info = GetNullabilityInfo( @this );
        return GetNullableTypeTree( @this.PropertyType, info, builder );
    }

    /// <summary>
    /// Gets the <see cref="NullabilityTypeInfo"/> for a field.
    /// </summary>
    /// <param name="this">This field.</param>
    /// <returns>The nullability info for the field.</returns>
    [DebuggerStepThrough]
    public static NullabilityTypeInfo GetNullabilityInfo( this FieldInfo @this )
    {
        return GetNullabilityInfo( @this.FieldType, @this.DeclaringType, @this.CustomAttributes );
    }

    /// <summary>
    /// Creates a <see cref="NullableTypeTree"/> for a field's type.
    /// The type must not be a nested type in a generic (its <see cref="Type.DeclaringType"/> must be null or not be a generic type)
    /// otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="this">This field.</param>
    /// <param name="builder">Optional builder.</param>
    /// <returns>The nullable tree for the fields's type.</returns>
    [DebuggerStepThrough]
    public static NullableTypeTree GetNullableTypeTree( this FieldInfo @this, INullableTypeTreeBuilder? builder = null )
    {
        var info = GetNullabilityInfo( @this );
        return GetNullableTypeTree( @this.FieldType, info, builder );
    }

    /// <summary>
    /// Creates a <see cref="NullableTypeTree"/> for this type based on a root <see cref="NullabilityTypeInfo"/> that must
    /// have been computed for this type otherwise behavior is undefined.
    /// <para>
    /// This low-level method doesn't use by default the <see cref="NullableTypeTree.ObliviousDefaultBuilder"/>.
    /// It has to be provided explicitly to dictionary keys to not be nullable.
    /// </para>
    /// </summary>
    /// <param name="this">This type.</param>
    /// <param name="info">The nullability info.</param>
    /// <param name="builder">Optional builder.</param>
    /// <returns>The detailed, recursive, <see cref="NullableTypeTree"/>.</returns>
    [DebuggerStepThrough]
    public static NullableTypeTree GetNullableTypeTree( this Type @this, NullabilityTypeInfo info, INullableTypeTreeBuilder? builder = null )
    {
        return GetNullableTypeTreeWithProfile( @this, info.GenerateAnnotations().GetEnumerator(), info.Kind, builder );
    }

    /// <summary>
    /// Creates a <see cref="NullableTypeTree"/> for this type based on no specific NRT profile (oblivious context):
    /// all reference types that appear in the tree are de facto nullable.
    /// The generic 'notnull' constraint is (currently and unfortunately) ignored (failed to handle it so far).
    /// As a workaround, when the <paramref name="builder"/> is null, the <see cref="NullableTypeTree.ObliviousDefaultBuilder"/> is used
    /// that corrects this.
    /// </summary>
    /// <param name="this">This type.</param>
    /// <param name="builder">Optional builder.</param>
    /// <returns>The detailed, recursive, <see cref="NullableTypeTree"/>.</returns>
    [DebuggerStepThrough]
    public static NullableTypeTree GetNullableTypeTree( this Type @this, INullableTypeTreeBuilder? builder = null )
    {
        var info = new NullabilityTypeInfo( GetNullabilityKind( @this ), null, false );
        return GetNullableTypeTreeWithProfile( @this, info.GenerateAnnotations().GetEnumerator(), info.Kind, builder ?? NullableTypeTree.ObliviousDefaultBuilder );
    }

    static NullableTypeTree GetNullableTypeTreeWithProfile( Type t, IEnumerator<byte> annotations, NullabilityTypeKind known, INullableTypeTreeBuilder? builder )
    {
        if( t.IsGenericParameter )
        {
            Throw.ArgumentException( $"Type '{t.Name}' is a generic parameter. Open generics are not supported.", nameof( t ) );
        }
        if( t.DeclaringType != null && t.DeclaringType.IsGenericType )
        {
            Throw.ArgumentException( $"Type '{t.Name}' is nested in a generic type ({t.DeclaringType.ToCSharpName()}). Only nested types in non generic types are supported.", nameof( t ) );
        }
        NullableTypeTree[] sub = Array.Empty<NullableTypeTree>();
        bool isInside;
        if( isInside = (known == NullabilityTypeKind.None) )
        {
            known = t.GetNullabilityKind();
        }
        if( known.IsNullableValueType() )
        {
            // Lift the Nullable<T>.
            t = Nullable.GetUnderlyingType( t )!;
        }
        if( isInside && !known.IsNonGenericValueType() )
        {
            // Consume our annotation.
            if( !annotations.MoveNext() )
            {
                throw new InvalidOperationException( $"Byte annotations too short." );
            }
            var thisOne = annotations.Current;
            // Annotations only apply to reference types. Only 1 (not null!) is of interest.
            if( thisOne == 1 && known.IsReferenceType() )
            {
                Throw.DebugAssert( known.IsNullable() );
                known &= ~NullabilityTypeKind.IsNullable;
            }
        }
        Type[]? genArgs = null;
        if( t.HasElementType )
        {
            Throw.DebugAssert( known.IsReferenceType() );
            sub = new[] { GetNullableTypeTreeWithProfile( t.GetElementType()!, annotations, default, builder ) };
        }
        else if( t.IsGenericType )
        {
            Throw.DebugAssert( "This has been already computed.", (known & NullabilityTypeKind.IsGenericType) != 0 );
            genArgs = t.GetGenericArguments();
            sub = new NullableTypeTree[genArgs.Length];
            int idx = 0;
            foreach( var g in genArgs )
            {
                sub[idx++] = GetNullableTypeTreeWithProfile( g, annotations, default, builder );
            }
        }
        return builder != null ? builder.Create( t, known, sub, genArgs ) : new NullableTypeTree( t, known, sub );
    }

    static NullabilityTypeInfo GetNullabilityInfo( Type t, MemberInfo? parent, IEnumerable<CustomAttributeData> attributes )
    {
        byte[]? profile = null;
        var n = GetNullabilityKind( t );
        bool fromContext = false;
        if( !n.IsNonGenericValueType() )
        {
            var a = attributes.FirstOrDefault( a => a.AttributeType.Name == "NullableAttribute" && a.AttributeType.Namespace == "System.Runtime.CompilerServices" );
            if( a == null )
            {
                while( parent != null )
                {
                    fromContext = true;
                    a = parent.CustomAttributes.FirstOrDefault( a => a.AttributeType.Name == "NullableContextAttribute" && a.AttributeType.Namespace == "System.Runtime.CompilerServices" );
                    if( a != null )
                    {
                        n = HandleByte( n, (byte)a.ConstructorArguments[0].Value! );
                        break;
                    }
                    parent = parent.DeclaringType;
                }
            }
            else
            {
                object? data = a.ConstructorArguments[0].Value;
                Throw.DebugAssert( data != null );
                // A single value means "apply to everything in the type", e.g. 1 for Dictionary<string, string>, 2 for Dictionary<string?, string?>?
                if( data is byte b )
                {
                    n = HandleByte( n, b );
                }
                else
                {
                    var arguments = (System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument>)data;
                    Throw.DebugAssert( arguments.Count > 0 );
                    var firstByte = (byte)arguments[0].Value!;
                    // Complex nullability marker.
                    n |= NullabilityTypeKind.NRTFullNullable | NullabilityTypeKind.NRTFullNonNullable;
                    // Quick check.
                    Throw.DebugAssert( firstByte == 0 || (n & NullabilityTypeKind.IsValueType) == 0 );
                    // Apply the first byte for this information.
                    if( firstByte == 1 ) n &= ~NullabilityTypeKind.IsNullable;

                    profile = new byte[arguments.Count - 1];
                    for( int i = 0; i < profile.Length; ++i )
                    {
                        profile[i] = (byte)arguments[i + 1].Value!;
                    }
                    Throw.DebugAssert( "Mono byte annotation is invalid.", profile.Length != 0 );
                }
            }
        }
        return new NullabilityTypeInfo( n, profile, fromContext );

        static NullabilityTypeKind HandleByte( NullabilityTypeKind n, byte b )
        {
            if( b == 1 )
            {
                n &= ~NullabilityTypeKind.IsNullable;
                n |= NullabilityTypeKind.NRTFullNonNullable;
            }
            else if( b == 2 )
            {
                n |= NullabilityTypeKind.NRTFullNullable;
            }
            return n;
        }
    }

}

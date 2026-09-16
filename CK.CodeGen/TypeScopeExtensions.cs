using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace CK.CodeGen;

/// <summary>
/// Provides useful extension methods to <see cref="ITypeScope"/>.
/// </summary>
public static class TypeScopeExtensions
{
    /// <summary>
    /// Creates all (or filtered set) constructors from a type (that should be the base type)
    /// to this type which simply relay the call to the base class.
    /// </summary>
    /// <param name="this">This type scope.</param>
    /// <param name="baseType">The base type.</param>
    /// <param name="accessBuilder">
    /// Optional filter (returning null skips the constructor) and
    /// access protection builder. The default access protection is "public ".
    /// </param>
    /// <returns>This function scopes created.</returns>
    public static List<IFunctionScope> CreatePassThroughConstructors( this ITypeScope @this, Type baseType, Func<ConstructorInfo, string?>? accessBuilder = null )
    {
        Throw.CheckNotNullArgument( @this );
        Throw.CheckNotNullArgument( baseType );
        List<IFunctionScope> result = new List<IFunctionScope>();
        foreach( var c in baseType.GetConstructors( BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
                                  .Where( c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly ) )
        {
            string? access = "public ";
            if( accessBuilder != null && (access = accessBuilder( c )) == null ) continue;

            IFunctionScope built = @this.CreateFunction( scope =>
            {
                if( access.Length > 0 )
                {
                    scope.Append( access );
                    if( !Char.IsWhiteSpace( access, access.Length - 1 ) ) scope.Space();
                }
                var parameters = c.GetParameters();
                scope.Append( Helper.RemoveGenericParameters( @this.Name ) )
                     .AppendParameters( parameters );

                if( parameters.Length > 0 )
                {
                    scope.Append( " : base( " );
                    bool atLeastOne = false;
                    foreach( var p in parameters )
                    {
                        if( atLeastOne ) scope.Append( ", " );
                        else atLeastOne = true;
                        scope.Append( p.Name );
                    }
                    scope.Append( " )" );
                }
            } );
            result.Add( built );
        }
        return result;
    }

    /// <summary>
    /// Creates an overridden method.
    /// The <paramref name="method"/> must be virtual (not static nor sealed) and not purely internal.
    /// </summary>
    /// <param name="this">This type scope.</param>
    /// <param name="method">The method description.</param>
    /// <returns>The newly created function scope.</returns>
    public static IFunctionScope CreateOverride( this ITypeScope @this, MethodInfo method )
    {
        Throw.CheckNotNullArgument( @this );
        Helper.CheckIsOverridable( method );
        return @this.CreateFunction( h => h.DoAppendSignature( AccessProtectionOption.ThrowOnPureInternal, "override ", method ) );
    }

    /// <summary>
    /// Creates a sealed overridden method.
    /// The <paramref name="method"/> must be virtual (not static nor sealed) and not purely internal.
    /// </summary>
    /// <param name="this">This type scope.</param>
    /// <param name="method">The method description.</param>
    /// <returns>The newly created function scope.</returns>
    public static IFunctionScope CreateSealedOverride( this ITypeScope @this, MethodInfo method )
    {
        Throw.CheckNotNullArgument( @this );
        Helper.CheckIsOverridable( method );
        return @this.CreateFunction( h => h.DoAppendSignature( AccessProtectionOption.ThrowOnPureInternal, "sealed override ", method ) );
    }

}

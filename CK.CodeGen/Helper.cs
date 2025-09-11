using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.CodeGen;

static class Helper
{
    internal static ICodeWriter DoAppendSignature( this ICodeWriter @this,
                                                   AccessProtectionOption protection,
                                                   string frontModifier,
                                                   MethodInfo method )
    {
        Throw.CheckNotNullArgument( method );
        string name = method.Name;
        if( method.ContainsGenericParameters )
        {
            name += '<';
            name += string.Join( ",", method.GetGenericArguments().Select( a => a.Name ) );
            name += '>';
        }
        if( protection != AccessProtectionOption.None ) @this.AppendAccessProtection( method, protection );
        @this.Append( frontModifier )
             .AppendCSharpName( method.ReturnType, true, true, useValueTupleParentheses: true )
             .Space()
             .Append( name )
             .AppendParameters( method.GetParameters() );
        return @this;
    }

    internal static ICodeWriter AppendAccessProtection( this ICodeWriter w, MethodInfo method, AccessProtectionOption p )
    {
        Throw.DebugAssert( p != AccessProtectionOption.None );
        if( p == AccessProtectionOption.ThrowOnPureInternal
            && (method.IsAssembly || method.IsFamilyAndAssembly) )
        {
            throw new ArgumentException( $"Method {method} must not be internal.", nameof( method ) );
        }
        if( method.IsPublic ) w.Append( "public " );
        else if( method.IsFamily ) w.Append( "protected " );
        else if( method.IsAssembly )
        {
            if( p == AccessProtectionOption.All )
            {
                w.Append( "internal " );
            }
        }
        else if( method.IsFamilyAndAssembly )
        {
            if( p == AccessProtectionOption.All )
            {
                w.Append( "private protected " );
            }
            else w.Append( "protected " );
        }
        else if( method.IsFamilyOrAssembly )
        {
            if( p == AccessProtectionOption.All )
            {
                w.Append( "internal protected " );
            }
            else w.Append( "protected " );
        }
        return w;
    }

    internal static ICodeWriter AppendParameters( this ICodeWriter @this, IReadOnlyList<ParameterInfo> parameters )
    {
        if( parameters.Count == 0 ) return @this.Append( "()" );
        @this.Append( "( " );
        bool isFirstParameter = true;
        foreach( var p in parameters )
        {
            if( isFirstParameter ) isFirstParameter = false;
            else @this.Append( ", " );
            @this.AddParameter( p );
        }
        return @this.Append( " )" );
    }

    internal static ICodeWriter AddParameter( this ICodeWriter @this, ParameterInfo p )
    {
        if( p.IsIn ) @this.Append( "in " );
        else if( p.IsOut ) @this.Append( "out " );
        else if( p.ParameterType.IsByRef ) @this.Append( "ref " );
        Type? parameterType = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
        return @this.AppendCSharpName( parameterType, true, true, useValueTupleParentheses: true )
                    .Space()
                    .AppendVariable( p.Name! );
    }

    internal static void CheckIsOverridable( MethodInfo method )
    {
        if( method == null ) throw new ArgumentNullException( nameof( method ) );
        if( !method.IsVirtual || method.IsStatic || method.IsFinal )
            throw new ArgumentException( $"Method {method} is not overridable.", nameof( method ) );
    }

    internal static string RemoveGenericParameters( string typeName )
    {
        int idx = typeName.IndexOf( '<', StringComparison.Ordinal );
        return idx < 0 ? typeName : typeName.Substring( idx );
    }
}

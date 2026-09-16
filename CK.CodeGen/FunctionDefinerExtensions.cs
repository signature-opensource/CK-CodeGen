using CK.Core;
using System;

namespace CK.CodeGen;

/// <summary>
/// Provides extension methods to <see cref="IFunctionDefinerScope"/>.
/// </summary>
public static class FunctionDefinerExtensions
{
    /// <summary>
    /// Creates a <see cref="IFunctionScope"/> inside this scope.
    /// Its name is automatically extracted from the header that may contain the
    /// opening curly brace '{' or not (in such case it is automatically appended).
    /// </summary>
    /// <param name="this">This scope.</param>
    /// <param name="header">The header of the function. Must not be null.</param>
    /// <returns>The new function scope.</returns>
    public static IFunctionScope CreateFunction( this IFunctionDefinerScope @this, string header )
    {
        Throw.CheckNotNullArgument( header );
        return @this.CreateFunction( t => t.Append( header ) );
    }

    /// <summary>
    /// Finds or creates a function or a constructor inside this scope.
    /// There must not be any ': this( ... )' or ': base( ... )' clause or any start
    /// of the function body otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="this">This scope.</param>
    /// <param name="declaration">The header of the function.</param>
    /// <returns>The new or existing function scope.</returns>
    public static IFunctionScope FindOrCreateFunction( this IFunctionDefinerScope @this, string declaration )
    {
        var fDef = FunctionDefinition.Parse( declaration, out string? bodyStart );
        if( fDef.ReturnType == null && fDef.ThisOrBaseConstructorCall != FunctionDefinition.CallConstructor.None )
        {
            throw new ArgumentException( $"Constructor must not specify a ': this( ... )' or ': base( ... )' clause when using FindOrCreateFunction: {declaration}", nameof( declaration ) );
        }
        if( bodyStart != null )
        {
            throw new ArgumentException( $"Function must not specify the start of its body when using FindOrCreateFunction: {declaration}", nameof( declaration ) );
        }
        return @this.FindFunction( fDef.Key, false ) ?? @this.CreateFunction( fDef );
    }

}

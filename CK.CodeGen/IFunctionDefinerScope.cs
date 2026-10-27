using System;
using System.Collections.Generic;

namespace CK.CodeGen;

/// <summary>
/// A function definer scope is <see cref="INamedScope"/> that can create <see cref="IFunctionScope"/>.
/// It is not itself a <see cref="ICodeWriter"/> but its specializations (<see cref="ITypeScope"/> and <see cref="IFunctionScope"/>)
/// are: raw code can be appended to them as needed.
/// </summary>
public interface IFunctionDefinerScope : INamedScope
{
    /// <summary>
    /// Creates a <see cref="IFunctionScope"/> (that can be a constructor) inside
    /// this scope.
    /// Its name is automatically extracted from the header that may contain the
    /// opening curly brace '{' or not (in such case it is automatically appended).
    /// </summary>
    /// <param name="header">Configure the header or more (the body can be generated here). Must not be null.</param>
    /// <returns>The new function scope.</returns>
    IFunctionScope CreateFunction( Action<IFunctionScope> header );

    /// <summary>
    /// Creates a <see cref="IFunctionScope"/> (that can be a constructor) inside
    /// this scope. There must not be an already created function with the same <see cref="FunctionDefinition.Key"/>
    /// otherwise an exception is thrown.
    /// </summary>
    /// <param name="f">The function to define. May be shared among multiple scopes.</param>
    /// <returns>The new function scope.</returns>
    IFunctionScope CreateFunction( FunctionDefinition f );

    /// <summary>
    /// Finds an existing <see cref="IFunctionScope"/> previously created with <see cref="CreateFunction(FunctionDefinition)"/>
    /// from its <see cref="FunctionDefinition.Key"/> and if not found and <paramref name="analyzeHeader"/> is true, considers
    /// that the key is a signature and parses it with <see cref="FunctionDefinition.Parse(string)"/> and tries to find again. 
    /// </summary>
    /// <param name="key">The key of the function to find. See <see cref="FunctionDefinition.Key"/>.</param>
    /// <param name="analyzeHeader">True to parse the <paramref name="key"/> as a header function if key lookup failed.</param>
    /// <returns>The function or null if not found.</returns>
    IFunctionScope? FindFunction( string key, bool analyzeHeader );

    /// <summary>
    /// Gets the functions that this scope contains.
    /// </summary>
    IReadOnlyCollection<IFunctionScope> Functions { get; }

}

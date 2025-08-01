using CK.CodeGen.SimpleParser;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace CK.CodeGen;

/// <summary>
/// Captures a method or constructor definition.
/// The <see cref="Key"/> identifies it.
/// </summary>
public partial class FunctionDefinition
{

    internal FunctionDefinition( AttributeCollection? attributes,
                                 Modifiers modifiers,
                                 ExtendedTypeName? returnType,
                                 TypeName methodName,
                                 CallConstructor thisOrBaseConstructorCall,
                                 string? thisOrBaseConstructorParameters,
                                 bool isIndexer,
                                 IReadOnlyList<Parameter>? parameters,
                                 IReadOnlyList<TypeParameterConstraint>? constraints,
                                 StringBuilder buffer )
    {
        Attributes = attributes ?? new AttributeCollection();
        Modifiers = modifiers;
        ReturnType = returnType;
        MethodName = methodName;
        ThisOrBaseConstructorCall = thisOrBaseConstructorCall;
        ThisOrBaseConstructorParameters = thisOrBaseConstructorParameters;
        IsIndexer = isIndexer;
        Parameters = parameters ?? Array.Empty<Parameter>();
        Constraints = constraints ?? Array.Empty<TypeParameterConstraint>();

        Key = ComputeFunctionKey( methodName, parameters, buffer );
    }

    static string ComputeFunctionKey( TypeName methodName, IReadOnlyList<Parameter>? parameters, StringBuilder buffer )
    {
        buffer.Append( methodName.Name );
        string[] genNames = methodName.GenericParameters.Select( p => p.Type.TypeName!.Name ).ToArray();
        if( genNames.Length > 0 ) buffer.Append( '`' ).Append( genNames.Length );
        bool atLeastOne = false;
        buffer.Append( '(' );
        if( parameters != null && parameters.Count > 0 )
        {
            foreach( var p in parameters )
            {
                if( atLeastOne ) buffer.Append( ',' );
                else atLeastOne = true;
                if( (p.Modifiers & ParameterModifier.Scoped) != 0 )
                {
                    buffer.Append( "scoped" );
                }
                if( (p.Modifiers & (ParameterModifier.In | ParameterModifier.Ref | ParameterModifier.Out)) != 0 )
                {
                    buffer.Append( '&' );
                }
                p.Type.Write( buffer, name => { int i = Array.IndexOf( genNames, name ); return i >= 0 ? "§" + i : name; } );
            }
        }
        buffer.Append( ')' );
        return buffer.ToString();
    }

    /// <summary>
    /// Gets the returned type. This is null if this method is a constructor.
    /// </summary>
    public ExtendedTypeName? ReturnType { get; }

    /// <summary>
    /// Gets a mutable set of attributes.
    /// </summary>
    public AttributeCollection Attributes { get; }

    /// <summary>
    /// Gets or sets the modifiers that applies to this method.
    /// Only a subset of the <see cref="Modifiers"/> are valid.
    /// </summary>
    public Modifiers Modifiers { get; set; }

    /// <summary>
    /// Gets the full method name with its generic parameter defintions if any.
    /// Use the <see cref="TypeName.Name"/> to obtain the naked name of this method.
    /// </summary>
    public TypeName MethodName { get; }

    /// <summary>
    /// Gets the normalized key that identifies this method.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Defines whether a ': base(<see cref="ThisOrBaseConstructorParameters"/>)' or 'this(...)' exists.
    /// </summary>
    public enum CallConstructor
    {
        /// <summary>
        /// The method is not a constructor or a constructor that doesn't call another one or its base.
        /// </summary>
        None,

        /// <summary>
        /// The method is a constructor that calls another one.
        /// </summary>
        This,

        /// <summary>
        /// The method is a constructor that calls a base constructor.
        /// </summary>
        Base
    }

    /// <summary>
    /// Gets whether a <c>: this( ... )'</c> or <c>': base( ... )'</c> call exists. Always <see cref="CallConstructor.None"/>
    /// if this method is not a constructor.
    /// </summary>
    public CallConstructor ThisOrBaseConstructorCall { get; set; }

    /// <summary>
    /// Gets the raw string that is inside the <c>: this( ... )'</c> or <c>': base( ... )'</c> call if it exists.
    /// </summary>
    public string? ThisOrBaseConstructorParameters { get; set; }

    /// <summary>
    /// Gets whether this is an indexer rather than a regular method definition.
    /// </summary>
    public bool IsIndexer { get; }

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>
    /// Gets the generic 'where' constraints if any.
    /// </summary>
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; }

    StringBuilder WriteParameters( StringBuilder b, bool withAttributes, bool withNames, bool withDefaultValues )
    {
        b.Append( '(' );
        if( Parameters.Count > 0 )
        {
            b.Append( ' ' );
            bool already = false;
            foreach( var p in Parameters )
            {
                if( already ) b.Append( ", " );
                else already = true;
                if( withAttributes ) p.Attributes.Write( b );
                switch( p.Modifiers )
                {
                    case ParameterModifier.Out: b.Append( "out " ); break;
                    case ParameterModifier.This: b.Append( "this " ); break;
                    case ParameterModifier.Ref: b.Append( "ref " ); break;
                    case ParameterModifier.Params: b.Append( "params " ); break;
                    case ParameterModifier.In: b.Append( "in " ); break;
                }
                p.Type.Write( b );
                if( withNames ) b.Append( ' ' ).Append( p.Name );
                if( withDefaultValues && !String.IsNullOrEmpty( p.DefaultValue ) )
                {
                    b.Append( " = " ).Append( p.DefaultValue );
                }
            }
            b.Append( ' ' );
        }
        b.Append( ')' );
        return b;
    }

    /// <summary>
    /// Writes this MethodDefinition into the provided StringBuilder.
    /// </summary>
    /// <param name="b">The target.</param>
    /// <returns>The StringBuilder to enable fluent syntax.</returns>
    public StringBuilder Write( StringBuilder b )
    {
        if( b == null ) throw new ArgumentNullException( nameof( b ) );
        if( Attributes.HasAttributes )
        {
            Attributes.Write( b );
            b.AppendLine();
        }
        Modifiers.Write( b );
        if( ReturnType != null )
        {
            ReturnType.Write( b );
            b.Append( ' ' );
        }
        MethodName.Write( b );
        WriteParameters( b, true, true, true );
        if( Constraints.Count > 0 )
        {
            b.Append( ' ' );
            bool already = false;
            foreach( var c in Constraints )
            {
                if( already ) b.Append( ' ' );
                else already = true;
                c.Write( b );
            }
        }
        if( ThisOrBaseConstructorCall != CallConstructor.None )
        {
            b.Append( " : " )
             .Append( ThisOrBaseConstructorCall == CallConstructor.This ? "this" : "base" )
             .Append( '(' )
             .Append( ThisOrBaseConstructorParameters )
             .Append( ')' );
        }
        return b;
    }

    /// <summary>
    /// Tries to parse a method definition.
    /// Note that there must be no start of the function body in the declaration since it is skipped by this overload.
    /// </summary>
    /// <param name="declaration">The string to parse.</param>
    /// <param name="m">The non null method definition on success.</param>
    /// <returns>True on success, false if this cannot be parsed.</returns>
    public static bool TryParse( ReadOnlySpan<char> declaration, [NotNullWhen( true )] out FunctionDefinition? m )
    {
        return declaration.MatchMethodDefinition( out m, out bool _ );
    }

    /// <summary>
    /// Tries to parse a method definition, returning the remaining potential start of the body if any.
    /// </summary>
    /// <param name="declaration">The string to parse.</param>
    /// <param name="fDef">The non null method definition on success.</param>
    /// <param name="bodyStart">
    /// On output, contains the start of the function
    /// body (without opening '{' or with a "=>" lambda token).
    /// </param>
    /// <returns>True on success, false if this cannot be parsed.</returns>
    public static bool TryParse( string declaration, [NotNullWhen( true )] out FunctionDefinition? fDef, out string? bodyStart )
    {
        return DoParse( declaration, out fDef, out bodyStart, false );
    }

    /// <summary>
    /// Parses a method definition or throws if unable to parse.
    /// Note that there must be no start of the function body in the declaration since it is skipped by this overload.
    /// </summary>
    /// <param name="declaration">The string to parse.</param>
    /// <returns>The method definition.</returns>
    public static FunctionDefinition Parse( string declaration )
    {
        DoParse( declaration, out var fDef, out _, true );
        return fDef!;
    }

    /// <summary>
    /// Parses a method definition, returning the remaining potential start of the body if any, or throws if unable to parse.
    /// </summary>
    /// <param name="declaration">The string to parse.</param>
    /// <param name="bodyStart">
    /// On output, contains the start of the function
    /// body (without opening '{' or with a "=>" lambda token).
    /// </param>
    /// <returns>The method definition.</returns>
    public static FunctionDefinition Parse( string declaration, out string? bodyStart )
    {
        DoParse( declaration, out var fDef, out bodyStart, true );
        return fDef!;
    }

    static bool DoParse( ReadOnlySpan<char> declaration, [NotNullWhen( true )] out FunctionDefinition? fDef, out string? bodyStart, bool throwOnError )
    {
        bodyStart = null;
        var m = declaration;
        m.SkipWhiteSpacesAndJSComments();
        if( !m.MatchMethodDefinition( out fDef, out bool hasCodeOpener ) )
        {
            if( throwOnError ) throw new InvalidOperationException( $"Error: Unable to parse function or constructor declaration '{declaration}'." );
            return false;
        }
        Throw.DebugAssert( fDef != null );
        if( hasCodeOpener )
        {
            bodyStart = new string( declaration.Slice( declaration.Length - m.Length ).TrimEnd() );
        }
        return true;
    }
}

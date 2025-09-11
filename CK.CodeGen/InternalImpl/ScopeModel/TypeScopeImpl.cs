using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.CodeGen;

sealed class TypeScopeImpl : TypeDefinerScopeImpl, ITypeScope
{
    readonly FunctionDefiner _funcs;

    [AllowNull]
    TypeDefinition _typeDef;

    internal TypeScopeImpl( CodeWorkspaceImpl ws, INamedScope parent )
        : base( ws, parent )
    {
        UniqueId = ws.GetNextTypeScopeIdentifier();
        _funcs = new FunctionDefiner( true );
        INamedScope? p = parent;
        for(; ; )
        {
            if( p is INamespaceScope ns )
            {
                Namespace = ns;
                break;
            }
            p = p.Parent;
            Throw.DebugAssert( "We eventually reached the root namespace.", p != null );
        }
    }

    public INamespaceScope Namespace { get; }

    public int UniqueId { get; }

    public bool IsNestedType => Parent is ITypeScope;

    internal string TypeKey => _typeDef.Name.Key;

    internal void MergeWith( TypeScopeImpl other )
    {
        Throw.DebugAssert( other != null );
        if( TypeKey != other.TypeKey )
        {
            throw new InvalidOperationException( $"Unable to merge type '{_typeDef}' with '{other._typeDef}'." );
        }
        _typeDef.MergeWith( other._typeDef );
        CodePart.MergeWith( other.CodePart );
        MergeTypes( other );
        _funcs.MergeWith( Workspace, this, other._funcs );
    }

    /// <summary>
    /// Extracts the name.
    /// The declaration itself is updated as one string and the scope opener is injected if needed.
    /// </summary>
    internal void Initialize()
    {
        var b = new SmarterStringBuilder( new StringBuilder() );
        // We store the declaration and clears the code buffer.
        var declaration = CodePart.Build( b ).ToString();
        CodePart.Parts.Clear();
        var m = declaration.AsSpan();
        m.SkipWhiteSpacesAndJSComments();
        if( !m.MatchTypeDefinition( out var typeDef, IsNestedType, out bool hasCodeOpener ) )
        {
            Throw.InvalidOperationException( $"Error: Unable to parse type declaration '{declaration}'." );
        }
        _typeDef = typeDef;
        if( hasCodeOpener )
        {
            CodePart.Parts.Add( declaration.Substring( declaration.Length - m.Length ) );
        }
        SetName( _typeDef.Name.ToString() );
    }

    internal protected override SmarterStringBuilder Build( SmarterStringBuilder b, bool closeScope )
    {
        if( _typeDef != null )
        {
            if( b.Builder != null ) _typeDef.Write( b.Builder );
            else b.Append( _typeDef.ToString() );
            b.HasNewLine = false;
        }
        b.AppendLine().Append( "{" ).AppendLine();
        CodePart.Build( b );
        _funcs.Build( b );
        BuildTypes( b );
        if( closeScope ) b.AppendLine().Append( "}" ).AppendLine();
        return b;
    }

    public IFunctionScope CreateFunction( Action<IFunctionScope> header ) => _funcs.Create( Workspace, this, header );

    public IFunctionScope CreateFunction( FunctionDefinition def ) => _funcs.Create( Workspace, this, def );

    public TypeDefinition Definition => _typeDef;

    public IReadOnlyCollection<IFunctionScope> Functions => _funcs.Functions;

    public ITypeScopePart CreatePart( bool top )
    {
        var p = new Part( this );
        if( top ) CodePart.Parts.Insert( 0, p );
        else CodePart.Parts.Add( p );
        return p;
    }

    public IFunctionScope? FindFunction( string key, bool analyzeHeader ) => _funcs.FindFunction( key, analyzeHeader );

    sealed class Part : TypeDefinerPart, ITypeScopePart
    {
        public Part( ITypeScope owner )
            : base( owner )
        {
        }

        public new ITypeScope PartOwner => (ITypeScope)base.PartOwner;

        public INamespaceScope Namespace => PartOwner.Namespace;

        public bool IsNestedType => PartOwner.IsNestedType;

        public int UniqueId => PartOwner.UniqueId;

        public TypeDefinition Definition => PartOwner.Definition;

        public IReadOnlyCollection<IFunctionScope> Functions => PartOwner.Functions;

        public IFunctionScope CreateFunction( Action<IFunctionScope> header ) => PartOwner.CreateFunction( header );

        public IFunctionScope CreateFunction( FunctionDefinition def ) => PartOwner.CreateFunction( def );

        ICodePart ICodePartFactory.CreatePart( bool top ) => CreatePart( top );

        public ITypeScopePart CreatePart( bool top )
        {
            var p = new Part( this );
            if( top ) Parts.Insert( 0, p );
            else Parts.Add( p );
            return p;
        }

        public IFunctionScope? FindFunction( string key, bool analyzeHeader ) => PartOwner.FindFunction( key, analyzeHeader );

    }

}

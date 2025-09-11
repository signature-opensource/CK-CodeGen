using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace CK.CodeGen;

sealed class NamespaceScopeImpl : TypeDefinerScopeImpl, INamespaceScope
{
    readonly static Regex _nsName = new Regex( @"^\s*(?<1>\w+)(\s*\.\s*(?<1>\w+))*\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture );
    readonly Dictionary<string, KeyValuePair<string?, string?>> _usings;
    readonly List<NamespaceScopeImpl> _subNamespaces;
    readonly CodePartRaw _beforeNamespace;

    internal NamespaceScopeImpl( CodeWorkspaceImpl ws, INamespaceScope? parent, string name )
        : base( ws, parent )
    {
        Throw.DebugAssert( (parent == null) == (name.Length == 0) );
        Throw.DebugAssert( parent == null || parent is NamespaceScopeImpl );
        _usings = new Dictionary<string, KeyValuePair<string?, string?>>();
        _subNamespaces = new List<NamespaceScopeImpl>();
        _beforeNamespace = new CodePartRaw();
        if( parent != null ) SetName( name );
    }

    internal void MergeWith( NamespaceScopeImpl other )
    {
        Throw.DebugAssert( other != null );
        _beforeNamespace.MergeWith( other._beforeNamespace );
        foreach( var u in other._usings )
        {
            DoEnsureUsing( u.Key, u.Value.Key, u.Value.Value );
        }
        CodePart.MergeWith( other.CodePart );
        MergeTypes( other );
        foreach( var oNS in other._subNamespaces )
        {
            var my = _subNamespaces.FirstOrDefault( x => x.Name == oNS.Name );
            if( my == null )
            {
                my = new NamespaceScopeImpl( Workspace, this, oNS.Name );
                _subNamespaces.Add( my );
            }
            my.MergeWith( oNS );
        }
    }

    INamespaceScope? INamespaceScope.Parent => Parent;

    internal new NamespaceScopeImpl? Parent => (NamespaceScopeImpl?)base.Parent;

    public ICodePart BeforeNamespace => _beforeNamespace;

    public INamespaceScope EnsureUsing( string ns )
    {
        if( !String.IsNullOrWhiteSpace( ns ) )
        {
            int assignIdx = ns.IndexOf( '=', StringComparison.Ordinal );
            if( assignIdx != 0 )
            {
                if( assignIdx > 0 )
                {
                    var def = ns.Substring( assignIdx + 1 );
                    ns = CheckAndNormalizeOneName( ns.Substring( 0, assignIdx ) );
                    return DoEnsureUsing( ns, def );
                }
                return DoEnsureUsing( CheckAndNormalizeNamespace( ns ), null );
            }
        }
        throw new ArgumentException( $"'using {ns}' is invalid.", nameof( ns ) );
    }

    public INamespaceScope EnsureUsingAlias( string alias, string definition )
    {
        if( String.IsNullOrWhiteSpace( definition ) ) throw new ArgumentException( $"'{definition}' is not a valid alias definition.", nameof( definition ) );
        return DoEnsureUsing( CheckAndNormalizeOneName( alias ), definition );
    }

    INamespaceScope DoEnsureUsing( string alias, string? definition )
    {
        Throw.DebugAssert( (definition == null && CheckAndNormalizeNamespace( alias ) == alias)
                            || (definition != null && CheckAndNormalizeOneName( alias ) == alias) );
        var keyDef = definition;
        if( keyDef != null )
        {
            Throw.DebugAssert( "Obviously...", definition != null );
            // We must normalize the trailing ;.
            definition = definition.Trim();
            if( definition.Length == 0 || definition == ";" ) throw new ArgumentException( $"'{definition}' is not a valid alias definition.", nameof( definition ) );
            if( definition[definition.Length - 1] != ',' ) definition += ';';

            keyDef = RemoveWhiteSpaces( definition );
        }
        return DoEnsureUsing( alias, keyDef, definition );
    }

    INamespaceScope DoEnsureUsing( string alias, string? keyDef, string? definition )
    {
        Throw.DebugAssert( alias != null );
        Throw.DebugAssert( (keyDef == null) == (definition == null) );
        if( _usings.TryGetValue( alias, out var defs ) )
        {
            if( defs.Key == keyDef ) return this;
            string existing = defs.Value != null ? " = " + defs.Value : ";";
            string newOne = definition != null ? " = " + definition : ";";
            throw new ArgumentException( $"using {alias}{newOne} is already defined in this scope as: {alias}{existing}." );
        }
        NamespaceScopeImpl? scope = this;
        while( (scope = scope.Parent) != null )
        {
            if( scope._usings.TryGetValue( alias, out defs ) && defs.Key == keyDef ) return this;
        }
        _usings.Add( alias, new KeyValuePair<string?, string?>( keyDef, definition ) );
        return this;
    }

    static public string[] CheckAndGetNamespaceParts( string ns )
    {
        if( ns == null ) throw new ArgumentNullException( ns );
        var m = _nsName.Match( ns );
        if( !m.Success ) throw new ArgumentException( $"Invalid namespace: {ns}" );
        var captures = m.Groups[1].Captures;
        var result = new string[captures.Count];
        for( int i = 0; i < result.Length; ++i ) result[i] = captures[i].Value;
        return result;
    }

    static string CheckAndNormalizeNamespace( string ns )
    {
        return String.Join( ".", CheckAndGetNamespaceParts( ns ) );
    }

    static public string CheckAndNormalizeOneName( string ns )
    {
        var n = CheckAndGetNamespaceParts( ns );
        if( n.Length != 1 ) throw new ArgumentException( $"Only one identifier is expected: {ns}." );
        return n[0];
    }

    public INamespaceScope FindOrCreateNamespace( string ns )
    {
        string[] names = CheckAndGetNamespaceParts( ns );
        return DoFindOrCreateNamespace( names, 0 );
    }

    INamespaceScope DoFindOrCreateNamespace( string[] names, int idx )
    {
        var exist = _subNamespaces.FirstOrDefault( x => x.Name == names[idx] );
        if( exist == null )
        {
            exist = new NamespaceScopeImpl( Workspace, this, names[idx] );
            _subNamespaces.Add( exist );
            Workspace.OnNamespaceCreated( exist );
        }
        return names.Length == ++idx ? exist : exist.DoFindOrCreateNamespace( names, idx );
    }

    public IReadOnlyCollection<INamespaceScope> Namespaces => _subNamespaces;

    internal protected override SmarterStringBuilder Build( SmarterStringBuilder b, bool closeScope )
    {
        _beforeNamespace.Build( b );
        // A global using prevent any attribute definition (and [assembly::...] attributes cannot be defined
        // in a namespace.
        // We write the global usings in the TOP namespaces: they are duplicated among the top-level namespaces
        // but this enables to define attributes at the top of one file.
        if( Workspace.Global != this )
        {
            b.Append( "namespace " )
            .Append( Name )
            .AppendLine()
            .Append( "{" )
            .AppendLine();
            if( Parent == Workspace.Global )
            {
                Parent.WriteThisUsings( b );
            }
            WriteThisUsings( b );
        }
        else if( _subNamespaces.Count == 0 )
        {
            // However, if there is no namespace defined, the global
            // usings must be written. We have no choice.
            WriteThisUsings( b );
        }
        CodePart.Build( b );
        foreach( var ns in _subNamespaces )
        {
            ns.Build( b, true );
        }
        BuildTypes( b );
        if( Workspace.Global != this && closeScope ) b.AppendLine().Append( "}" );
        return b;
    }

    private void WriteThisUsings( SmarterStringBuilder b )
    {
        foreach( var e in _usings )
        {
            b.AppendLine().Append( "using " ).Append( e.Key );
            if( e.Value.Value == null ) b.Append( ";" );
            else b.Append( " = " ).Append( e.Value.Value );
            b.AppendLine();
        }
    }

    ICodePart ICodePartFactory.CreatePart( bool top ) => CreatePart( top );

    public INamespaceScopePart CreatePart( bool top )
    {
        var p = new Part( this );
        if( top ) CodePart.Parts.Insert( 0, p );
        else CodePart.Parts.Add( p );
        return p;
    }

    sealed class Part : TypeDefinerPart, INamespaceScopePart
    {
        public Part( INamespaceScope owner )
            : base( owner )
        {
        }

        public new INamespaceScope PartOwner => (INamespaceScope)base.PartOwner;

        public INamespaceScope? Parent => PartOwner.Parent;

        public IReadOnlyCollection<INamespaceScope> Namespaces => PartOwner.Namespaces;

        public ICodePart BeforeNamespace => PartOwner.BeforeNamespace;

        ICodePart ICodePartFactory.CreatePart( bool top ) => CreatePart( top );

        public INamespaceScopePart CreatePart( bool top )
        {
            var p = new Part( this );
            if( top ) Parts.Insert( 0, p );
            else Parts.Add( p );
            return p;
        }

        public INamespaceScope EnsureUsing( string ns ) => PartOwner.EnsureUsing( ns );

        public INamespaceScope EnsureUsingAlias( string alias, string definition ) => PartOwner.EnsureUsingAlias( alias, definition );

        public INamespaceScope FindOrCreateNamespace( string ns ) => PartOwner.FindOrCreateNamespace( ns );

    }

}

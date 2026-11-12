using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace CK.CodeGen;

/// <summary>
/// Reads the Xml documentation file of an assembly (the ".xml" file next to it) and finds the documentation
/// element of a member by its documentation name ("T:", "M:", "P:", "F:" or "E:" prefixed).
/// </summary>
public static class XmlDocumentationReader
{
    /// <summary>
    /// Loads the Xml file documentation for an assembly.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="a">The assembly to document.</param>
    /// <param name="cache">Optional cache to avoid reloading the same file.</param>
    /// <returns>The Xml documentation if found.</returns>
    public static XDocument? GetXmlDocumentation( IActivityMonitor monitor, Assembly a, IDictionary<object, object?>? cache = null )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( a );

        XDocument? xDoc = null;
        var keyCache = a;
        if( cache != null && cache.TryGetValue( keyCache, out var oDoc ) )
        {
            if( oDoc == null ) return null;
            xDoc = (XDocument)oDoc;
        }
        else
        {
            var path = System.IO.Path.ChangeExtension( a.Location, ".xml" );
            if( !System.IO.File.Exists( path ) )
            {
                monitor.Warn( $"Missing Xml documentation file '{path}' for assembly '{a.FullName}'." );
                cache?.Add( keyCache, null );
            }
            else
            {
                try
                {
                    xDoc = XDocument.Load( path );
                    cache?.Add( keyCache, xDoc );
                }
                catch( Exception ex )
                {
                    monitor.Warn( $"Unable to load Xml documentation file '{path}' for assembly '{a.FullName}'.", ex );
                    cache?.Add( keyCache, null );
                }
            }
        }
        return xDoc;
    }

    /// <summary>
    /// Attempts to get the documentation element for a member (type, property, field, method, etc.) from the Xml
    /// documentation file of its assembly.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="member">The member for which the documentation must be obtained.</param>
    /// <param name="cache">Optional cache to avoid reloading the same file.</param>
    /// <returns>The element if found.</returns>
    public static XElement? GetDocumentationFor( IActivityMonitor monitor, MemberInfo member, IDictionary<object, object?>? cache = null )
    {
        Throw.CheckNotNullArgument( member );
        var xDoc = GetXmlDocumentation( monitor, member.Module.Assembly, cache );
        if( xDoc == null ) return null;
        return GetDocumentationElement( xDoc, GetNameAttributeValueFor( member ) );
    }

    /// <summary>
    /// Attempts to get documentation elements for multiple members at once.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="members">The members for which the documentation must be obtained.</param>
    /// <param name="cache">Optional cache to avoid reloading the same file.</param>
    /// <returns>The set of available elements.</returns>
    public static IEnumerable<XElement> GetDocumentationFor( IActivityMonitor monitor, IEnumerable<MemberInfo> members, IDictionary<object, object?>? cache = null )
    {
        Throw.CheckNotNullArgument( members );
        return members.Select( m => (XDoc: GetXmlDocumentation( monitor, m.Module.Assembly, cache ), M: m) )
                      .Where( m => m.XDoc != null )
                      .Select( m => GetDocumentationElement( m.XDoc!, GetNameAttributeValueFor( m.M ) ) )
                      .Where( e => e != null )!;
    }

    /// <summary>
    /// Encapsulates the <c>/doc/members/member[@name='{0}']</c> lookup in the Xml documentation file.
    /// </summary>
    /// <param name="xDoc">The Xml documentation file.</param>
    /// <param name="nameAttribute">The member name to lookup.</param>
    /// <returns>The element if found.</returns>
    public static XElement? GetDocumentationElement( XDocument xDoc, string nameAttribute )
    {
        Throw.CheckNotNullArgument( xDoc );
        return xDoc.Root?.Elements( "members" ).Elements( "member" ).FirstOrDefault( e => e.Attribute( "name" )?.Value == nameAttribute );
    }

    /// <summary>
    /// Gets the value of the documentation name attribute for a member (type, property, field, method, etc.).
    /// </summary>
    /// <param name="member">The member for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( MemberInfo member ) => member switch
    {
        Type t => GetNameAttributeValueFor( t ),
        MethodBase m => GetNameAttributeValueFor( m ),
        PropertyInfo p => GetNameAttributeValueFor( p ),
        FieldInfo f => GetNameAttributeValueFor( f ),
        EventInfo e => GetNameAttributeValueFor( e ),
        _ => Throw.NotSupportedException<string>()
    };


    /// <summary>
    /// Gets the value of the documentation name attribute for a type.
    /// </summary>
    /// <param name="type">The type for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( Type type )
    {
        Throw.CheckNotNullArgument( type );
        return WriteTypeName( new StringBuilder( "T:" ), type, false ).ToString();
    }

    /// <summary>
    /// Gets the value of the documentation name attribute for a method.
    /// This applies to <see cref="MethodInfo"/> and <see cref="ConstructorInfo"/>.
    /// </summary>
    /// <param name="method">The method for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( MethodBase method )
    {
        Throw.CheckNotNullArgument( method );
        var b = new StringBuilder( "M:" );
        WriteTypeName( b, method.DeclaringType!, false )
            .Append( '.' )
            .Append( method.IsConstructor ? "#ctor" : method.Name );
        if( method.ContainsGenericParameters )
        {
            b.Append( "``" ).Append( method.GetGenericArguments().Length );
        }
        var parameters = method.GetParameters();
        if( parameters.Length > 0 )
        {
            b.Append( '(' );
            WriteParameterTypes( b, parameters.Select( p => p.ParameterType ) );
            b.Append( ')' );
        }
        return b.ToString();
    }

    /// <summary>
    /// Gets the value of the documentation name attribute for a property.
    /// </summary>
    /// <param name="prop">The property for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( PropertyInfo prop ) => GetNameAttributeValueFor( "P:", prop );

    /// <summary>
    /// Gets the value of the documentation name attribute for a field.
    /// </summary>
    /// <param name="field">The field for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( FieldInfo field ) => GetNameAttributeValueFor( "F:", field );

    /// <summary>
    /// Gets the value of the documentation name attribute for an event.
    /// </summary>
    /// <param name="ev">The event for which a documentation name must be computed.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( EventInfo ev ) => GetNameAttributeValueFor( "E:", ev );

    /// <summary>
    /// Basic documentation name builder that applies to <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>
    /// and <see cref="EventInfo"/> but also <see cref="Type"/> but not for <see cref="ConstructorInfo"/>.
    /// </summary>
    /// <param name="linkType">Prefix of the name.</param>
    /// <param name="member">The member for which a documentation name must be obtained.</param>
    /// <param name="memberName">Optional trailing member name. When not null it will be appended after a '.'.</param>
    /// <returns>The attribute value to use.</returns>
    public static string GetNameAttributeValueFor( string linkType, MemberInfo member, string? memberName = null )
    {
        Throw.CheckNotNullArgument( member );
        Throw.CheckArgument( member is not ConstructorInfo );
        StringBuilder b;
        if( member is Type t )
        {
            b = WriteTypeName( new StringBuilder( linkType ), t, false );
        }
        else
        {
            Debug.Assert( member is PropertyInfo || member is FieldInfo || member is EventInfo );
            b = WriteTypeName( new StringBuilder( linkType ), member.DeclaringType!, false )
                    .Append( '.' )
                    .Append( member.Name );
        }
        if( memberName != null )
        {
            b.Append( '.' ).Append( memberName );
        }
        return b.ToString();
    }
    static StringBuilder WriteTypeName( StringBuilder b, Type t, bool withGenericParamters )
    {
        if( t.IsArray )
        {
            return WriteTypeName( b, t.GetElementType()!, withGenericParamters ).Append( "[]" );
        }
        if( !string.IsNullOrEmpty( t.Namespace ) )
        {
            b.Append( t.Namespace ).Append( '.' );
        }
        return WriteTypeNameWithDeclaringNames( b, t, withGenericParamters );
    }

    static StringBuilder WriteTypeNameWithDeclaringNames( StringBuilder b, Type t, bool expandGenericArgs )
    {
        if( t.DeclaringType != null ) WriteTypeNameWithDeclaringNames( b, t.DeclaringType, true ).Append( '.' );
        if( t.IsConstructedGenericType && expandGenericArgs )
        {
            b.Append( t.Name, 0, t.Name.IndexOf( '`', StringComparison.Ordinal ) ).Append( '{' );
            WriteParameterTypes( b, t.GetGenericArguments() );
            return b.Append( '}' );
        }
        return b.Append( t.Name );
    }

    static void WriteParameterTypes( StringBuilder b, IEnumerable<Type> args )
    {
        bool atLeastOne = false;
        foreach( var arg in args )
        {
            if( atLeastOne ) b.Append( ',' ); else atLeastOne = true;
            if( arg.IsGenericTypeParameter )
            {
                b.Append( '`' ).Append( arg.GenericParameterPosition );
            }
            else if( arg.IsGenericMethodParameter )
            {
                b.Append( "``" ).Append( arg.GenericParameterPosition );
            }
            else
            {
                Throw.DebugAssert( "Generic parameters can only be Type XOR Method.", arg.IsGenericParameter == false );
                WriteTypeName( b, arg, true );
            }
        }
    }
}

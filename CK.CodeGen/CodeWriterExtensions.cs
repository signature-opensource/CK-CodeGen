using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.CodeGen;

/// <summary>
/// Provides Append fluent extension methods to <see cref="ICodeWriter"/> specializations.
/// </summary>
public static class CodeWriterExtensions
{
    /// <summary>
    /// Gets a type alias for basic types or null if no alias exists.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>The alias or null.</returns>
    static public string? GetTypeAlias( Type t ) => Core.TypeExtensions.TypeAliases.GetValueOrDefault( t );

    /// <summary>
    /// Appends raw C# code only once: the code itself is used as a key in <see cref="INamedScope.Memory"/> to
    /// avoid adding it twice.
    /// </summary>
    /// <typeparam name="T">Must be both a <see cref="ICodeWriter"/> and <see cref="INamedScope"/>.</typeparam>
    /// <param name="this">This named scope and code writer.</param>
    /// <param name="code">Raw code to append. Must not be null, empty or white space.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendOnce<T>( this T @this, string code ) where T : ICodeWriter, INamedScope
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( code );
        if( !@this.Memory.ContainsKey( code ) )
        {
            @this.Append( code );
            @this.Memory.Add( code, null );
        }
        return @this;
    }

    /// <summary>
    /// Appends a variable name that is prefixed with the @ sign if it's a <see cref="ReservedKeyword"/>.
    /// </summary>
    /// <typeparam name="T">The <see cref="ICodeWriter"/>.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="name">The variable name. Must not be null, empty or white space.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendVariable<T>( this T @this, string name ) where T : ICodeWriter
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( name );
        if( ReservedKeyword.IsReservedKeyword( name ) )
        {
            @this.Append( "@" );
        }
        Append( @this, name );
        return @this;
    }

    /// <summary>
    /// Appends raw C# code.
    /// This is the most basic Append method to use.
    /// Use <see cref="AppendSourceString{T}(T, string)"/> to append the source string representation.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="code">Raw code to append.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, string? code ) where T : ICodeWriter
    {
        @this.DoAdd( code );
        return @this;
    }

    /// <summary>
    /// Appends raw character.
    /// Use <see cref="AppendSourceChar{T}(T, char)"/> to append the source string representation
    /// of the character.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="c">Char to append.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, char c ) where T : ICodeWriter
    {
        @this.DoAdd( c.ToString() );
        return @this;
    }

    /// <summary>
    /// Appends a white space.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Space<T>( this T @this ) where T : ICodeWriter => @this.Append( " " );

    /// <summary>
    /// Appends a <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T NewLine<T>( this T @this ) where T : ICodeWriter => @this.Append( Environment.NewLine );

    /// <summary>
    /// Appends a "{" on a new independent line.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T OpenBlock<T>( this T @this ) where T : ICodeWriter => @this.Append( Environment.NewLine ).Append( '{' ).NewLine();

    /// <summary>
    /// Appends a "}" on a new independent line.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T CloseBlock<T>( this T @this ) where T : ICodeWriter => @this.Append( Environment.NewLine ).Append( '}' ).NewLine();

    /// <summary>
    /// Appends the C# type full name prefixed by <c>global::</c>.
    /// See <see cref="TypeExtensions.ToGlobalTypeName(Type?, bool)"/>.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="t">The type to append.</param>
    /// <param name="typeDeclaration">True to include generic parameter names in the output.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T AppendGlobalTypeName<T>( this T @this, Type? t, bool typeDeclaration = true ) where T : ICodeWriter
    {
        return @this.Append( t.ToGlobalTypeName( typeDeclaration ) );
    }

    /// <summary>
    /// Appends the C# type name. Handles generic definition (either opened or closed, ByRef and pointers).
    /// The <paramref name="typeDeclaration"/> parameters applies to open generics:
    /// When true (the default), typeof( Dictionary&lt;,&gt;.KeyCollection )
    /// will append "System.Collections.Generic.Dictionary&lt;TKey,TValue&gt;.KeyCollection".
    /// When sets to false, it will append "System.Collections.Generic.Dictionary&lt;,&gt;.KeyCollection".
    /// </summary>
    /// <remarks>
    /// Value tuples are expressed (by default) with the (lovely,brackets) but can use the explicit type: <see cref="ValueTuple{T1, T2}"/>
    /// instead. This is mainly because of pattern matching limitations in (at least) C# 8 (i.e. netcoreapp3.1).
    /// </remarks>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="t">The type to append.</param>
    /// <param name="typeDeclaration">True to include generic parameter names in the output.</param>
    /// <param name="useValueTupleParentheses">False to use the (safer) "System.ValueTuple&lt;&gt;" instead of the (tuple, with, parentheses, syntax).</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T AppendCSharpName<T>( this T @this, Type? t, bool withNamespaces, bool typeDeclaration, bool useValueTupleParentheses ) where T : ICodeWriter
    {
        return @this.Append( t.ToCSharpName( withNamespaces, typeDeclaration, useValueTupleParentheses ) );
    }

    /// <summary>
    /// Appends "typeof(<see cref="AppendCSharpName"/>)" with the type name in is non declaration form:
    /// for the open generic dictionary this is "typeof(System.Collections.Generic.Dictionary&lt;,&gt;)".
    /// When <paramref name="t"/> is null, <c>null</c> is appended.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="t">The type to append with typeof operator. When null, "null" will be appended.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T AppendTypeOf<T>( this T @this, Type? t ) where T : ICodeWriter
    {
        // typeof handles the (tuple, with, parentheses, syntax).
        return t == null
                ? @this.Append( "null" )
                : @this.Append( "typeof(" ).AppendGlobalTypeName( t, typeDeclaration: false ).Append( ")" );
    }

    /// <summary>
    /// Appends a call to <see cref="Assembly.Load(string)"/> with the <see cref="Assembly.FullName"/>.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="a">The assembly.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, Assembly? a ) where T : ICodeWriter
    {
        return a == null
                ? @this.Append( "null" )
                : @this.Append( "System.Reflection.Assembly.Load(" ).AppendSourceString( a.FullName ).Append( ")" );
    }

    /// <summary>
    /// Appends what is needed to recover a <see cref="MemberInfo"/>: this applies to
    /// <see cref="Type"/>, <see cref="MethodInfo"/>, <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="EventInfo"/>
    /// and <see cref="ConstructorInfo"/>.
    /// Current implementation rely on <see cref="AppendTypeOf{T}(T, Type)"/> on the type (either the <paramref name="m"/> parameter
    /// or the <see cref="MemberInfo.DeclaringType"/>): the type must be accessible from where it is used.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="m">The member.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, MemberInfo? m ) where T : ICodeWriter
    {
        switch( m )
        {
            case null: @this.Append( "null" ); return @this;
            case Type t: @this.AppendTypeOf( t ); return @this;
            case MethodInfo method: @this.Append( "(System.Reflection.MethodInfo)" ); break;
            case PropertyInfo prop: @this.Append( "(System.Reflection.PropertyInfo)" ); break;
            case FieldInfo field: @this.Append( "(System.Reflection.FieldInfo)" ); break;
            case EventInfo ev: @this.Append( "(System.Reflection.EventInfo)" ); break;
            case ConstructorInfo c: @this.Append( "(System.Reflection.ConstructorInfo)" ); break;
        }
        return @this.AppendTypeOf( m.DeclaringType! )
                    .Append( ".GetMembers( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly )[" )
                    .Append( Array.IndexOf( m.DeclaringType!.GetMembers( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly ), m ) )
                    .Append( "]" );
    }

    /// <summary>
    /// Appends either "true" or "false".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="b">The boolean value.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, bool b ) where T : ICodeWriter => @this.Append( b ? "true" : "false" );

    /// <summary>
    /// Appends the source representation of an integer value.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The integer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, int i ) where T : ICodeWriter
    {
        return @this.Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a long value (0 is appended as "0L").
    /// </summary>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The long.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, long i ) where T : ICodeWriter
    {
        return @this.Append( i.ToString( CultureInfo.InvariantCulture ) ).Append( "L" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="UInt64"/> value (0 is appended as "(ulong)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The unsigned long.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, ulong i ) where T : ICodeWriter
    {
        return @this.Append( "(ulong)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="Int16"/> value (0 is appended as "(short)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The short integer..</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, short i ) where T : ICodeWriter
    {
        return @this.Append( "(short)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="UInt16"/> value (0 is appended as "(ushort)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The unsigned short integer..</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, ushort i ) where T : ICodeWriter
    {
        return @this.Append( "(ushort)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="SByte"/> value (0 is appended as "(sbyte)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The signed byte.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, sbyte i ) where T : ICodeWriter
    {
        return @this.Append( "(sbyte)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }
    /// <summary>
    /// Appends the source representation of a <see cref="Byte"/> value (0 is appended as "(byte)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The byte.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, byte i ) where T : ICodeWriter
    {
        return @this.Append( "(byte)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="UInt32"/> value (0 is appended as "(uint)0").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="i">The unsigned integer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, uint i ) where T : ICodeWriter
    {
        return @this.Append( "(uint)" ).Append( i.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="Guid"/> value: "new Guid(...)".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="g">The Guid.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, Guid g ) where T : ICodeWriter
    {
        return @this.Append( "new System.Guid(\"" ).Append( g.ToString() ).Append( "\")" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="Double"/> value.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="d">The double.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, double d ) where T : ICodeWriter
    {
        return @this.Append( d.ToString( CultureInfo.InvariantCulture ) );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="Single"/> value.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="f">The float.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, float f ) where T : ICodeWriter
    {
        return @this.Append( f.ToString( CultureInfo.InvariantCulture ) ).Append( "f" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="Decimal"/> value (0 is appended as "0m").
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="d">The decimal.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, decimal d ) where T : ICodeWriter
    {
        return @this.Append( d.ToString( CultureInfo.InvariantCulture ) ).Append( "m" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="DateTime"/> value: "new DateTime( ... )".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="d">The datetime.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, DateTime d ) where T : ICodeWriter
    {
        return @this.Append( "new System.DateTime(" ).Append( d.Ticks ).Append( ", System.DateTimeKind." ).Append( d.Kind.ToString() ).Append( ")" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="TimeSpan"/> value: "new TimeSpan( ... )".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="ts">The time span.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, TimeSpan ts ) where T : ICodeWriter
    {
        return @this.Append( "new System.TimeSpan(" ).Append( ts.Ticks.ToString( CultureInfo.InvariantCulture ) ).Append( ")" );
    }

    /// <summary>
    /// Appends the source representation of a <see cref="DateTimeOffset"/> value: "new DateTimeOffset( ... )".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="to">The date time with offset.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, DateTimeOffset to ) where T : ICodeWriter
    {
        return @this.Append( "new System.DateTimeOffset(" )
                    .Append( to.Ticks )
                    .Append( ", new System.TimeSpan(" )
                    .Append( to.Offset.Ticks )
                    .Append( "))" );
    }

    /// <summary>
    /// Appends the source representation of a character: "'c'".
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="c">The character.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendSourceChar<T>( this T @this, char c ) where T : ICodeWriter
    {
        switch( c )
        {
            case '\\': return @this.Append( @"'\\'" );
            case '\r': return @this.Append( @"'\r'" );
            case '\n': return @this.Append( @"'\n'" );
            case '\t': return @this.Append( @"'\t'" );
            case '\0': return @this.Append( @"'\0'" );
            case '\b': return @this.Append( @"'\b'" );
            case '\v': return @this.Append( @"'\v'" );
            case '\a': return @this.Append( @"'\a'" );
            case '\f': return @this.Append( @"'\f'" );
        }
        int vC = c;
        if( vC < 32
            || (vC >= 127 && vC <= 160)
            || vC >= 888 )
        {
            return @this.Append( "'\\u" ).Append( vC.ToString( "X4", NumberFormatInfo.InvariantInfo ) ).Append( "'" );
        }
        return @this.Append( "'" ).Append( c.ToString() ).Append( "'" );
    }

    /// <summary>
    /// Appends the source representation of the string.
    /// See <see cref="ExternalTypeExtensions.ToSourceString(string)"/>.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="s">The string. Can be null.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendSourceString<T>( this T @this, string? s ) where T : ICodeWriter
    {
        return @this.Append( s.ToSourceString() );
    }

    /// <summary>
    /// Appends multiple string (raw C# code) at once, separated with a comma by default.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="strings">The string. Can be null or empty.</param>
    /// <param name="separator">Separator between the strings.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, IEnumerable<string> strings, string? separator = ", " ) where T : ICodeWriter
    {
        if( strings != null )
        {
            if( String.IsNullOrEmpty( separator ) ) separator = null;
            bool already = false;
            foreach( var s in strings )
            {
                if( already )
                {
                    if( separator != null ) @this.Append( separator );
                }
                else already = true;
                Append( @this, s );
            }
        }
        return @this;
    }

    /// <summary>
    /// Appends the code of a collection of objects of a given type <typeparamref name="T"/>.
    /// The code is either "null", <see cref="Array.Empty{T}()"/> or an actual new array
    /// with the items appended with <see cref="Append{T}(T, object?, bool)"/>: only
    /// basic types are supported.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <typeparam name="TItem">The items type.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="e">Set of items for which code must be generated. Can be null.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendArray<T, TItem>( this T @this, IEnumerable<TItem>? e ) where T : ICodeWriter
    {
        if( e == null ) return @this.Append( "null" );
        var enumerator = e.GetEnumerator(); 
        if( !enumerator.MoveNext() ) return @this.Append( "System.Array.Empty<" ).AppendGlobalTypeName( typeof( TItem ), false ).Append( ">()" );
        @this.Append( "new " ).AppendGlobalTypeName( typeof( TItem ), false ).Append( "[]{" );
        bool already = false;
        do
        {
            if( already ) @this.Append( "," );
            else already = true;
            Append( @this, enumerator.Current );
        }
        while( enumerator.MoveNext() );
        return @this.Append( "}" );
    }

    /// <summary>
    /// Appends the code of a collection of objects.
    /// If the actual set is a <see cref="IEnumerable{T}"/>, the actual type is extracted
    /// otherwise the type of the items is considered as being object.
    /// The code is either "null", <see cref="Array.Empty{T}()"/> or an actual new array
    /// with the items appended with <see cref="Append{T}(T, object?, bool)"/>: only
    /// basic types are supported.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="e">Set of items for which code must be generated. Can be null.</param>
    /// <param name="useValueTupleParentheses">False to use the (safer) "System.ValueTuple&lt;&gt;" instead of the (tuple, with, parentheses, syntax).</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T AppendArray<T>( this T @this, IEnumerable? e, bool useValueTupleParentheses = true ) where T : ICodeWriter
    {
        if( e == null ) return @this.Append( "null" );
        Type type = typeof( object );
        var eI = e.GetType()
                    .GetInterfaces()
                    .FirstOrDefault( iT => iT.IsGenericType && iT.GetGenericTypeDefinition() == typeof( IEnumerable<> ) );
        if( eI != null )
        {
            type = eI.GetGenericArguments()[0];
        }
        var i = e.GetEnumerator();
        bool any = i.MoveNext();
        (i as IDisposable)?.Dispose();
        if( any )
        {
            @this.Append( "new " ).AppendGlobalTypeName( type, false ).Append( "[]{" );
            bool existing = false;
            foreach( var x in e )
            {
                if( existing ) @this.Append( "," );
                else existing = true;
                Append( @this, x );
            }
            return @this.Append( "}" );
        }
        return @this.Append( "Array.Empty<" ).AppendCSharpName( type, true, false, useValueTupleParentheses ).Append( ">()" );
    }

    /// <summary>
    /// Appends the code source of an enumeration value.
    /// The value is written as its integral type value casted into the enum type.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <typeparam name="TEnum">Type of the <see cref="Enum"/>.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="o">The enum value.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T, TEnum>( this T @this, TEnum o ) where T : ICodeWriter where TEnum : Enum => AppendEnumValue( @this, typeof( TEnum ), o );

    static T AppendEnumValue<T>( T @this, Type t, object o ) where T : ICodeWriter
    {
        @this.Append( "((" ).Append( t.FullName! ).Append( ')' );
        char tU = Enum.GetUnderlyingType( t ).Name[0];
        if( tU == 'U' || tU == 'B' )
        {
            // An enum based on byte (enum EByte : byte) or any other unsigned integral type shorter than a ulong
            // cannot be cast into a ulong... We must use Convert that handles this correctly.
            @this.Append( Convert.ToUInt64( o, NumberFormatInfo.InvariantInfo ) );
        }
        else
        {
            // Parentheses are required around negative values.
            long v = Convert.ToInt64( o, NumberFormatInfo.InvariantInfo );
            if( v >= 0 ) @this.Append( v );
            else @this.Append( '(' ).Append( v ).Append( ')' );
        }
        return @this.Append( ')' );
    }

    /// <summary>
    /// Appends the code source for an untyped object.
    /// Only types that are implemented through one of the existing Append, AppendArray (all IEnumerable are
    /// handled) and enum values.
    /// extension methods are supported: an <see cref="ArgumentException"/> is thrown for unsupported type.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="o">The object. Can be null.</param>
    /// <param name="useValueTupleParentheses">False to use the (safer) "System.ValueTuple&lt;&gt;" instead of the (tuple, with, parentheses, syntax).</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    static public T Append<T>( this T @this, object? o, bool useValueTupleParentheses = true ) where T : ICodeWriter
    {
        if( o == Type.Missing ) return @this.Append( "System.Type.Missing" );
        if( o == DBNull.Value ) return @this.Append( "System.DBNull.Value" );
        switch( o )
        {
            case null: return @this.Append( "null" );
            case Type x: return @this.AppendTypeOf( x );
            case MemberInfo m: return @this.Append( m );
            case string x: return Append( @this, x.ToSourceString() );
            case bool x: return Append( @this, x );
            case int x: return Append( @this, x );
            case long x: return Append( @this, x );
            case short x: return Append( @this, x );
            case ushort x: return Append( @this, x );
            case sbyte x: return Append( @this, x );
            case uint x: return Append( @this, x );
            case ulong x: return Append( @this, x );
            case byte x: return Append( @this, x );
            case Guid x: return Append( @this, x );
            case char x: return AppendSourceChar( @this, x );
            case double x: return Append( @this, x );
            case float x: return Append( @this, x );
            case decimal x: return Append( @this, x );
            case DateTime x: return Append( @this, x );
            case TimeSpan x: return Append( @this, x );
            case DateTimeOffset x: return Append( @this, x );
            case IEnumerable<Type> x: return AppendArray( @this, x, useValueTupleParentheses );
            case IEnumerable x: return AppendArray( @this, x, useValueTupleParentheses );
        }
        Type t = o.GetType();
        if( t.IsEnum ) return AppendEnumValue( @this, t, o );
        return Throw.ArgumentException<T>( "Unknown type: " + o.GetType().AssemblyQualifiedName );
    }

    /// <summary>
    /// Creates a segment of code inside this function.
    /// This signature allows a fluent code to "emit" one or more insertion points.
    /// </summary>
    /// <typeparam name="T">The function scope type.</typeparam>
    /// <param name="this">This function scope.</param>
    /// <param name="part">The function part to use to inject code at this location (or at the top).</param>
    /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
    /// <returns>This function scope writer to enable fluent syntax.</returns>
    public static T CreatePart<T>( this T @this, out IFunctionScopePart part, bool top = false ) where T : IFunctionScope
    {
        part = @this.CreatePart( top );
        return @this;
    }

    /// <summary>
    /// Creates a segment of code inside this namespace.
    /// This signature allows a fluent code to "emit" one or more insertion points.
    /// </summary>
    /// <typeparam name="T">The namespace scope type.</typeparam>
    /// <param name="this">This namespace scope.</param>
    /// <param name="part">The namespace part to use to inject code at this location (or at the top).</param>
    /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
    /// <returns>This namespace scope writer to enable fluent syntax.</returns>
    public static T CreatePart<T>( this T @this, out INamespaceScopePart part, bool top = false ) where T : INamespaceScope
    {
        part = @this.CreatePart( top );
        return @this;
    }

    /// <summary>
    /// Creates a segment of code inside this type.
    /// This signature allows a fluent code to "emit" one or more insertion points.
    /// </summary>
    /// <typeparam name="T">The type scope type.</typeparam>
    /// <param name="this">This type scope.</param>
    /// <param name="part">The type part to use to inject code at this location (or at the top).</param>
    /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
    /// <returns>This type scope writer to enable fluent syntax.</returns>
    public static T CreatePart<T>( this T @this, out ITypeScopePart part, bool top = false ) where T : ITypeScope
    {
        part = @this.CreatePart( top );
        return @this;
    }

    /// <summary>
    /// Fluent function application: this enables a procedural fragment to be inlined in a fluent code.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="f">Fluent function to apply.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T Append<T>( this T @this, Func<T, T> f ) where T : ICodeWriter
    {
        Throw.CheckNotNullArgument( f );
        return f( @this );
    }

    /// <summary>
    /// Fluent action application: this enables a procedural fragment to be inlined in a fluent code.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="f">Action to apply to this code writer.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T Append<T>( this T @this, Action<T> f ) where T : ICodeWriter
    {
        Throw.CheckNotNullArgument( f );
        f( @this );
        return @this;
    }

    /// <summary>
    /// Appends an array with the given types.
    /// When <paramref name="types"/> is null, "null" is written, when the enumerable
    /// is empty, "<see cref="Type.EmptyTypes"/>" is written.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="types">Types to <see cref="AppendTypeOf"/> in an array.</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T AppendArray<T>( this T @this, IEnumerable<Type>? types ) where T : ICodeWriter
    {
        if( types == null ) return @this.Append( "null" );
        bool atLeastOne = false;
        foreach( var t in types )
        {
            if( atLeastOne ) @this.Append( ", " );
            else
            {
                @this.Append( "new[]{" );
                atLeastOne = true;
            }
            @this.AppendTypeOf( t );
        }
        if( atLeastOne ) @this.Append( "}" );
        else @this.Append( "System.Type.EmptyTypes" );
        return @this;
    }

    /// <summary>
    /// Appends a star comment with the current source origin and method name.
    /// </summary>
    /// <typeparam name="T">Actual type of the code writer.</typeparam>
    /// <param name="this">This code writer.</param>
    /// <param name="comment">Optional comment that start the star comment.</param>
    /// <param name="format">Full comment format.</param>
    /// <param name="appendNewLine">False to not append a new line after the comment.</param>
    /// <param name="source">Generator file name (should not be set explicitly).</param>
    /// <param name="lineNumber">Line number in the generator file (should not be set explicitly).</param>
    /// <param name="methodName">The name of the caller (should not be set explicitly).</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static T GeneratedByComment<T>( this T @this,
                                           string? comment = null,
                                           string format = "Generated by {0}, line: {1} (method {2}).",
                                           bool appendNewLine = true,
                                           [CallerFilePath] string? source = null,
                                           [CallerLineNumber] int lineNumber = 0,
                                           [CallerMemberName] string? methodName = null ) where T : ICodeWriter
    {
        @this.Append( "/* " );
        if( !string.IsNullOrEmpty( comment ) ) @this.Append( comment ).Append( " - " );
        @this.Append( String.Format( CultureInfo.InvariantCulture, format, source, lineNumber, methodName ) );
        @this.Append( " */" );
        return appendNewLine ? @this.NewLine() : @this;
    }

    /// <summary>
    /// Appends a #region with an optional comment with the current method name an source origin.
    /// </summary>
    /// <param name="this">This code writer.</param>
    /// <param name="comment">Optional comment that start the region name.</param>
    /// <param name="format">Source generation info format.</param>
    /// <param name="source">Generator file name (should not be set explicitly).</param>
    /// <param name="lineNumber">Line number in the generator file (should not be set explicitly).</param>
    /// <param name="methodName">The name of the caller (should not be set explicitly).</param>
    /// <returns>This code writer to enable fluent syntax.</returns>
    public static IDisposable Region( this ICodeWriter @this, string? comment = null, string format = "Generated by '{2}' in {0}, line: {1}.", [CallerFilePath] string? source = null, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string? methodName = null )
    {
        @this.NewLine().Append( "#region " );
        if( !string.IsNullOrEmpty( comment ) ) @this.Append( comment ).Append( " - " );
        @this.Append( String.Format( CultureInfo.InvariantCulture, format, source, lineNumber, methodName ) ).NewLine();
        return Util.CreateDisposableAction( () =>
        {
            @this.NewLine().Append( "#endregion " );
            if( !string.IsNullOrEmpty( comment ) ) @this.Append( comment ).Append( " - " );
            @this.Append( "'" ).Append( methodName ).Append( "'." ).NewLine();
        } );
    }

    sealed class Combiner : ICodeWriter
    {
        readonly ICodeWriter _w1;
        readonly ICodeWriter _w2;

        public Combiner( ICodeWriter w1, ICodeWriter w2 )
        {
            _w1 = w1;
            _w2 = w2;
        }

        public void DoAdd( string? code )
        {
            _w1.DoAdd( code );
            _w2.DoAdd( code );
        }
    }

    /// <summary>
    /// Combines 2 writers into one: the same code will be added to both of them.
    /// </summary>
    /// <param name="this">This code writer.</param>
    /// <param name="other">The other code writer.</param>
    /// <returns>A writer for this and the other writer.</returns>
    public static ICodeWriter And( this ICodeWriter @this, ICodeWriter other ) => new Combiner( @this, other );

}

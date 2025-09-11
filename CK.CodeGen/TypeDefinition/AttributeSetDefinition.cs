using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.CodeGen;

/// <summary>
/// An attribute set definition appears inside [] and contains one or more comma separated <see cref="AttributeDefinition"/>.
/// This set is characterized by its <see cref="Target"/> that applies to all its <see cref="Attributes"/>.
/// </summary>
public class AttributeSetDefinition
{
    /// <summary>
    /// Initializes a new <see cref="AttributeSetDefinition"/>.
    /// </summary>
    /// <param name="target">The target for this set.</param>
    /// <param name="attributes">Optional existing list of attributes.</param>
    public AttributeSetDefinition( CodeAttributeTarget target, List<AttributeDefinition>? attributes = null )
    {
        Target = target;
        Attributes = attributes ?? new List<AttributeDefinition>();
    }

    /// <summary>
    /// Gets the target.
    /// </summary>
    public CodeAttributeTarget Target { get; }

    /// <summary>
    /// Gets the mutable list of attributes in this definition.
    /// </summary>
    public IList<AttributeDefinition> Attributes { get; }

    /// <summary>
    /// Writes this AttributeSetDefinition into the provided StringBuilder.
    /// </summary>
    /// <param name="b">The target.</param>
    /// <returns>The StringBuilder to enable fluent syntax.</returns>
    public StringBuilder Write( StringBuilder b )
    {
        Throw.CheckNotNullArgument( b );
        if( Attributes.Count > 0 )
        {
            b.Append( '[' );
            if( Target != CodeAttributeTarget.None ) b.Append( Target.ToString().ToLowerInvariant() ).Append( ": " );
            bool already = false;
            foreach( var one in Attributes )
            {
                if( already ) b.Append( ", " );
                else already = true;
                one.Write( b );
            }
            b.Append( ']' );
        }
        return b;
    }

    internal void MergeWith( AttributeSetDefinition set )
    {
        Throw.DebugAssert( set.Target == Target );
        foreach( var a in set.Attributes ) Attributes.Add( a );
    }
}

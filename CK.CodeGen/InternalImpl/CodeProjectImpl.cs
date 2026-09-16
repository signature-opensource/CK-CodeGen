using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CK.CodeGen;

sealed class CodeProjectImpl : ICodeProject
{
    public CodeProjectImpl( string projectName, ICodeWorkspace code )
    {
        Code = code;
        ProjectName = projectName;
        TargetFrameworks = new HashSet<string>();
        Sdk = "Microsoft.NET.Sdk";
        PackageReferences = new List<PackageReference>();
    }

    public ICodeWorkspace Code { get; }

    public string ProjectName { get; }

    public HashSet<string> TargetFrameworks { get; }

    public string? LangVersion { get; set; }

    public string? OutputType { get; set; }

    public string Sdk { get; set; }

    public IList<PackageReference> PackageReferences { get; }

    public IReadOnlyCollection<PackageReference> UnifiedPackageReferences
    {
        get
        {
            var all = PackageReferences.Concat( Code.AssemblyReferences.Select( a => PackageReference.FromAssembly( a ) ) );
            Dictionary<string, PackageReference> best = new Dictionary<string, PackageReference>();
            foreach( var p in all )
            {
                if( best.TryGetValue( p.Name, out var exists ) )
                {
                    if( exists.Version < p.Version ) best[p.Name] = p;
                }
                else best.Add( p.Name, p );
            }
            return best.Values;
        }
    }

    public XElement CreateRootElement()
    {
        return new XElement( "Project", new XAttribute( "Sdk", Sdk ),
                new XElement( "PropertyGroup",
                    new XElement( TargetFrameworks.Count > 1 ? "TargetFrameworks" : "TargetFramework", TargetFrameworks.Aggregate( ( a, b ) => a + ';' + b ) ),
                    OutputType != null ? new XElement( "OutputType", OutputType ) : null,
                    LangVersion != null ? new XElement( "LangVersion", LangVersion ) : null,
                    new XElement( "AssemblyName", ProjectName ) ),
                new XElement( "ItemGroup",
                    UnifiedPackageReferences.Select( p => new XElement( "PackageReference",
                                                            new XAttribute( "Include", p.Name ),
                                                            new XAttribute( "Version", p.Version ) ) ) ) );
    }
}

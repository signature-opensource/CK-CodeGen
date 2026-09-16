using CK.Core;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Reflection;

using static CK.Testing.MonitorTestHelper;

namespace CK.CodeGen.Roslyn.Tests;

static partial class LocalTestHelper
{
    public static string RandomDllPath => TestHelper.BinFolder.AppendPart( $"Test-{Guid.NewGuid().ToString().Substring( 0, 8 )}.dll" );

    public static Assembly CreateAssembly( string sourceCode, IEnumerable<Assembly> references, params ICodeGeneratorModule[] modules )
    {
        var g = new CodeGenerator( CodeWorkspace.Factory );
        g.Modules.AddRange( modules );
        return HandleCreateResult( sourceCode,
                                   g.Generate( sourceCode, RandomDllPath,
                                               references,
                                               false,
                                               GetAssemblyLoader() ) );
    }

    static Assembly HandleCreateResult( string sourceCode, GenerateResult result )
    {
        result.LogResult( TestHelper.Monitor, LogLevel.Info );
        result.Success.ShouldBeTrue();
        return result.Assembly.ShouldNotBeNull();
    }

    private static Func<string, Assembly> GetAssemblyLoader()
    {
        Func<string, Assembly> loader;
#if NET461
        loader = Assembly.LoadFrom;
#else
        loader = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath;
#endif
        return loader;
    }
}

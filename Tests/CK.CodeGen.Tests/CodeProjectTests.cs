using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;
using System.IO;
using CK.Core;
using System.Diagnostics;
using System.Text;

namespace CK.CodeGen.Tests;

[TestFixture]
public class CodeProjectTests
{
    [Test]
    public void full_run_test()
    {
        var f = TestHelper.TestProjectFolder.AppendPart( "TestCodeProject" );
        TestHelper.CleanupFolder( f );
        ICodeProject project = CodeWorkspace.CreateProject( "MyProject" );
        project.TargetFrameworks.Add( "netcoreapp3.1" );
        project.OutputType = "Exe";

        project.Code.Global.EnsureUsing( "System" );
        ITypeScope program = project.Code.Global.CreateType( "public static class Program" );
        IFunctionScope main = program.GeneratedByComment()
                                     .CreateFunction( "public static int Main()" );
        main.Append( "Console.WriteLine(" ).AppendSourceString( "Hop!" ).Append( " );" ).NewLine();
        main.Append( "return 0;" );

        var projectFolder = WriteProjectFolder( f, project );
        Run( projectFolder, "dotnet", "run", out string output );
        output.ShouldContain( "Hop!" );
    }

    NormalizedPath WriteProjectFolder( NormalizedPath folder, ICodeProject project )
    {
        folder = folder.AppendPart( project.ProjectName );
        Directory.CreateDirectory( folder );
        project.CreateRootElement().Save( folder.AppendPart( project.ProjectName + ".csproj" ) );
        using( var source = new StreamWriter( folder.AppendPart( "Program.cs" ) ) )
        {
            project.Code.WriteGlobalSource( source );
        }
        return folder;
    }

    /// <summary>
    /// Simple process run.
    /// </summary>
    /// <param name="workingDir">The working directory.</param>
    /// <param name="fileName">The file name to run.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="output">Output of the process (on the StdOut).</param>
    /// <returns>True on success (<see cref="Process.ExitCode"/> is equal to 0), false otherwise.</returns>
    public static bool Run(
             string workingDir,
             string fileName,
             string arguments,
             out string output )
    {
        var m = TestHelper.Monitor;
        ProcessStartInfo cmdStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        cmdStartInfo.Arguments = arguments;
        using( m.OpenTrace( $"{fileName} {cmdStartInfo.Arguments}" ) )
        using( Process cmdProcess = new Process() )
        {
            StringBuilder outputCapture = new StringBuilder();
            StringBuilder errorCapture = new StringBuilder();
            cmdProcess.StartInfo = cmdStartInfo;
            cmdProcess.ErrorDataReceived += ( o, e ) => { if( !string.IsNullOrEmpty( e.Data ) ) errorCapture.AppendLine( e.Data ); };
            cmdProcess.OutputDataReceived += ( o, e ) => { if( e.Data != null ) outputCapture.Append( e.Data ); };
            cmdProcess.Start();
            cmdProcess.BeginErrorReadLine();
            cmdProcess.BeginOutputReadLine();
            cmdProcess.WaitForExit();
            if( errorCapture.Length > 0 )
            {
                using( m.OpenGroup( LogLevel.Error, "Received errors on <StdErr>:" ) )
                {
                    m.Log( LogLevel.Error, errorCapture.ToString() );
                }
            }
            output = outputCapture.ToString();
            if( cmdProcess.ExitCode != 0 )
            {
                m.Error( $"Process returned ExitCode {cmdProcess.ExitCode}." );
                return false;
            }
            return true;
        }
    }
}

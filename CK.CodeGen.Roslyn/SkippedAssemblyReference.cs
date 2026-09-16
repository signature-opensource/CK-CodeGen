using CK.Core;
using System;
using System.Reflection;

namespace CK.CodeGen;

/// <summary>
/// Captures an assembly reference that could not be loaded while transitively closing the set of
/// references used to compile the generated code. See <see cref="GenerateResult.SkippedReferences"/>.
/// </summary>
public sealed class SkippedAssemblyReference
{
    /// <summary>
    /// Initializes a new <see cref="SkippedAssemblyReference"/>.
    /// </summary>
    /// <param name="requesting">The name of the assembly that references the <paramref name="wanted"/> one.</param>
    /// <param name="wanted">The name of the assembly that cannot be loaded.</param>
    /// <param name="error">The error raised by the load attempt.</param>
    public SkippedAssemblyReference( AssemblyName requesting, AssemblyName wanted, Exception error )
    {
        Throw.CheckNotNullArgument( requesting );
        Throw.CheckNotNullArgument( wanted );
        Throw.CheckNotNullArgument( error );
        Requesting = requesting;
        Wanted = wanted;
        Error = error;
    }

    /// <summary>
    /// Gets the name of the assembly that references the <see cref="Wanted"/> one.
    /// </summary>
    public AssemblyName Requesting { get; }

    /// <summary>
    /// Gets the name of the assembly that cannot be loaded.
    /// </summary>
    public AssemblyName Wanted { get; }

    /// <summary>
    /// Gets the error raised by the load attempt: a <see cref="System.IO.FileNotFoundException"/>,
    /// a <see cref="System.IO.FileLoadException"/> or a <see cref="BadImageFormatException"/>.
    /// </summary>
    public Exception Error { get; }

    /// <summary>
    /// Overridden to return a one-line descriptive string with all the data.
    /// </summary>
    /// <returns>A descriptive string.</returns>
    public override string ToString() => $"Assembly '{Requesting.FullName}' references '{Wanted.FullName}' that cannot be loaded: {Error.GetType().Name} - {Error.Message}";
}

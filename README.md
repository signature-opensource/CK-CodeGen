# CK-CodeGen

[![Licence](https://img.shields.io/github/license/signature-opensource/CK-CodeGen.svg)](LICENSE)

A source code model for C# that can be written out of order, and the Roslyn compilation that turns it
into an assembly.

| Package | Description | Latest stable |
|---------|-------------|---------------|
| [CK.CodeGen](CK.CodeGen/README.md) | The mutable model: scopes, parts, definitions, and source-safe value appending. | [![nuget](https://img.shields.io/nuget/v/CK.CodeGen.svg?label=CK.CodeGen)](https://www.nuget.org/packages/CK.CodeGen/) |
| [CK.CodeGen.Roslyn](CK.CodeGen.Roslyn/README.md) | Compiles a workspace into an assembly, with rewriting modules and detailed results. | [![nuget](https://img.shields.io/nuget/v/CK.CodeGen.Roslyn.svg?label=CK.CodeGen.Roslyn)](https://www.nuget.org/packages/CK.CodeGen.Roslyn/) |

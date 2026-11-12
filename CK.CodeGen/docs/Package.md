A mutable model of a C# source file whose namespaces, types and functions can be written in any order.
Main types: `CodeWorkspace.Create()` and `ICodeWorkspace`, the `INamespaceScope`, `ITypeScope` and
`IFunctionScope` scopes and their parts, `CodeWriterExtensions` for source-safe value appending,
`TypeDefinition`, `FunctionDefinition`, `NullableTypeTree`, and `XmlDocumentationReader` that reads the Xml
documentation of the types.

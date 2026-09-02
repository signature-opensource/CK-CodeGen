A mutable model of a C# source file, so that generated code does not have to be written in order.

Namespaces, types and functions are scopes that can be navigated and written into at any time, and any
scope can be segmented into parts that are filled later. Independent code generators can therefore
contribute to the same type without agreeing on an order, everything falling into place when the
workspace is built.

Also provides source-safe appending of values (primitives, Guid, DateTime, enums, type names, typeof
expressions), parsed type and function definitions, and a resolved view of nullable reference type
annotations through NullableTypeTree.

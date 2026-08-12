# Managed Protocol Bindings

`Devilution.Protocol` is the single C# code-generation owner for
`protocol/devilution.proto`. It contains generated wire types only and has no
dependency on Domain, Data, Engine, a gameplay module, a host, or a client.

Managed consumers must reference this project instead of adding another
`<Protobuf Include>` item. This keeps the Server and Godot client on one schema
and one runtime type identity.

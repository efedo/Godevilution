# Managed Protocol Bindings

`Devilution.Protocol` is the single C# code-generation owner for
`protocol/devilution.proto`. It contains generated wire types only and has no
dependency on Domain, Data, Engine, a gameplay module, a host, or a client.

Managed consumers must reference this project instead of adding another
`<Protobuf Include>` item. This keeps the Server and Godot client on one schema
and one runtime type identity.

Monster snapshots include server-owned Fallen fear countdown and retreat
direction fields (46-47). Clients render these values but never execute the
native AI behavior. Sneak hidden state and retreat countdown are likewise
server-owned fields 48-49. Corpse snapshots and Scavenger healing state are
server-owned fields 12 and 50-51.

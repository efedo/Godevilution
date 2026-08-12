# Devilution.Domain

`Devilution.Domain` is the engine-neutral deterministic kernel for authoritative
gameplay. It targets .NET 8 and intentionally has no package or project
dependencies. It must not reference the server host, Protobuf contracts, Godot,
content loaders, or a game-specific rules module.

The extracted capabilities currently include canonical little-endian hashing
and the authoritative inventory-grid layout: geometry, footprint validation,
placement, occupancy mutation, and index compaction. Server and data adapters
supply primitive values; domain components do not know about stores, protocol,
persistence, files, or content schemas.

Run its independent xUnit v3 suite from the repository root:

```powershell
dotnet run --project server/tests/Devilution.Domain.Tests/Devilution.Domain.Tests.csproj
```

Future deterministic primitives and state machines should move here only when
their API can remain independent of transport, storage, presentation, and
Diablo-specific content definitions.

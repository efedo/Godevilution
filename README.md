# Godevilution

Godevilution is an experimental fork of
[DevilutionX](https://github.com/diasurgical/DevilutionX) that is moving the
Diablo engine from an embedded C++/SDL architecture to a separated,
server-authoritative C# engine with a Godot C# client.

The project preserves the native client as a compatibility and parity harness
while gameplay systems move to the server. Single-player and multiplayer use
the same protocol: clients submit intent, and the server owns simulation,
randomness, world state, inventory, combat, and persistence.

## Repository layout

- `server/` - engine-neutral C# domain, data, and composition runtimes; the
  Diablo game module; generic authoritative host; Diablo protocol adapter;
  composition executable; persistence; and tests.
- `godot/` - Godot presentation client and engine-independent protocol client.
- `protocol/` - shared Protobuf contract, its single generated C# assembly,
  and language-neutral test vectors.
- `Source/` - inherited native C++ client and its server-backed compatibility
  adapter.
- `server/content/base/` - current external authoritative content pack.
- `test/fixtures/replay/` - shared C++/C# deterministic replay fixtures.

See the [documentation index](docs/README.md),
[architecture](docs/csharp-authoritative-server-migration.md), and
[current migration status](docs/migration-status.md). The
[completion plan](docs/completion-plan.md) defines delivery order and acceptance
gates. The backend foundations are established; full gameplay parity and the
complete playable Godot client remain in progress.
Contributor expectations are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Quick verification

```powershell
dotnet run --project server/tests/Devilution.Domain.Tests/Devilution.Domain.Tests.csproj
dotnet run --project server/tests/Devilution.Data.Tests/Devilution.Data.Tests.csproj
dotnet run --project server/tests/Devilution.Engine.Tests/Devilution.Engine.Tests.csproj
dotnet run --project server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj
dotnet run --project server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj
dotnet build server/src/Devilution.Server/Devilution.Server.csproj
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build godot/Devilution.Client/Devilution.Client.sln
```

Run the server with:

```powershell
dotnet run --project server/src/Devilution.Server/Devilution.Server.csproj
```

The standalone server defaults to multiplayer. Pass
`-- --game-mode single-player` for a local single-player session; both modes use
the same authoritative protocol and implementation.

The Godot client requires Godot 4.7.1 Mono. Run
`powershell -ExecutionPolicy Bypass -File run-godot-client.ps1` for a local
server/client prototype session. Build the server and Godot solution above
first; the current Godot harness does not refresh every existing binary.
See [godot/README.md](godot/README.md) for details and
[docs/native-client.md](docs/native-client.md) for the native compatibility
client.

## Origin and game data

Godevilution derives from DevilutionX and ultimately from the Devilution
reverse-engineering project. It does not include Blizzard's Diablo game data.
Running the inherited native client requires legally obtained Diablo assets.
The C# server requires no MPQ files; it loads validated external content packs.
Godot currently uses stand-in assets. The target is a complete MPQ-free client
presentation pack with optional support for legally supplied original assets.

## License

The inherited [Sustainable Use License](LICENSE.md) remains in the repository.
Its presence is preserved as part of the fork's licensing history; consult a
qualified professional for legal advice about redistribution or relicensing.

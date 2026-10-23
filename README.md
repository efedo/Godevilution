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
- `godot-client/` - Godot presentation client and engine-independent protocol client.
- `protocol/` - shared Protobuf contract, its single generated C# assembly,
  and language-neutral test vectors.
- `legacy-native/` - inherited native C++ client and its server-backed compatibility
  adapter.
- `server/content/base/` - current external authoritative content pack.
- `test/fixtures/replay/` - shared C++/C# deterministic replay fixtures.

See the [documentation index](docs/README.md),
[architecture](docs/csharp-authoritative-server-migration.md), and
[current migration status](docs/migration-status.md). The
[near-term work plan](docs/work-plan.md) owns the short execution order; the
[feature inventory](docs/feature-gap-inventory.md) owns completion states, and the
[completion plan](docs/completion-plan.md) defines milestone requirements and exits. The backend foundations are established; full gameplay parity and the
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
dotnet run --project godot-client/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build godot-client/Devilution.Client/Devilution.Client.sln
```

Run the server with:

```powershell
dotnet run --project server/src/Devilution.Server/Devilution.Server.csproj
```

The standalone server defaults to multiplayer. Pass
`-- --game-mode single-player` for a local single-player session; both modes use
the same authoritative protocol and implementation.

The Godot client requires a stable Godot 4.7.x Mono engine and renders with
Vulkan (no OpenGL fallback). The source SDK stays
pinned to 4.7.1; isolated launch acceptance has been qualified with 4.7.2. Run
`powershell -ExecutionPolicy Bypass -File run-godot-client.ps1` for a local
server/client prototype session. The combat HUD maps keys `1`–`8` to eligible belt
consumables; each action remains server-authoritative. The launcher rebuilds both
projects, imports
assets, and retains local saves and the resume bookmark under `.local/godot`.
For the optional authored town/combat/equipment/shop/stash preview, run
`powershell -ExecutionPolicy Bypass -File run-godot-preview.ps1`. It starts in
town with an isolated overlay and saves under `.local/godot-preview`; it does
not change the default base pack or existing saves. This is not a full campaign.
For the same authored route with a purchased Town Portal scroll, use
`powershell -ExecutionPolicy Bypass -File run-godot-portal-preview.ps1`.
Its additional stock overlay and `.local/godot-portal-preview` saves are separate
from the original preview; neither profile grants extra starting resources.
For two local Godot windows on one multiplayer host, run
`powershell -ExecutionPolicy Bypass -File run-godot-multiplayer.ps1`. It uses
base content with separate player bookmarks under `.local/godot-multiplayer`.
See [Godot build and application checks](docs/ai/godot-build.md) for acceptance.
See [godot-client/README.md](godot-client/README.md) for details and
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

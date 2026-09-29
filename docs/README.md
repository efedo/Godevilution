# Godevilution Documentation

This index separates current project documentation from historical migration
records. Documents under `docs/history/` are retained for context and are not
implementation instructions.

Read [migration status](migration-status.md#progress-assessment) for the dated
baseline, then the [completion plan](completion-plan.md) for what to do next.
The plan owns priorities and acceptance gates; architecture owns invariants;
specialist guides own contracts and operation. Keep estimates only in migration
status and chronological completed work in [the changelog](CHANGELOG.md).

## Current architecture and status

- [Authoritative architecture](csharp-authoritative-server-migration.md)
- [Migration status](migration-status.md)
- [Completion plan and workflow](completion-plan.md)
- [Protocol contract](authoritative-protocol-contract.md)
- [Protocol package](../protocol/README.md)
- [Authoritative server](../server/README.md)
- [Engine-neutral domain kernel](../server/src/Devilution.Domain/README.md)
- [Validated data runtime](../server/src/Devilution.Data/README.md)
- [Engine composition runtime](../server/src/Devilution.Engine/README.md)
- [Diablo gameplay module](../server/src/Devilution.Diablo/README.md)
- [Godot client](../godot/README.md)
- [Native compatibility client](native-client.md)
- [Replay fixtures](replay-fixtures.md)
- [Lua replacement inventory](lua-capability-inventory.md)
- [Repository organization](source-organization.md)
- [Entity-pool contract](ENTITY_POOLS.md)
- [Entity-pool modernization](ENTITY_POOL_MODERNIZATION.md)

## Development

- [Contributing](../CONTRIBUTING.md)
- [MSVC build notes for agents](ai/msvc-build.md)
- [Godot build notes](ai/godot-build.md)
- [Debugging the native client](debug.md)
- [Style guide](STYLE_GUIDE.md)
- [Authoritative content tables](../server/content/README.md)

## History

Historical plans and completed analyses live under [history](history/README.md).
They may describe intermediate states that no longer match the repository.

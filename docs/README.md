# Godevilution Documentation

Start with [working status/memory](migration-status.md), the
[numbered feature and gap inventory](feature-gap-inventory.md) and the
[completion plan](completion-plan.md). The inventory owns feature statuses;
the plan owns priorities and acceptance gates. Keep these roles separate.

## Current project state

- [Working status and memory](migration-status.md)
- [Numbered feature and gap inventory](feature-gap-inventory.md)
- [Completion plan and workflow](completion-plan.md)
- [Implementation history](CHANGELOG.md)
- [Test history](test-history.md)
- [Clean-session handoff and goal](ai/session-handoff.md)

## Architecture and contracts

- [Authoritative architecture](csharp-authoritative-server-migration.md)
- [Protocol contract](authoritative-protocol-contract.md)
- [Protocol package](../protocol/README.md)
- [Authoritative server](../server/README.md)
- [Engine-neutral domain kernel](../server/src/Devilution.Domain/README.md)
- [Validated data runtime](../server/src/Devilution.Data/README.md)
- [Engine composition runtime](../server/src/Devilution.Engine/README.md)
- [Diablo gameplay module](../server/src/Devilution.Diablo/README.md)
- [Godot client](../godot/README.md)
- [Native compatibility client](native-client.md)
- [Replay reference](replay-fixtures.md)
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

Past document versions and removed detail belong in Git history. Do not create
separate historical snapshots, QA archives or copies of obsolete plans.

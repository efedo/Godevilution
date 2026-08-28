# Entity-pool modernization strategy

The native client is being migrated from globally indexed arrays toward
server-owned entities without breaking Devilution-compatible code. The
modernization is intentionally staged:

1. Pool-owned storage keeps the old `Items`, `Monsters`, and `Objects` names
   source-compatible.
2. Adapter boundaries centralize allocation, release, active-list iteration,
   and reset behavior for connected presentation code.
3. Authoritative projections claim slots by stable server entity ID and remove
   any unclaimed compatibility slots on every snapshot.
4. Native gameplay mutation is bypassed for connected sessions; the pools are
   used only for rendering, tile lookup, and legacy read-only affordances.
5. Once Godot presentation covers the remaining native interactions, the
   adapters can be removed or reduced to an offline compatibility layer.

The migration must retain fixed capacities, native tile references, save-file
compatibility, and deterministic replay behavior at each step. New gameplay
state belongs in the engine/data/game-module layers, not in these presentation
containers.

See [the pool contract](ENTITY_POOLS.md) for allocation policy and lifecycle
rules.

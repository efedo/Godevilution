# Entity-pool modernization strategy

This is a compatibility strategy, not a separate project roadmap. Current
projection/pool work is recorded in [migration status](migration-status.md);
near-term order follows the [work plan](work-plan.md), with retirement subject
to the M5 gates in the [completion plan](completion-plan.md). Storage aliases, adapter operations, and snapshot reconciliation
already exist; audit remaining consumers instead of restarting those steps.

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
5. Once Godot passes the complete play/compatibility gates, remove production
   dependence on these adapters. Retain an isolated native compatibility/oracle
   build while supported consumers remain; physical removal requires their
   characterized replacements, not just equivalent-looking Godot presentation.

The migration must retain fixed capacities, native tile references, save-file
compatibility, and deterministic replay behavior at each step. New gameplay
state belongs in the engine/data/game-module layers, not in these presentation
containers.

See [the pool contract](ENTITY_POOLS.md) for allocation policy and lifecycle
rules.

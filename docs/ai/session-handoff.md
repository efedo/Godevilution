# Clean-session handoff

Prepared 2026-10-22. Use `D:\Programming\Godevilution` and its existing working
tree. Fresh context does not mean a reset or a replacement checkout.

## Read first

1. [AGENTS.md](../../AGENTS.md)
2. [Worker guide](worker-guide.md): how to choose, size, port, verify, document
   and hand off a batch, and the patterns that previously sent work off track.
3. The current phase of the [work plan](../work-plan.md), including its batch
   queue.
4. [Working status](../migration-status.md) sections on limits and environment.

Search other documents only for the contract you need. Do not read the
histories end to end or repeat their tests to rebuild context.

## First task

Take the next unstarted batch in the current phase's queue in the
[work plan](../work-plan.md), write its batch contract as described in the
[worker guide](worker-guide.md#batch-contract), and start. Catacombs-only parity
work is frozen; unfinished earlier drafts are on branch `wip/phase0-shelf` and
are recovered piecemeal only when a batch needs them.

## Session goal

Complete Godevilution's migration to a server-authoritative C# engine and fully
playable Godot client, including supported Diablo and Hellfire progression,
native behavioral compatibility, full native graphical parity in Godot, shared
single-player/multiplayer infrastructure, optional client MPQs and retirement of
production C++/SDL and Lua dependencies. The server must never require MPQs.
Keep engine, validated external data, game modules, hosting and presentation
separate. The server owns gameplay, RNG, world state and persistence. The
[work plan](../work-plan.md) owns the order: Diablo single-player first.

## Operating policy

- Follow [working status](../migration-status.md#working-environment-and-authority)
  for environment, retained holds and authority. Preserve unrelated work, use
  CRLF (except shell scripts and Godot-written files), and keep `master` CI green.
- No reset, bulk commit, security changes or new archives. Push only on explicit
  user instruction. Git history holds past versions; do not create backup
  documentation or QA dumps.
- Use the [fixed worker policy](../completion-plan.md#worker-delegation). Root
  owns batch selection, integration, verification, concise documentation and
  cohesive commits; every worker and reviewer follows the
  [worker guide](worker-guide.md).

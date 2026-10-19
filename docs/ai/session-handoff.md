# Clean-session handoff

Prepared 2026-10-19. Use `D:\Programming\Godevilution` and its existing working
tree; gameplay source baseline `3af2a51cc`. Fresh context does not mean a reset or a
replacement checkout.

## Read first

Read [AGENTS.md](../../AGENTS.md), this handoff and
[working status/memory](../migration-status.md) completely. Then read the
[near-term work plan](../work-plan.md),
[numbered feature inventory](../feature-gap-inventory.md),
[completion plan](../completion-plan.md), documentation index and relevant build
guides. Implementation history is in [CHANGELOG.md](../CHANGELOG.md); verification
history is in [test-history.md](../test-history.md). Read only the history relevant
to the next decision; do not repeat its tests merely to rebuild context.

## Repository layout and cleanup

The folder rename is committed as `aea715e63`: the Godot tree is now
`godot-client/` and the C++ compatibility runtime is `legacy-native/`.
Use these paths in source, build and launcher work; the gameplay baseline above
is unchanged.

Generated-output cleanup reclaimed about 17.2 GiB. Old local run/export/report
archives and temporary builds were removed; current inputs, build caches and
unique sources/scripts/patches remain. Do not assume historical `.local` run
paths exist. See [working environment](../migration-status.md#working-environment-and-authority)
for retained caches and input entry points. Gameplay WIP remains uncommitted.

## First task

The reviewed session-flow batch now passes headless create/return/rejoin and
fresh-host resume, including profile-aware setup. Read the work plan for the next
proposed batch: native Warrior/Sorcerer admission, combat/ability use and resume.
Establish that batch before feature implementation; it has not started. Refresh
only changed claims and keep the work plan, working status and affected inventory
entries synchronized. Rendered flow remains open. Do not treat the failing L3 bot
scenario as a universal blocker.

## Session goal

Complete Godevilution's migration to a server-authoritative C# engine and fully
playable Godot client, including supported Diablo and Hellfire progression,
native behavioral compatibility, full native graphical parity in Godot, shared
single-player/multiplayer infrastructure, optional client MPQs and retirement of
production C++/SDL and Lua dependencies. The server must never require MPQs.
Keep engine, validated external data, game modules, hosting and presentation
separate. The server owns gameplay, RNG, world state and persistence.

Use the reviewed inventory to prioritize substantial playable batches. Deliver
complete behavior families, resolve independent review findings, run only the
necessary changed-behavior/compatibility/application checks and commit selectively.
Reuse valid evidence. Do not redefine the full objective around one seeded route,
one class or the work already implemented. Create the clean session's goal from
this section when authorized by its opening user request.

## Operating policy

Follow the working status for current constraints, WIP and environment. Main.cs
and Main.Session.cs source work is authorized. Preserve unrelated work and use
CRLF. Stay headless; no reset, bulk commit, security changes or new archives.
Push only on explicit user instruction.
Git history holds past versions; do not create backup documentation or QA dumps.

Use the [fixed worker policy](../completion-plan.md#worker-delegation): two
GPT-6 Luna/xhigh implementation workers and one GPT-6.1 Sol/medium independent
reviewer, initialized once and reused. Report model/capacity problems without
substitution; no nested delegation. Root owns integration, verification, concise
documentation and cohesive local commits.

# Near-term work plan

Updated 2026-10-19. The reviewed native Warrior/Sorcerer admission batch passes
its headless L1 scope. The next proposed primary batch is the native
Town/Cathedral visual slice; establish its headless asset/binding scope before
implementation and leave rendered acceptance open.

The native Rogue Town/L1/L2 route with earned gear, Town services and fresh-host
resume remains the broader reusable baseline. Explicit Warrior/Sorcerer profiles
now pass canonical preparation, L1 combat/abilities and exact cold resume. Later
campaigns, remaining classes and full Godot graphical parity remain open. See [working status](migration-status.md)
for current facts, constraints and WIP, and the [feature inventory](feature-gap-inventory.md)
for individual completion states. The [completion plan](completion-plan.md) keeps
the full migration requirements and milestone exit criteria.

## Current baseline

Class-bound profiles validate canonical native hero facts and starter bindings
before initial generation/admission, preserve Rogue output/save identities and
retain class/floor ownership across cold restore. Both new classes pass ordinary
L1 combat, class ability and fresh-host continuity through actual headless
controls. See [test history](test-history.md#2026-10-19--native-class-admission)
for the verified boundary. This does not establish complete class campaigns.

## Delivery order

| Order | Coherent deliverable | Inventory scope | Exit for this batch |
| --- | --- | --- | --- |
| 1 | Deliver a native Town/Cathedral visual slice: asset import/pack, admitted-class sprites and equipment/action selection. | Area 11; begin with 11.2–11.5 and 11.13–11.15 | Implement the asset/binding/rendering slice and perform matched native/Godot scene/animation comparisons when visual testing is permitted. Keep graphical acceptance open until those comparisons pass. |
| 2 | Extend the native world into Catacombs, beginning with L4/L5 entry and return and building the L5–L8 family coherently. | 5.6, 5.17, 5.22, 9.21 | Native generation/population comparisons for changed behavior, ordinary travel/combat, retained floor state and fresh-host return. |

The session-flow baseline now includes reviewed create/return/rejoin, local
profile class limits, cancellation/exit cleanup and exact fresh-host resume.
Headless checks pass; rendered usability and broader multiplayer/campaign flow
remain open. See [test history](test-history.md#2026-10-19--session-flow-consolidation).

Work on one primary playable batch at a time. Reorder this short queue when the
user selects a different priority or a real dependency changes. If a hold or the
headless constraint blocks one part, continue eligible work and retain the open
acceptance item. L3 bot deaths are diagnostics, not a universal prerequisite.

For each batch, integrate the affected content, rules, persistence and client
behavior; reuse the same implementation/review workers. Run necessary checks
for changed behavior and one relevant application journey, reuse valid existing
evidence, resolve independent review and commit selectively. Do not turn inventory
subitems into separate microtasks or rerun unrelated suites.

## Keep the plan current

Update this file, [working status](migration-status.md) and the affected
[inventory subitems](feature-gap-inventory.md) together when a batch starts or
finishes, scope/priority changes, or findings alter completion, evidence or
blockers. Reconcile them before an affected commit or handoff.

This file owns the short execution order; working status owns current facts and
constraints; the inventory owns feature completion. Replace completed queue
entries with the next useful work. Keep implementation/test history in their
existing documents and old versions in Git; do not append an archive here.

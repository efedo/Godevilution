# Near-term work plan

Updated 2026-10-19. The reviewed native Warrior/Sorcerer admission batch passes
its headless L1 scope. The active primary batch is the native Town/Cathedral
presentation slice. Optional original-player CL2/PAL/TRN import and snapshot-driven
class/equipment/direction/action selection pass full native pixel/atlas comparisons;
both headless class draw/restore pairs pass. Generic packs remain compatible
and original assets stay client-only. The pure Town composition prerequisite now
passes all 191 native cases. Continue with its explicit profile/backing-state
integration, native d-piece/CEL scene art and one isometric transform for cells,
actors, camera and pointer mapping. Complete scene coverage and matched rendered
comparisons remain open within this visual slice.

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

The original-player stage passes native indices/masks, published RGBA/padding and
actual class draw/restore checks; see [test history](test-history.md#2026-10-19--optional-native-player-art).
The qualified profiles already supply Cathedral final d-pieces, while Town's
112x112 geometry layers are empty. The separate MPQ-free builder now matches
Town's four sectors, automap, conditional mutations, retained channels, entry view
and RNG; see [test history](test-history.md#2026-10-19--native-town-geometry-prerequisite).
Integrate its 40x40 coarse and 112x112 fine backing state explicitly before
binding the Town scene. Normal cohort/profile adoption remains separately held.

## Delivery order

| Order | Coherent deliverable | Inventory scope | Exit for this batch |
| --- | --- | --- | --- |
| 1 | Continue the native Town/Cathedral visual slice with MPQ-free Town geometry preparation, CEL/MIN scene art and shared isometric cell/actor/camera/pointer mapping. | 11.2–11.5, 11.12, 11.23–11.26, 11.35–11.36 | Native composition/pixel comparisons, inverse transform/input checks and a headless journey/restore with actual scene bindings. Retain the normal cohort/profile adoption hold and open rendered acceptance. |
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

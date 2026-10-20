# Near-term work plan

Updated 2026-10-20. Native Warrior/Sorcerer admission passes its headless L1
scope. The active primary batch remains native Town/Cathedral presentation.
Optional original-player and scene imports pass full native pixel/atlas
comparisons. Distinct format-5 Town profiles now retain semantic fine/coarse/
special/protected state, canonical resident collision and strict v11 cold restore.
Both classes pass native Town/Cathedral drawing/input, Smith/Pepin controls and
exact fresh-host continuity. Generic packs and earlier profiles retain their
contracts. Complete actor/object/effect coverage, native scene phases, lighting,
palette cycling, wall transparency and matched rendered comparisons remain open.

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

The optional original-player and scene stages retain their full native pixel
qualification; see [test history](test-history.md#2026-10-19--optional-native-player-art)
and [scene import](test-history.md#2026-10-19--optional-native-scene-art-import).
The pure MPQ-free Town builder passes all 191 native cases. The additive format-5
profile compiles original Town map prefixes, TIL/SOL and the canonical roster
into semantic tables. Its v11 owner keeps 112x112 fine/special and 40x40 coarse/
protected channels separate, validates detached replay and preserves simulation
RNG. Native occupancy includes eight human residents and three four-cell cows;
the Smith remains solid with a reachable service position.

Both headless class pairs now prove generated L1 combat/abilities, ordinary Town
travel, Smith/Pepin controls, native Town/Cathedral raster draws and exact cold
retention. The backend also restores typed Town mutations and a newly visited
floor. Earlier format-4 profiles retain their empty Town layers and explicit
fallback. See [Town integration evidence](test-history.md#2026-10-19--explicit-native-town-integration).
Normal cohort/profile adoption remains separately held.

The Town profile/backing and original resident-art stages are complete for their
headless boundaries. Immutable projection covers all eight humans and three cows;
optional CEL/PAL art retains native pixels, directions, anchors and prepared
frame order/delay. Both class journeys observe every resident at an in-viewport
anchor and retain exact cold continuity. See [resident verification](test-history.md#2026-10-20--original-town-resident-art).
Live animation remains cosmetic; exact initialization RNG, authoritative cursors
and cold phase continuity stay open. The next cohesive family is
native Cathedral interactable-object art: L1 doors, ordinary/trapped chest types
1–3, sarcophagi, ordinary/explosive barrels and left/right wall traps (11.18).
Before runtime implementation, characterize every source frame, per-type initial/
operation frame and native anchor, including offset draw tiles. Use authoritative
current frames and typed door states; do not infer lifecycle timing from table
animation lengths or add a client gameplay clock. Qualify all imported pixels and
fallbacks, then ordinary interactions and retained frame/state/collision across
cold restore. Native lighting, pre/post phases, wall lookahead and missile art stay
separate.
Keep rendered qualification and the full graphical-parity requirement open.

## Delivery order

| Order | Coherent deliverable | Inventory scope | Exit for this batch |
| --- | --- | --- | --- |
| 1 | Native Cathedral interactable-object art for existing L1 doors, ordinary/trapped chests, sarcophagi, barrels and wall traps. | 11.5, 11.18, 11.22, 11.37 | Characterize source/operation frames and native anchors first; compare all imported pixels, validate fallback, then ordinary interaction and retained frame/state/collision through cold restore. Keep lighting, scene phases, missile art, rendered parity and normal-profile adoption open. |
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

# Godevilution Feature and Gap Inventory

Inventory date: 2026-10-18, HEAD `3af2a51cc`, with existing uncommitted packages
identified separately in the [working status](migration-status.md#existing-working-tree-packages).
This is a capability-level review of eleven areas, not an exhaustive function audit
or a new completion percentage. Numbers are stable identifiers; do not renumber
existing items when new items are added. Each status covers the full named area,
not just its implemented subset. **Missing implementation**, **native parity**,
**integration** and **acceptance** are different gaps; existing code is not absent
merely because a full journey has not passed. No new gameplay tests were run for
this inventory. The scope is the full supported Diablo/Hellfire replacement.

## Completion status

- **Complete**: the full named scope is implemented and its required acceptance
  evidence is established.
- **Partial**: an implemented subset exists; required implementation, parity,
  integration or acceptance remains incomplete.
- **Not started**: no implementation for the named scope has been established.
- **Unverified**: implementation is believed complete, but required evidence is missing.
- **Blocked**: a specific external dependency prevents progress; record the dependency.

All eleven broad areas are currently **Partial**. The implemented and remaining
columns distinguish completed subsets from their larger incomplete areas. No
numerical completion percentages are implied.

## Numbered inventory

| No. | Area | Completion status | What exists / demonstrated scope | Remaining concrete gameplay gap |
| --- | --- | --- | --- | --- |
| 1 | Player classes and progression | Partial | Six-class metadata/setup, native starting formulas, XP/level/stat allocation and class-skill paths exist. The generated Cathedral play profile and passed native journey are canonical Rogue, single-player Normal. | **Integration/parity:** make supported classes and difficulties usable with native generated profiles, valid loadouts and action/resource rules. The current native profile rejects noncanonical Rogue selection despite the broader setup UI. All-class campaign play is unproved. |
| 2 | Combat, spells and statuses | Partial | Melee, bows, missiles, spell casting, recovery, damage/status rules and many focused native comparisons exist. Actual native route proof includes bow combat/healing. Held entity input is committed and ACK-paced. | **Parity/acceptance:** complete class/gear/status/action-order combinations, simultaneous effects and live spell/class coverage. No specific missing core spell was established in this bounded pass; do not label the whole spell system unimplemented or infer a combat defect from L3 deaths. |
| 3 | Monsters, AI and summons | Partial | Native monster/type/spawn/unique/missile catalogs, many AI families and Cathedral actor adaptation exist. Scoped native encounters and corpse/XP retention pass. | **Implementation/parity:** admit and simulate remaining initial actor states, goals, emergence/petrification/special behaviors and full encounter families. Multiple-owner summons, replacement/credit and broad campaign AI remain incomplete or unqualified. Unsupported admission is distinct from absent AI code. |
| 4 | Items and economy | Partial | Generation/affixes/uniques, equipment, inventory/belt/cursor/stash, drops, consumables and buy/sell/repair/recharge/identify paths exist. Actual earned gear/Gold and Pepin purchase/heal routes pass. | **Parity/integration:** complete native generated vendor stock/service lifecycle, quest rewards and item/equipment combinations across classes/difficulties. Pinned Pepin stock and authored stores do not prove native initial Town stock/RNG or the entire economy. |
| 5 | Generated world and depth progression | Partial | Managed connected-grid generation and supplied DUN/TIL/SOL loading exist. Native ordinary Cathedral L1-L4 generation and lazy owners have oracle/backend evidence; actual Town/L1/L2 restore/revisit passes. | **Implementation:** native seed-driven Catacombs/Caves/Hell and Hellfire Hive/Crypt generation/population are not established by generic generators or imported maps. **Acceptance:** living L3 continuation and complete four-floor retained-world journey remain unproved. |
| 6 | Native objects and environment | Partial | Generated Cathedral doors, ordinary chests, sarcophagi, ordinary/explosive barrels and Arrow wall traps have shared ownership and scoped cold-retention proof. Generic authored object effects also exist. | **Implementation/parity:** remaining generated native object families, including fountains/shrines and their lifecycle/effect/RNG bindings, are not qualified by authored effects. No fountain implementation was started in this session. Wider traps, quest-linked interactions and environment mutations require family-specific closure. |
| 7 | Town, quests and set levels | Partial | Quest state/selection metadata, dialogue stages, towner interactions, supplied set-level loading and some authored quest/portal/death journeys exist. Town healing/purchases are proven in scoped native play. | **Integration/implementation:** the native depth policy is quest-disabled. Complete generated quest selection/triggers, set-level entry/return, map changes/rewards, final bosses and victory/difficulty progression are not delivered as a campaign. Supplied maps/catalogs are prerequisites, not completed quest routes. |
| 8 | Hellfire progression | Partial | Hellfire class/content definitions, specific behavior fixtures and Nest/Crypt supplied-map types exist. | **Implementation/integration:** a live native Hive/Crypt campaign, Hellfire quest/reward chains, Na-Krul encounter and final progression route are not established. Generic/imported map support does not close those families. |
| 9 | Persistence and multiplayer | Partial | Complete cohort capture/restore, durable receipts, duplicate handling and real two-client recovery/gameplay slices exist. Exact scoped native cold restore and retained mutations pass. | **Integration/acceptance:** carry every new campaign/quest/AI state through restart and multiplayer joins/ownership. Legacy per-participant cold imports still overwrite shared world/RNG. Normal cohort adoption is separately held; broader fault/filesystem/platform/soak qualification remains open. Persistence is not missing wholesale. |
| 10 | Client playability and flow | Partial | Ordinary controls, targeting, route travel, HUD/inventory/services and scene/recovery checks exist. Some menu/session/class-flow work remains uncommitted. | **Product/integration/acceptance:** consolidate the play flow and session WIP, match available profile/class choices, and complete audio, input/accessibility and rendered usability qualification. |
| 11 | Full Godot graphical parity | Partial | Client-pack validation, atlases, visual frame clocks and semantic player bindings exist, with SVG stand-ins and limited rendered checks. These are rendering foundations, not native graphical parity. | **Implementation/parity/acceptance:** match native Diablo/Hellfire world rendering, actor/equipment/facing/action sprites and animation, objects/items/projectiles/spell effects/corpses, draw ordering/occlusion, palettes/transparency/lighting/vision, automap, HUD/panels/icons/fonts/cursors, menus and transitions. Complete asset coverage and matched native/Godot rendered-frame and animation comparisons remain open. |

### Godot graphical parity acceptance

Full graphical parity is an explicit completion requirement, separate from
functional controls or readable replacement art. Cover Town, every supported
base/Hellfire dungeon family and set level; all supported player class/equipment
appearances and actions; monsters/NPCs, objects, items, missiles, effects and
corpses; and the complete game UI, automap and screen transitions.

Compare matched native/Godot scenes with the same assets, authoritative state,
camera, viewport and scaling settings. Verify sprite/frame selection, animation
cadence and synchronization, tile/sprite placement, depth sorting/occlusion,
palette/color cycling, transparency, light/vision presentation, UI layout,
fonts/icons and cursor/selection behavior. Use rendered images for static scenes
and frame sequences for animation; record remaining visual differences as gaps.
Asset-binding tests, headless protocol success and SVG placeholders do not close
this gate. Audio has its own completeness requirement in the client-flow area.

Keep asset loading/conversion client-only and preserve the complete MPQ-free
presentation-pack requirement. The server must never require MPQs. This adds no
GUI run to the current planning pass; rendered parity remains unverified until
its actual comparisons are performed.

Representative production sources: [class/profile admission](../server/src/Devilution.Server.Diablo/Gameplay/DiabloAuthoritativeGameHostAdapterFactory.cs),
[player progression](../server/src/Devilution.Diablo/DiabloPlayerProgression.cs),
[combat commands](../server/src/Devilution.Diablo/DiabloCombatCommandProcessor.cs),
[monster adaptation](../server/src/Devilution.Diablo/AuthoritativeCathedralMonsterRuntimeAdapter.cs),
[native depth generation](../server/src/Devilution.Diablo/AuthoritativeCathedralDepthGenerator.cs),
[native population policy](../server/src/Devilution.Diablo/AuthoritativeCathedralNativePopulationPolicy.cs),
[set-level catalog](../server/src/Devilution.Diablo/AuthoritativeSetLevelMapCatalog.cs),
[quest state](../server/src/Devilution.Diablo/AuthoritativeQuestState.cs),
[cohort store](../server/src/Devilution.Server.Host/Stores/AuthoritativeCohortStore.cs),
[module coverage](../server/src/Devilution.Diablo/README.md) and
[client scope](../godot/README.md). Current application evidence is summarized
[in migration status](migration-status.md#current-delivery-position-and-verification-limits); the next session
should inspect the exact source paths for its selected batch, not rerun every
historical suite or expand this into a method-by-method census.

Three substantial candidate batches for prioritization (none selected yet):

| Option no. | Candidate | Completion status | Playable outcome and dependency | Necessary acceptance |
| --- | --- | --- | --- | --- |
| 1 | Native base-class integration | Partial; not selected | Make the native Cathedral profile honor Warrior/Rogue/Sorcerer selection with valid native starting resources, loadouts, targeting and class abilities. Reuse class/progression/combat systems; qualify the profile/native preparation restrictions rather than rebuilding those systems. | Changed class/admission/preparation cases, representative ordinary combat/ability controls and exact fresh-host resume for newly enabled state. Do not repeat unrelated transport or geometry suites. |
| 2 | Campaign progression beyond Cathedral | Not started; not selected | Implement the next native dungeon family and its transition/population contract, beginning with Catacombs L5 entry/return. Requires the relevant generator, native placement/AI and persistent floor ownership; a supplied DUN loader is insufficient. | Native generation/placement comparisons for changed rules, ordinary cross-family travel/combat and retained fresh-host return. Stage the larger L5-L8 family coherently; preserve the existing four-floor evidence limit. |
| 3 | Usable client/session delivery | Partial; not selected | Consolidate the existing menu/HUD/session work into a clear launch/create/play/return/resume flow using supported explicit profiles. Include a staged delivery plan for the full graphical-parity gate, rather than accepting stand-in art as final. Reuse the proven Town/L1/L2 gameplay baseline; keep normal-profile adoption separate. | Review actual WIP, affected lifecycle/control checks and one relevant application journey. Rendered usability requires a later user-authorized visible check; headless evidence must be labeled accurately. |

The inventory does not choose the next implementation package. Other material
families, such as native object interactions, generated quest/set-level routes,
and multiple-owner summons, remain in the table and full completion plan.
Release packaging/performance/platform qualification and physical Lua/C++/SDL
retirement remain later objective requirements, not additional gameplay rows.

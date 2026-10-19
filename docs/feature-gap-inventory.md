# Godevilution Feature and Gap Inventory

Updated 2026-10-19 against documentation commit `1234efc6e`, gameplay baseline
`3af2a51cc` and the existing working tree. Uncommitted session/menu, class-flow
and experimental acceptance work is identified in [working memory](migration-status.md#existing-working-tree-packages).
This inventory covers the supported Diablo/Hellfire replacement, including full
Godot graphical parity. It records capabilities and remaining deliverables;
implementation history and dated test results stay in their separate documents.

Original areas 1–11 keep their identifiers. Area 12 makes the already-required
release and legacy-retirement gates explicit. Subitem IDs are stable: append new
IDs rather than renumbering existing ones. Each subitem has its own completion
status; a completed narrow prerequisite does not complete its parent area.

## Completion status and evidence

- **Complete**: the explicitly named scope is implemented with relevant existing
  evidence. A bounded profile or oracle scope is stated, rather than implied to
  cover every campaign, class or platform.
- **Partial**: usable implementation or data exists, with a known remaining
  implementation, native-parity, integration or acceptance requirement.
- **Not started**: no implementation of the named deliverable was found in the
  reviewed paths. Catalog entries and generic prerequisites may still exist.
- **Unverified**: the claimed behavior or necessary parity/acceptance has not
  been established; this does not mean its implementation is absent.
- **Blocked**: an external dependency prevents progress; identify it explicitly.

The remaining-work column distinguishes **implementation**, **native parity**,
**integration** and **acceptance**. Source presence, focused native comparison,
headless application proof and rendered proof are different evidence levels.
No new gameplay tests or GUI checks were run for this expansion. Source/test
links identify the basis for the claims, not a claim that every linked suite was
rerun. Existing accepted journeys and their limits are in [test history](test-history.md).
No overall completion percentage is inferred from item counts.

## Numbered inventory

| No. | Area | Completion status |
| --- | --- | --- |
| 1 | Player classes and progression | Partial |
| 2 | Combat, spells and statuses | Partial |
| 3 | Monsters, AI and summons | Partial |
| 4 | Items and economy | Partial |
| 5 | Generated world and depth progression | Partial |
| 6 | Native objects and environment | Partial |
| 7 | Town, quests and set levels | Partial |
| 8 | Hellfire progression | Partial |
| 9 | Persistence and multiplayer | Partial |
| 10 | Client playability and flow | Partial |
| 11 | Full Godot graphical parity | Partial |
| 12 | Release readiness and legacy retirement | Partial |

## 1. Player classes and progression

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 1.1 | Six-class identity and metadata | Complete | Warrior, Rogue, Sorcerer, Monk, Bard and Barbarian have stable IDs, availability and class-specific metadata. | None for catalog identity; playable class integration is scored separately. |
| 1.2 | Native starting attributes and resources | Complete | Shipped class definitions and focused tests cover native starting attributes and Q6 life/mana formulas. | None for the starting-formula scope; later equipment and progression use have separate entries. |
| 1.3 | Starting equipment, inventory and learned spells | Complete | Validated per-class loadouts include starting Gold, item topology, seeded identities, ability and learned-spell bindings. | None for the supported loadout-catalog contract; native profile admission remains separate. |
| 1.4 | Warrior gameplay | Partial | Class rules, loadout, melee traits and Repair exist; a class-control preview is WIP. | Integration/acceptance: admit Warrior to native generated play and qualify melee, equipment, progression and resume. |
| 1.5 | Rogue gameplay | Partial | Native single-player Normal Town/L1/L2 bow, equipment, services and cold-resume route passes. | Acceptance: complete later depths, quests, difficulties and campaign; the limited route does not close the class. |
| 1.6 | Sorcerer gameplay | Partial | Firebolt starting knowledge, mana/casting rules and an authored Recharge/quest/death journey exist. | Integration/acceptance: native generated-world admission, spell-driven progression and retained campaign play. |
| 1.7 | Monk gameplay | Partial | Hellfire metadata, unarmed/armor/block rules, Search and preview work exist. | Integration/parity: native Hellfire progression, equipment/action combinations and full class journey. |
| 1.8 | Bard gameplay | Partial | Dual-wield metadata, formulas and Identify behavior exist; scene/launcher preview work is uncommitted. | Integration/acceptance: supported profile admission, dual-wield combat and retained generated-world play. |
| 1.9 | Barbarian gameplay | Partial | Critical strike, armor/resistance traits, class formulas and Rage rules exist. | Integration/acceptance: native generated progression and Rage/equipment/resource behavior in full play. |
| 1.10 | Warrior Repair ability | Partial | Item mutation and class ability admission exist; actual-control preview covers a bounded repair case. | Native parity/acceptance: qualify supported item cases and generated-world use without conflating paid repair. |
| 1.11 | Rogue Trap Disarm ability | Partial | Actual generated Arrow-trap interaction and scoped retained state pass; authored preview also exists. | Native parity/integration: remaining trap types, roll/link conditions and full campaign use. |
| 1.12 | Sorcerer Recharge ability | Partial | Class-specific charge and maximum-charge mutation paths and authored controls exist. | Native parity/acceptance: broader staff/source combinations and generated-world class play. |
| 1.13 | Monk Search ability | Partial | Search duration/status behavior exists, with scene acceptance work in the working tree. | Integration/acceptance: complete native hidden-item discovery feedback and sustained class play. |
| 1.14 | Bard Identify ability | Partial | Identify can target owned items without a paid service; preview and recovery work exist. | Acceptance: integrate the WIP controls and qualify supported item locations in native play. |
| 1.15 | Barbarian Rage ability | Partial | Rage controller, resource admission and status/timing behavior exist in combat code. | Native parity/acceptance: full buff, expiry/recovery, death and restore behavior through ordinary controls. |
| 1.16 | Experience and level advancement | Partial | External XP thresholds, level growth, resource refill and stat-point award rules exist; native route earns XP. | Native parity/acceptance: sustained leveling, caps and reward attribution across classes/difficulties in campaign play. |
| 1.17 | Attribute allocation and class caps | Partial | Server-owned allocation and class maximums exist, with equipment-derived recalculation. | Acceptance: complete UI allocation and restore coverage during actual class progression. |
| 1.18 | Derived class/equipment resources | Partial | Equipment recalculates attributes, armor, life/mana, resistances and class modifiers. | Native parity: complete interacting affixes, class traits and equipment/action combinations. |
| 1.19 | Difficulty selection and scaling | Partial | Normal/Nightmare/Hell scaling rules and metadata exist. | Integration/acceptance: the native populated profile admits only Normal; qualify other difficulties and progression. |
| 1.20 | Player death and restart | Partial | Death modes, Gold loss/drop and Town restart exist; an authored Sorcerer lifecycle has evidence. | Integration/acceptance: native campaign death consequences, equipment/cursor interactions and restart/resume for supported modes. |
| 1.21 | Resurrection and cooperative lifecycle | Partial | Resurrection targeting, placement and beam/controller behavior exist. | Native parity/acceptance: ordinary multiplayer resurrection, occupancy, resources and cold continuity. |
| 1.22 | Native profile class admission | Partial | Explicit native population/progression profiles require canonical Rogue, single-player Normal and canonical fresh resources. | Implementation/integration: support other classes and valid initial preparation; UI selection alone does not enable them. |

Basis: [class definitions](../server/content/base/classes.tsv),
[loadouts](../server/content/base/class_loadouts.tsv),
[profile admission](../server/src/Devilution.Server.Diablo/Gameplay/DiabloAuthoritativeGameHostAdapterFactory.cs),
[progression](../server/src/Devilution.Diablo/DiabloPlayerProgression.cs),
[player state](../server/src/Devilution.Diablo/AuthoritativePlayerState.cs) and
[class/loadout tests](../server/tests/Devilution.Diablo.Tests/AuthoritativePlayerLoadoutCatalogTests.cs).

## 2. Combat, spells and statuses

Spell-family entries below mean that corresponding catalog and execution paths
exist. They do not imply that every source, target, class, simultaneous effect
and native action-frame combination is accepted in a complete campaign.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 2.1 | Melee attacks | Partial | Server melee intents, hit/damage rules, native phases and object-breaking paths exist. | Native parity/acceptance: weapon/class combinations, moving targets and complete generated-world melee play. |
| 2.2 | Bow attacks | Partial | Native Rogue bow combat passes on the Town/L1/L2 route. | Native parity/acceptance: broader actors, gear, difficulty, action interruptions and campaign use. |
| 2.3 | Arrow flight and collision damage | Partial | Distance, collision-time shared RNG, impact and in-flight cold continuation exist. | Native parity: remaining collision/target/geometry and simultaneous impact combinations. |
| 2.4 | Walking during combat and destination actions | Partial | Native destination walking, reservations and pending ranged follow-ups survive scoped cold-host tests. | Acceptance: complete ordinary approach/interrupt/retarget behavior across live class encounters. |
| 2.5 | Held entity-attack input | Complete | Committed negotiated-client controls stop on release/focus/identity change; focused cases and three actual held pairs pass. | None for this bounded input contract; combat/campaign success remains separate. |
| 2.6 | Buffered ranged repeat | Partial | An active bow action can retain a follow-up, with native timing and pending-shot restore coverage. | Acceptance: sustained ordinary gameplay beyond current bounded scenarios; L3 death alone does not prove a defect. |
| 2.7 | Attack/cast/hit/block action frames | Partial | Native-derived animation contexts, effect frames, recovery and queued state exist. | Native parity: complete tick/frame chaining, interruption and class/equipment timing combinations. |
| 2.8 | Hit chance and physical damage | Partial | Class/level/armor formulas, native RNG and fixed-point damage paths exist. | Native parity: remaining threshold, difficulty, gear and target-state combinations. |
| 2.9 | Resistance, immunity, block and recovery | Partial | Damage types, resistances, shield blocking and recovery modes are implemented. | Native parity/integration: combined player/monster/equipment effects and live class coverage. |
| 2.10 | On-hit equipment effects | Partial | Elemental damage, life/mana restoration, knockback and related flags have rule implementations and focused tests. | Native parity: complete effect ordering, eligible targets and concurrent/death interactions. |
| 2.11 | Target admission, PvP and friendly mode | Partial | Player/monster target identity and eligibility, friendly-mode and player damage paths exist. | Acceptance/parity: multiplayer combat, invalidation, area effects and friendly-fire combinations. |
| 2.12 | Learned, scroll, staff and item spell sources | Partial | Source resolution validates knowledge, owned item/charge identity and effect-frame consumption. | Native parity/acceptance: complete source selection, failure/refusal and UI behavior across spells. |
| 2.13 | Firebolt, Holy Bolt and Fireball | Partial | Projectile creation, travel, eligible-target and explosion paths exist. | Native parity/acceptance: full source/target/obstacle and generated encounter coverage. |
| 2.14 | Lightning, Charged Bolt, Chain Lightning and Nova | Partial | Bolt/controller families and native-derived targeting/RNG rules exist. | Native parity: multi-target ordering, branching/collision and concurrent controllers in live encounters. |
| 2.15 | Fire/Lightning Wall, Flame Wave, Inferno and Ring of Fire | Partial | Controllers create timed segments and area damage; Immolation behavior also exists. | Native parity/integration: geometry, lifetime, overlap and simultaneous actor/effect ordering. |
| 2.16 | Flash and Apocalypse | Partial | Layered Flash and target-scanning Apocalypse controllers exist with replay coverage. | Native parity/acceptance: all eligible actor, resistance, room and concurrent-effect conditions. |
| 2.17 | Blood Star, Bone Spirit and Elemental | Partial | Specialized damage, tracking/projectile and impact paths exist. | Native parity/acceptance: resource cost, target loss, movement and collision edge cases in actual play. |
| 2.18 | Healing, Heal Other, Mana and Magi | Partial | Resource spells, class scaling and target/resource admission paths exist. | Native parity/acceptance: complete class/equipment, cooperative targeting and boundary cases. |
| 2.19 | Mana Shield, Reflect and Etherealize | Partial | Protective/status/controller behavior and native-derived damage interactions exist. | Native parity: overlapping effects, expiration, lethal hits, equipment changes and recovery continuity. |
| 2.20 | Stone Curse and Berserk | Partial | Target search, admission, controller/status mutation and resistance logic exist. | Native parity/integration: actor-specific eligibility, AI transition, expiration and multiplayer ownership. |
| 2.21 | Infravision and Search | Partial | Timed status application and duration extension/expiry exist. | Integration/acceptance: discovery/visibility feedback and ordinary class/spell use across worlds. |
| 2.22 | Teleport, Phasing and Warp | Partial | Destination search, collision/occupancy admission and movement controllers exist. | Native parity/acceptance: campaign destinations, set-level constraints and multiplayer occupancy. |
| 2.23 | Town Portal spell | Partial | Portal creation/use and scoped authored purchase/cast/stash/cold-resume journey pass. | Integration/parity: native generated campaign portal ownership, return placement and multi-owner recovery. |
| 2.24 | Guardian and Golem casting | Partial | Guardian controllers and owner-bound Golem creation, statistics and RNG paths exist. | Integration/acceptance: generated-world admission and complete summon/controller lifecycle; see area 3. |
| 2.25 | Jester and Rage | Partial | Jester selects spell behavior and Rage has its own resource/timing controller. | Native parity/acceptance: random selection outcomes, class traits, duration and recovery combinations. |
| 2.26 | Hellfire rune effects | Partial | Native spell identities and consumable/projectile behaviors cover rune families. | Integration/parity: all fire/light/nova/immolation/stone placement, triggering, ownership and restore cases. |
| 2.27 | Simultaneous effect ordering and RNG ownership | Partial | Deterministic ticks, ordered actors/controllers and owned native seeds have focused comparisons. | Native parity: remaining simultaneous player, AI, projectile, status and death interactions. |
| 2.28 | Mid-action save/resume | Partial | Walk, bow, melee and effect-frame source state have scoped restore coverage. | Acceptance: live spell/status/controller combinations and every newly integrated native family. |
| 2.29 | Consumable execution after death | Complete | Queued consumables executing after death are refused before consumption, hydration or RNG mutation. | None for this refusal contract; complete death/restart gameplay remains in 1.20. |
| 2.30 | Telekinesis | Partial | Active spell data and remote item pickup, object operation and monster knockback paths exist. | Native parity/integration: all eligible targets, range/collision/refusal conditions, effects and retained-world use through ordinary controls. |

Basis: [combat execution](../server/src/Devilution.Diablo/DiabloCombatCommandProcessor.cs),
[combat rules](../server/src/Devilution.Diablo/AuthoritativeCombatRules.cs),
[spell behaviors](../server/content/base/spells.tsv),
[action state](../server/src/Devilution.Diablo/AuthoritativePlayerActionState.cs),
[held input](../godot-client/Devilution.Client.Protocol/HeldEntityAttackController.cs) and
[replay reference](replay-fixtures.md). A failed L3 bot run does not identify a
missing spell or establish a core combat bug.

## 3. Monsters, AI and summons

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 3.1 | Monster/type/missile/unique catalogs | Complete | Stable native identities and validated behavior, spawn and unique metadata are available. | None for catalog ingestion; native initial placement and simulation are separate capabilities. |
| 3.2 | Ordinary native actor materialization | Partial | Cathedral slot/type/unique provenance and admitted initialization are installed as live targets. | Integration/parity: admit the additional valid initial states needed by broader generated populations. |
| 3.3 | Initial-state admission limits | Partial | Adapter explicitly classifies invalid/unavailable rows, moving or petrified actors, nondefault goals and dormant reservations. | Implementation: support required valid states without treating rejected actors as absent AI implementations. |
| 3.4 | Initial emergence and reserved actors | Partial | Constrained skeleton SpecialStand and linked sarcophagus/barrel release paths exist. | Integration/parity: remaining emergence, Gargoyle-special and other reserved-state lifecycles. |
| 3.5 | Monster navigation and occupancy | Partial | Deterministic movement, collision, reservations and target approach exist. | Native parity: full family-specific cornering, avoidance, pathing and multi-actor occupancy behavior. |
| 3.6 | Active windows, off-level suspension and AI seeds | Partial | Native actors use active windows and owned seed/state; scoped off-level retention exists. | Native parity/acceptance: all activation, suspension, re-entry and multi-floor concurrent rules. |
| 3.7 | Zombie, skeleton and Goat melee families | Partial | Dedicated native AI/tuning and attack behavior exist. | Native parity/acceptance: complete goals/animations and generated encounters across levels/difficulties. |
| 3.8 | Ranged skeleton, Goat and Succubus families | Partial | Ranged selection, projectiles, retreat/attack behavior and focused comparisons exist. | Native parity/acceptance: obstacle/corner/target changes and complete family encounters. |
| 3.9 | Fallen and Scavenger families | Partial | Group morale, retreat, corpse search/healing and related behavior paths exist. | Native parity: full group/corpse/goal transitions and simultaneous encounter order. |
| 3.10 | Bat, Sneak/Hidden and Gargoyle families | Partial | Specialized movement, hiding and Gargoyle behavior are implemented in combat simulation. | Integration/parity: required initial special states and full activation/animation lifecycle. |
| 3.11 | Rhino, Snake and related charge families | Partial | Charge and specialized movement/damage paths exist. | Native parity/acceptance: collision, knockback, terminal action and generated encounter combinations. |
| 3.12 | Magma, Storm, FireMan, Counselor and other caster families | Partial | Native AI and missile/controller handling exist for many families. | Native parity: complete goal, projectile, spell and simultaneous-player conditions. |
| 3.13 | Hellfire AI families | Partial | Hork Demon, Lich/Arch Lich, FireBat and other Hellfire AI metadata/behavior paths exist. | Integration/parity: Hive/Crypt population, special spawning and full encounter/cold-state coverage. |
| 3.14 | Unique monsters and packs | Partial | Unique identities, placement/pack metadata and modified actor behavior exist. | Integration/parity: every supported unique's native placement, entourage, attributes and campaign encounter. |
| 3.15 | Quest-dependent monster dialogue and behavior | Partial | Dialogue state and special gates, including Lazarus-minion behavior, exist. | Integration/acceptance: quest-selected actor admission, dialogue-to-combat transitions and retained outcomes. |
| 3.16 | Death, corpses, XP and monster loot | Partial | Native two-floor play proves kills, XP and retained corpses; L1 earned loot is observed. | Native parity/integration: complete death-side effects and drop families; the passed L2 run observed no drop. |
| 3.17 | One-owner Golem lifecycle | Partial | Owner-bound spawn, replacement of that owner's prior minion, native statistics and seed advancement exist. | Native parity/acceptance: full targeting, damage credit, death and generated-world retention. |
| 3.18 | Multiple simultaneous summon owners | Unverified | Owner filtering and secondary-caster replay support exist. | Acceptance/parity: simultaneous owners, replacement/allocation, kill credit and fresh-host continuation together. |
| 3.19 | Guardian and other controlled effects | Partial | Guardian targeting/projectile controllers and Berserk-controlled actor paths exist. | Native parity/integration: owner removal, target changes, simultaneous controllers and campaign recovery. |
| 3.20 | Boss encounters | Partial | Butcher, Skeleton King, Diablo and Hellfire boss rules/metadata have implementation pieces. | Integration/acceptance: native quest placement, full encounters, rewards and victory consequences; see areas 7–8. |

Basis: [type data](../server/content/base/monster_types.tsv),
[actor adapter](../server/src/Devilution.Diablo/AuthoritativeCathedralMonsterRuntimeAdapter.cs),
[native population policy](../server/src/Devilution.Diablo/AuthoritativeCathedralNativePopulationPolicy.cs),
[AI/combat/summons](../server/src/Devilution.Diablo/DiabloCombatCommandProcessor.cs) and
[unique placement](../server/src/Devilution.Diablo/AuthoritativeUniqueMonsterPlacementCatalog.cs).

## 4. Items and economy

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 4.1 | Stable item definitions and native classification | Complete | Item IDs, topology, tags, cursors and native effect metadata have validated catalogs. | None for those catalog contracts; generation and use are scored below. |
| 4.2 | Seeded base-item generation | Partial | Native RNG, item-level rules, quality selection and deterministic item state exist. | Native parity/integration: full level/difficulty/source combinations in generated campaigns. |
| 4.3 | Prefixes, suffixes and magical properties | Partial | Affix admission, selection, seeded ranges and value recalculation exist. | Native parity: complete interacting properties and generation contexts, including Hellfire variants. |
| 4.4 | Unique items and shared uniqueness history | Partial | Unique definitions, candidate rules and shared generated-floor history exist. | Integration/parity: complete campaign uniqueness, quest rewards and multiplayer acquisition rules. |
| 4.5 | Native death-time loot generation | Partial | Native-derived depth-owned drop composition is connected to the scoped live item boundary. | Native parity/acceptance: remaining monster/source/difficulty families and observed later-floor drop journeys. |
| 4.6 | Initial, theme and object-generated ground items | Partial | Native slot/pool generation and chest/sarcophagus/barrel item creation exist. | Integration/parity: all placement sources, capacity branches and quest/environment-created items. |
| 4.7 | Ground pickup, approach and drop | Partial | Authoritative item identity, pickup/drop and actual earned-item controls exist. | Acceptance/parity: full cursor/topology/occupancy, approach and simultaneous pickup cases. |
| 4.8 | Inventory grid and item swaps | Partial | Multi-cell placement, capacity refusal, swapping and retained topology exist. | Native parity/acceptance: full drag/swap/overflow combinations through ordinary controls. |
| 4.9 | Equipment requirements and hand combinations | Partial | Slot admission, stat requirements, two-handed/dual-wield rules and recalculation exist. | Native parity/acceptance: all class/weapon/shield/jewelry combinations and loss-of-requirement transitions. |
| 4.10 | Belt placement and quick use | Partial | Belt slots, transfers and quick-use intents are server-owned. | Acceptance: full class/consumable/empty-slot and pending-action behavior in sustained play. |
| 4.11 | Cursor-held item lifecycle | Partial | Held identity, inventory/world transfer, overflow and death handling exist. | Native parity/integration: all service, equipment, cancellation and cross-level interactions. |
| 4.12 | Stash deposit and withdrawal | Partial | Authored and scoped native stash/equipment/recovery journeys exist. | Acceptance/parity: broader item layouts, capacity, class and multiplayer service combinations. |
| 4.13 | Gold inventory topology and spending | Partial | Native-order spend, stack top-off/creation, sale proceeds and capacity refusal exist. | Native parity: complete denomination, cursor, overflow, death and campaign-economy combinations. |
| 4.14 | Approved inactive Gold-slot preservation | Complete | The approved correction retains updated Gold value/cursor when a ground stack leaves the active pool. | None for that correction; broader inactive-Gold changes remain separately scoped in working memory. |
| 4.15 | Native active Gold value/cursor projection | Partial | Admitted stack identities, denomination cursors, revaluation and cold restore exist. | Native parity: full MakeGoldStack scalar equivalence is not claimed. |
| 4.16 | Potions, elixirs and consumable effects | Partial | Resource use, permanent/stat effects, item consumption and dead-player refusal exist. | Native parity/acceptance: all content effects, class modifiers and resource/equipment boundaries. |
| 4.17 | Books, scrolls, staves and charges | Partial | Book admission/learning, cast-source identity and charge/source consumption exist. | Native parity/acceptance: all spell-source combinations, item loss and effect-frame restore behavior. |
| 4.18 | Durability and weapon break | Partial | Durability, paid/class repair and related item mutation paths exist. | Implementation/parity: ordered weapon-break/action completion and queued-intent behavior; retain its documented narrow hold. |
| 4.19 | Buy/sell validation, quotes and transactions | Partial | Prices, service hints, capacity checks and atomic mutation paths exist. | Native parity/acceptance: full native vendor lifecycle, concurrent state and campaign use. |
| 4.20 | Griswold standard/premium stock | Partial | Role-bound store catalogs and purchase/sale/repair behavior exist. | Implementation/parity: native initial stock/RNG, premium updates and level/return lifecycle. |
| 4.21 | Adria stock and paid recharge | Partial | Store roles, spell-item offers, recharge and resource service rules exist. | Native parity/integration: native stock generation, refresh and full source/price combinations. |
| 4.22 | Pinned Pepin healing/potion route | Complete | Native Rogue route purchases healing potions and receives ordinary Town healing with retained state. | None for that pinned journey; generated native healer stock is not thereby complete. |
| 4.23 | Cain identification service | Partial | Role/price/item admission and identification mutation exist. | Native parity/acceptance: full item classes, UI selection, refusal and service lifecycle. |
| 4.24 | Wirt offer lifecycle | Unverified | Towner/store foundations exist. | Implementation/parity assessment: establish native offer generation, viewing cost, refresh, purchase and retained state. |
| 4.25 | Quest items, combinations and rewards | Partial | Quest-effect/combination catalogs and authored turn-in/reward mutations exist. | Integration/acceptance: generated quest acquisition, delivery, reward placement and repeat refusal. |
| 4.26 | Death drops and lost-item recovery | Partial | Gold loss/drop/pickup and cursor-held death handling exist. | Native parity/acceptance: supported mode-specific equipment/item drops and recovery journeys. |
| 4.27 | Earned gear and cold-resume baseline | Complete | Passed Rogue Town/L1/L2 route earns and equips L1 gear, then restores and revisits retained state. | None for that baseline; other classes, floors and full economy remain open. |

Basis: [item catalog/generation](../server/src/Devilution.Diablo/AuthoritativeItemCatalog.cs),
[item rules/tests](../server/tests/Devilution.Diablo.Tests/AuthoritativeItemCatalogTests.cs),
[store transitions](../server/src/Devilution.Diablo/DiabloStoreCommandProcessor.cs),
[Gold topology](../server/src/Devilution.Diablo/DiabloGoldInventory.cs),
[native item ownership](../server/src/Devilution.Diablo/AuthoritativeCathedralItemRuntime.cs) and
[service lifecycle](../server/tests/Devilution.Diablo.Tests/NativeStoreTransitionLifecycleTests.cs).

## 5. Generated world and depth progression

Native generation means reproducing the family's seed-driven layout, population
and state transitions. A supplied DUN loader or generic grid is useful existing
work, but does not supply that generator. Geometry-only Complete entries below
are explicitly narrower than a playable campaign.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 5.1 | Generic procedural-grid foundation | Complete | Managed connected-grid generation and validated procedural-level definitions exist. | None for this generic foundation; native family synthesis is tracked separately. |
| 5.2 | Supplied DUN/TIL/SOL loading | Complete | Catalogs validate supplied native maps, tile definitions and collision layers. | None for supported loading contracts; supplying a map does not reproduce its native seed generation. |
| 5.3 | Ordinary Cathedral L1 coarse generation | Complete | Bounded native comparisons cover coarse maps, continuation RNG and successful-attempt restoration. | None for the compared ordinary generation scope; quest variants remain separate. |
| 5.4 | Cathedral fine geometry expansion | Complete | TIL/SOL expansion produces native fine coordinates and flags, with MAIN/PREV comparison coverage. | None for the compared geometry scope; exact shared vision is separate. |
| 5.5 | Ordinary Cathedral L1–L4 geometry | Complete | Four-depth generator/materializer has bounded seed/layer/RNG/stair oracle coverage. | None for that geometry prerequisite; full populated four-floor gameplay is not established. |
| 5.6 | Cathedral stair and Town bindings | Partial | Reciprocal Cathedral depth routes and L1-to-Town travel are bound. | Integration: L4-to-Catacombs L5 and all campaign-family transitions. |
| 5.7 | Cathedral theme-room placement | Complete | Detached CreateThemeRooms comparisons cover the ordinary Cathedral theme types and static-light cases. | None for this detached stage; composed lifecycle and interactions remain separate. |
| 5.8 | Theme-item setup and placement | Complete | Bounded original-Diablo SP Normal comparisons cover setup, selection, placement, pools and RNG. | None for that compared stage; full runtime item families remain in area 4. |
| 5.9 | Composed native population phases | Partial | Controlled object/theme/monster/item phase composition has native comparisons. | Integration/parity: full loader order, hero/store/quest context and native campaign seed selection. |
| 5.10 | Initial object/trap placement | Complete | Bounded native comparisons cover object initialization and loaded trap metadata. | None for that initialization scope; operation and wider family coverage are area 6. |
| 5.11 | Initial Cathedral monster placement | Complete | Ordinary Cathedral InitMonsters state has controlled native comparisons across depths. | None for that compared stage; admission limits and live AI remain in area 3. |
| 5.12 | Initial item placement | Partial | Item slots, setup and RNG have native-derived generation paths and comparisons. | Integration/parity: remaining placement/source conditions and complete live population coverage. |
| 5.13 | Cold pre-join native loader | Partial | Externalized metadata and fresh-cold generation reach the defined pre-join boundary. | Integration: this generator alone is not the full joined, ready-to-tick campaign loader. |
| 5.14 | Joined Town/L1/L2 restore and revisit | Complete | Passed native Rogue route restores at a fresh host/client and revisits both retained floors. | None for that exact route; this does not establish L3/L4 or quest-enabled loading. |
| 5.15 | Lazy floor owners and retained mutations | Partial | Explicit L1–L4 owners, first-entry contexts, native walking and cohort state exist. | Acceptance: live retained traversal and cold resume across every supported floor and mutation family. |
| 5.16 | Complete native Cathedral L1–L4 journey | Partial | Town/L1/L2 passes; backend four-floor support exists. Latest L3 run dies before a kill/living checkpoint. | Acceptance: complete living L3/L4 continuation, fresh-host restore and retained-world return. |
| 5.17 | Catacombs L5–L8 native generation | Not started | Generic/supplied-map catalogs and other prerequisites exist; no managed native family generator was identified. | Implementation/integration: native seeded geometry, population, L5 entry/return and retained floor owners. |
| 5.18 | Caves L9–L12 native generation | Not started | Supplied-map and content foundations exist. | Implementation/integration: native seeded Caves synthesis, placement, encounters and campaign transitions. |
| 5.19 | Hell L13–L16 native generation | Not started | Level, monster and quest metadata can describe Hell content. | Implementation/integration: native seeded Hell synthesis, population, final-level topology and retention. |
| 5.20 | Hive native generation | Not started | Hellfire level and supplied-fragment metadata exist. | Implementation/integration: native seeded Hive layouts, population and connected campaign travel. |
| 5.21 | Crypt native generation | Not started | Crypt identities and Uber Room/Cornerstone fragment prerequisites exist. | Implementation/integration: native seeded Crypt layouts, room insertion, population and retained progression. |
| 5.22 | Campaign seed and family selection | Partial | Explicit per-depth inputs, profile seeds and first-entry generation exist. | Native parity/integration: ordinary campaign seed derivation, depth selection and inter-family lifecycle. |
| 5.23 | Quest-selected pieces and level variants | Partial | Quest-piece origins, fragment catalogs and selected supplied set maps exist. | Integration/parity: quest-enabled generation, placement/removal, trigger topology and campaign ownership. |
| 5.24 | Collision, transparency, light and vision semantics | Partial | Native SOL/layer/light/vision metadata and scoped activation exist. | Native parity: exact shared vision remains open; the current level contract conservatively includes movement blockers. |

Basis: [procedural levels](../server/src/Devilution.Diablo/AuthoritativeProceduralLevelCatalog.cs),
[DUN loading](../server/src/Devilution.Diablo/AuthoritativeNativeDunLevelCatalog.cs),
[Cathedral generation](../server/src/Devilution.Diablo/AuthoritativeCathedralDepthGenerator.cs),
[depth bindings](../server/src/Devilution.Diablo/AuthoritativeCathedralDepthRuntimeCatalog.cs),
[full population](../server/src/Devilution.Diablo/AuthoritativeCathedralFullPopulationGenerator.cs),
[fresh loader](../server/src/Devilution.Diablo/AuthoritativeCathedralFreshLevelGenerator.cs) and
[native Cathedral evidence](test-history.md#native-cathedral-baselines).

## 6. Native objects and environment

Placement and operation are separate. Native theme/object catalogs already
include more families than the live population owner can operate. For the
Not started interaction entries, generic authored effects and native placement
remain available prerequisites; the missing deliverable is the family-specific
native interaction/effect/RNG/retention path, not all object infrastructure.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 6.1 | Generic authored object effects | Partial | Object effects, quest transitions, item gates and follow-up world mutations can be authored. | Native parity/integration: bind each required native family's actual lifecycle and effect order. |
| 6.2 | Object identity, item gates and links | Partial | Stable identities, item requirements and linked-object references are validated. | Integration: complete generated-family mappings, linked activation and cross-player interaction rules. |
| 6.3 | Cathedral doors | Complete | Scoped native/runtime coverage includes discovery, open/close, occupied-close handling and retained geometry/state. | None for supported Cathedral-door scope; other dungeon doors and rendering remain separate. |
| 6.4 | Ordinary untrapped Cathedral chests | Complete | Supported chest kinds have native loot/empty/full-pool branches, one-way opening and cold retention. | None for those kinds; trapped and other chest variants are not included. |
| 6.5 | Trapped chest variants | Partial | Ordinary chest types with supported Arrow-trap links are admitted; other native chest/trap variants are refused. | Native parity/acceptance: qualify linked-chest application branches and implement remaining native variant/effect/RNG behavior. |
| 6.6 | Supported Cathedral sarcophagi | Complete | Native/owner tests cover loot, empty outcomes and reserved skeleton release; scoped cold state passes. | None for supported operation scope; app evidence does not exercise every linked-release branch. |
| 6.7 | Ordinary native barrels | Complete | Supported break, animation, collision, loot/empty and reserved-actor paths have focused coverage and scoped retention. | None for that family scope; the application seed's empty barrel does not prove loot branches. |
| 6.8 | Explosive native barrels | Complete | Native comparisons cover explosion ordering/damage, linked effects and retained break state. | None for the compared owner scope; blast-victim branches are not claimed by the scoped app run. |
| 6.9 | Arrow wall traps | Complete | Supported left/right Arrow traps have disarm/operation, one-shot flight/impact and cold-retention coverage. | None for that scoped type; other wall traps remain separate. |
| 6.10 | Flame-trap operation | Not started | Native object/placement prerequisites exist; no corresponding managed native operation owner was found. | Implementation/parity: activation, flame lifetime/damage, lever linkage, RNG and retention. |
| 6.11 | Lightning/other wall-trap operation | Not started | Arrow trap support does not provide a native owner for the other damage families. | Implementation/parity: family-specific trigger/projectile/damage and saved one-shot state. |
| 6.12 | Initial static object lighting | Complete | Dedicated native comparison covers supported initial-object lighting. | None for that initial-data scope; dynamic light and rendered parity remain separate. |
| 6.13 | Shrine/fountain/cauldron theme placement | Partial | Theme availability and population metadata include these families; shrine identity is projected. | Integration/parity: complete generated placement contexts plus the operation owners below. |
| 6.14 | Blood Fountain operation | Not started | Native placement and generic healing effects exist; no native interaction owner was found. | Implementation/parity: health effect, repeat rules, RNG/context and cold state. |
| 6.15 | Purifying Spring operation | Not started | Native placement and generic resource effects are prerequisites. | Implementation/parity: native mana effect, admission/repeat rules and retained state. |
| 6.16 | Murky Pool operation | Not started | Object/theme metadata can describe the native object. | Implementation/parity: native effect/status duration, repeat/admission rules and recovery. |
| 6.17 | Fountain of Tears operation | Not started | Native placement and player-attribute machinery exist. | Implementation/parity: native random attribute transfer, limits, RNG consumption and persistence. |
| 6.18 | Attribute-changing native shrines | Not started | Native shrine placement and generic attribute changes exist. | Implementation/parity: Mysterious, Eerie, Abandoned, Creepy, Quiet and Tainted effects, limits and multiplayer scope. |
| 6.19 | Equipment-changing native shrines | Not started | Item/durability/identification systems are available prerequisites. | Implementation/parity: Hidden, Gloomy, Weird, Stone, Religious and Glimmering native item-selection/effect order. |
| 6.20 | Spell/resource-changing native shrines | Not started | Spell levels, resource maxima and item conversion primitives exist. | Implementation/parity: Enchanted, Fascinating, Sacred, Ornate, Cryptic, Eldritch, Divine, Spiritual and Magical effects. |
| 6.21 | World/cooperative native shrines | Not started | Exploration, teleport and shared player/world state exist. | Implementation/parity: Thaumaturgic, Holy, Spooky and Secluded effects, shared-player consequences and retained mutations. |
| 6.22 | Hellfire native shrine effects | Not started | Hellfire metadata and generic combat/resource effects are available. | Implementation/parity: Oily, Glowing, Mendicant, Sparkling, Town, Shimmering, Solar and Murphy's effects. |
| 6.23 | Goat-shrine interaction | Not started | Native shrine/theme placement exists. | Implementation/parity: random effect selection, availability rules, linked state and one-use retention. |
| 6.24 | Cauldron interaction | Not started | Cauldron theme/placement metadata exists. | Implementation/parity: native random shrine-effect selection, RNG order and retained consumed state. |
| 6.25 | Weapon racks | Not started | Presentation/category and item generation prerequisites exist; no native operation owner was found. | Implementation/parity: native item selection, placement/capacity, RNG and repeat refusal. |
| 6.26 | Armor stands | Not started | Object classification and equipment generation prerequisites exist. | Implementation/parity: native armor selection, object mutation, RNG and cold retention. |
| 6.27 | Quest books, levers and pedestals | Partial | Authored sequences, item gates, quest progress and map mutations exist. | Integration/parity: each generated quest mechanism, operation order and persistent geometry/rewards. |
| 6.28 | Environment geometry/collision mutations | Partial | Layer, tile, collision and transparency changes can be authored and validated. | Native parity/integration: complete family-specific changes and campaign re-entry/multiplayer behavior. |
| 6.29 | Native DUN fragment objects | Partial | Base/Hellfire object IDs and fragment placements can be loaded and validated. | Integration: insert and own them at native quest/campaign boundaries, with live operation support. |
| 6.30 | Set-level object placement and lifecycle | Partial | Supplied set maps have cataloged object placement. | Integration/acceptance: quest-selected admission, linked changes, return and cold/multiplayer retention. |
| 6.31 | Cross-player one-use and refusal behavior | Partial | Supported owners enforce scoped operation/refusal and saved mutations. | Acceptance/parity: simultaneous operations and rejected-command atomicity across every added family. |
| 6.32 | Native bookcases and bookstands | Not started | Object/loot metadata and authored book effects exist; no native operation owner was identified. | Implementation/parity: native loot/book selection, operation frames, RNG and retained state. |

Basis: [native owner](../server/src/Devilution.Diablo/AuthoritativeCathedralNativePopulationPolicy.cs),
[trap owner](../server/src/Devilution.Diablo/AuthoritativeCathedralNativePopulationTraps.cs),
[item/object mutation](../server/src/Devilution.Diablo/AuthoritativeCathedralItemRuntime.cs),
[generic objects](../server/src/Devilution.Diablo/AuthoritativeWorldObject.cs),
[theme placement](../server/src/Devilution.Diablo/AuthoritativeCathedralThemeGenerator.cs) and
[native reference operations](../legacy-native/game/objects/objects.cpp).
Object art/animation completion is tracked in area 11.

## 7. Town, quests and set levels

The base tables contain 16 quest-type definitions, six authored quest route/state
rows and nine initial-state rows; these are different kinds of support. A named
quest's Partial status below identifies real metadata/behavior prerequisites,
not an assertion that its complete native chain already runs. The current native
Cathedral progression profile is quest-disabled.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 7.1 | Base quest identity/type catalog | Complete | All 16 native base quest identities have validated type, level and related metadata. | None for catalog identity; authored and playable chains are separate entries. |
| 7.2 | Native single-player quest-pool selection | Complete | Ordered pools, literal selection and versioned native-derived selection have characterization. | None for the bounded selection contract; selected quests must still enter native generation/play. |
| 7.3 | Quest state transitions and journal data | Partial | Active/completed/unavailable/logged state, variables and selected transitions exist. | Native parity/integration: every trigger, journal update and completion/reward transition. |
| 7.4 | Dialogue stages and sequence variants | Partial | Stage, greeting, route-message and sequence-variant catalogs exist. | Native parity/acceptance: complete quest dialogue order, repetition and state-dependent presentation. |
| 7.5 | Towner interaction and turn-in | Partial | Roster-bound proximity/item admission, authored turn-ins and reward paths exist. | Integration/acceptance: all NPC/quest combinations and correct native item/reward placement. |
| 7.6 | Town roster and nonvendor interactions | Partial | Towner/greeting metadata and functional dialogue/stash/service controls exist. | Native parity/integration: complete Ogden, Gillian, Farnham, Cain, vendors and quest-specific actors/conditions. |
| 7.7 | Town service entry and exit lifecycle | Partial | Native role/range/held-item gates, healing and cross-level vendor-session cleanup exist. | Native parity/acceptance: full stock refresh, service dialogue and supported campaign transitions; economy is area 4. |
| 7.8 | Quest-enabled native profile | Partial | Native quest-selection/state foundations exist; current playable Cathedral profiles deliberately disable quests. | Integration: construct and retain quest-enabled worlds with matching placement, actors, objects and transitions. |
| 7.9 | The Magic Rock | Partial | Authored route/turn-in data includes the rock and Empyrean Band reward. | Integration/acceptance: native quest selection/placement, acquisition, Griswold turn-in and retained reward. |
| 7.10 | Black Mushroom | Partial | Authored quest/turn-in data includes the brain and Spectral Elixir, with supporting dialogue metadata. | Integration/parity: the complete native multi-stage item/NPC chain and reward consequences. |
| 7.11 | Gharbad the Weak | Partial | Quest type/state and monster/dialogue prerequisites exist. | Integration/parity: staged dialogue, item gifts, betrayal/combat and retained completion. |
| 7.12 | Zhar the Mad | Partial | Quest type/state and encounter/dialogue support exist. | Integration/parity: native placement, conversation/gift, provocation, combat and completion. |
| 7.13 | Lachdanan | Partial | Quest metadata/state and item/reward systems exist. | Integration/acceptance: Golden Elixir acquisition/turn-in, native dialogue and retained reward/completion. |
| 7.14 | Diablo | Partial | Boss behavior and kill/quest/DiabloKillLevel progression logic exist. | Integration/acceptance: native L16 encounter, final-level mutations and complete ending/victory consequences. |
| 7.15 | The Butcher | Partial | Quest type/state, boss AI and encounter metadata exist. | Integration/acceptance: quest-selected room/door trigger, actual boss fight, reward and retained completion. |
| 7.16 | Ogden's Sign | Partial | Quest state/type and towner/monster/item prerequisites exist. | Integration/parity: native sign placement, Snotspill/Ogden branches, reward and map-state consequences. |
| 7.17 | Halls of the Blind | Partial | Authored quest row and object/world-mutation foundations exist. | Integration/parity: native book, geometry reveal, room population/reward and retained completion. |
| 7.18 | Arkaine's Valor | Partial | Authored quest row, item gates and object effects exist. | Integration/parity: native blood-stone/pedestal sequence, room changes and unique reward. |
| 7.19 | Anvil of Fury | Partial | Authored anvil turn-in and Griswold's Edge reward data exist. | Integration/acceptance: native generated island/placement, acquisition, turn-in and retained reward. |
| 7.20 | Warlord of Blood | Partial | Quest type/state, encounter and linked-object prerequisites exist. | Integration/parity: native book/room gates, boss encounter, armory loot and completion. |
| 7.21 | The Curse of King Leoric | Partial | Skeleton King AI, quest state and supplied set-map/object links exist. | Integration/acceptance: quest-selected entry/return, entourage/boss fight and retained reward/completion. |
| 7.22 | Poisoned Water Supply | Partial | Quest type/state and poisoned-water set-level identity exist. | Implementation/integration: complete native map/population, purification changes, reward and Town return. |
| 7.23 | The Chamber of Bone | Partial | Authored quest row and supplied Bone Chamber map/object data exist. | Integration/parity: native quest activation, chamber/lever/book behavior, rewards and return. |
| 7.24 | Archbishop Lazarus / Vile Betrayer | Partial | Quest state, supplied set-map/object data and special actor gates exist. | Integration/parity: native portal/sequence, encounter, return and final-level unlock consequences. |
| 7.25 | Quest items and repeat-safe rewards | Partial | Item-effect/combination catalogs and authored reward/turn-in mutations exist. | Native parity/acceptance: acquisition/drop/turn-in order, capacity refusal, duplicate prevention and recovery. |
| 7.26 | Quest-selected set pieces and map replacement | Partial | Origins, fragments, mutations and set-level placement can be described. | Integration/parity: actual native generator selection, placement and irreversible world changes. |
| 7.27 | Supplied base set-level map coverage | Partial | Skeleton King, Bone Chamber and Vile Betrayer have three cataloged supplied maps. | Implementation/integration: remaining required set-map availability and complete quest-driven use. |
| 7.28 | Set-level entry and return ownership | Partial | Map/object/warp foundations and generic travel exist. | Integration/acceptance: native quest-driven entry/return, spawn placement, retained mutations and multiplayer ownership. |
| 7.29 | Quest-gated portals and warps | Partial | Required-quest metadata and authored portal journeys exist. | Integration/parity: every quest unlock/close/return condition in native campaigns. |
| 7.30 | Town shortcuts and depth unlocks | Partial | Warp/visited-level metadata and selected Town/Cathedral travel exist. | Integration/acceptance: supported dungeon-family unlocks, entry positions and persisted progression. |
| 7.31 | Ending, victory and difficulty progression | Partial | Diablo completion/kill-level logic is present; it is not a full ending implementation. | Integration/acceptance: final encounter consequences, ending flow and supported post-victory/difficulty transitions. |
| 7.32 | Quest retention and multiplayer decisions | Partial | Quest state is included in module/cohort snapshots and authored transitions exist. | Native parity/acceptance: selected-world quests, shared/individual consequences, join/return and fresh-host continuation. |

Basis: [quest types](../server/content/base/quest_types.tsv),
[authored quest rows](../server/content/base/quests.tsv),
[initial states](../server/content/base/quest_states.tsv),
[quest state](../server/src/Devilution.Diablo/AuthoritativeQuestState.cs),
[set maps](../server/content/base/set_level_maps.tsv),
[towner interactions](../server/src/Devilution.Diablo/AuthoritativeQuestTownerInteractionCatalog.cs) and
[world commands](../server/src/Devilution.Diablo/DiabloWorldCommandProcessor.cs).

## 8. Hellfire progression

Hellfire has eight quest-type definitions, three authored quest rows and eight
initial-state rows (including Lachdanan cross-game state). These counts do not
mean eight complete scripted quest chains. Base/Hellfire catalog composition is
already implemented and should be reused.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 8.1 | Hellfire content composition | Complete | A separate validated pack supplies Hellfire item/monster/quest/object/level data and overrides. | None for the composition boundary; a complete playable expansion is separate. |
| 8.2 | Hive/Crypt level identities and map types | Complete | Hellfire levels and Nest/Crypt map types are represented in validated catalogs. | None for identity/type recognition; native generation is not supplied by these entries. |
| 8.3 | Native Hive generation and entry | Not started | Level, population and quest prerequisites exist; no managed native seeded Hive generator was identified. | Implementation/integration: Hive geometry/population, entrance, depth travel and retention; see 5.20. |
| 8.4 | Native Crypt generation and entry | Not started | Crypt/fragment prerequisites exist; no managed native seeded Crypt generator was identified. | Implementation/integration: Crypt geometry/population, entrance, depth travel and retention; see 5.21. |
| 8.5 | Hellfire monster population and encounters | Partial | Native types, spawns, uniques and many AI behavior paths exist. | Integration/parity: complete Hive/Crypt placement, special states, bosses and actual retained encounters. |
| 8.6 | Hellfire items, affixes and native effects | Partial | Item/effect/topology catalogs and focused behavior tests exist. | Native parity/integration: complete generated drops/economy, class combinations and quest rewards. |
| 8.7 | Hellfire spells and runes | Partial | Many spell/controller/consumable rules and bounded native/replay comparisons exist. | Native parity/acceptance: all source/target/effect combinations and ordinary expansion gameplay; see area 2. |
| 8.8 | Monk, Bard and Barbarian expansion play | Partial | Class definitions, traits, loadouts and abilities exist. | Integration/acceptance: admitted native expansion profiles and complete class progression; see area 1. |
| 8.9 | Hellfire quest identity/state metadata | Complete | Eight native quest identities and related initial-state/type bindings are represented. | None for metadata ingestion; the named quest routes below remain incomplete. |
| 8.10 | Farmer's Orchard and Hive entrance | Partial | Quest/type/state, Rune Bomb and Town-warp/object prerequisites exist. | Integration/parity: native entrance-opening chain, Hive travel and retained completion. |
| 8.11 | Grave Matters and Crypt entrance | Partial | Quest/type/state and map/item metadata exist. | Integration/parity: native entrance conditions, acquisition/use chain and retained Crypt access. |
| 8.12 | Little Girl | Partial | Authored Theodore turn-in/reward data and supporting quest/towner metadata exist. | Integration/acceptance: native quest placement, rescue/acquisition, turn-in and retained reward. |
| 8.13 | Wandering Trader | Partial | Quest identity/state and towner/item foundations exist. | Integration/parity: native item delivery, dialogue, reward and campaign-state progression. |
| 8.14 | The Defiler | Partial | Boss AI/monster and quest metadata exist. | Integration/acceptance: generated Hive encounter, native completion/reward/drop chain and return. |
| 8.15 | Na-Krul encounter and release | Partial | Authored sequenced objects, boss spawn follow-up and quest-completing lever exist. | Native parity/integration: Crypt room/door/book effects, release conditions, actual boss fight and retained outcome. |
| 8.16 | Cornerstone of the World | Partial | Cornerstone fragment, quest identity and item-anchor prerequisites exist. | Integration/parity: native room insertion and supported item placement/retrieval/persistence behavior. |
| 8.17 | Jersey's Jersey | Partial | Quest type/state, item/towner and dialogue prerequisites exist. | Integration/acceptance: native variant chain, item delivery, rewards and retained completion. |
| 8.18 | Cornerstone/Uber Room fragment insertion | Partial | Supplied fragment/object catalogs contain those rooms and placement data. | Integration/parity: seed/quest-selected insertion, connectivity and native collision/light changes. |
| 8.19 | Hellfire towners and dialogue | Partial | Hellfire-specific interaction, route-message and dialogue rows compose with base content. | Integration/acceptance: complete NPC appearance/availability, conversations and quest turn-ins. |
| 8.20 | Hellfire quest/environment mutations | Partial | Authored world changes and Na-Krul sequences have data/runtime foundations. | Native parity/integration: full native geometry, collision, activation order and cold/multiplayer state. |
| 8.21 | Complete Hellfire campaign and resume | Partial | Expansion catalogs and individual behavior pieces exist. | Integration/acceptance: complete native Hive/Crypt quest/boss progression with earned resources and fresh-host retention. |
| 8.22 | Expansion and hidden-class mode policy | Partial | Availability metadata distinguishes base, Hellfire and hidden classes/content. | Integration/parity: expose supported choices consistently and qualify base/expansion saves, rules and campaign transitions. |

Basis: [Hellfire pack](../server/content/hellfire/),
[quest types](../server/content/hellfire/quest_types.tsv),
[authored quest rows](../server/content/hellfire/quests.tsv),
[initial states](../server/content/hellfire/quest_states.tsv),
[objects/sequences](../server/content/hellfire/objects.tsv),
[fragments](../server/content/hellfire/native_dun_fragments.tsv) and
[native item effects](../server/src/Devilution.Diablo/AuthoritativeNativeItemHellfireEffectCatalog.cs).

## 9. Persistence and multiplayer

Complete entries here are limited to supported schema versions, registered
current state, the existing file-backed store or the specifically named recovery
scenario. They do not certify arbitrary future gameplay fields, save backends,
concurrent campaigns or filesystem/platform failure combinations.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 9.1 | Supported compatibility player-save codecs | Complete | Versioned readers/writers and supported legacy player-save forms are retained. | None for that codec boundary; full release/upgrade compatibility qualification is in area 12. |
| 9.2 | Canonical current-state snapshots | Complete | Canonical player/world projections and hashes support deterministic replay and state comparison. | None for the registered current projection contract; future campaign fields need explicit inclusion. |
| 9.3 | Complete-cohort capture contract | Complete | Current capture covers participants, world/geometry, RNG, command cursors, stores, events and registered pending state. | None for current registered state; each added gameplay family must extend/verify its capture. |
| 9.4 | Cohort codec and integrity validation | Complete | Supported cohort versions validate state graphs and integrity before restore. | None for those formats; new formats and deployment backends require their own compatibility evidence. |
| 9.5 | Restore identity and detached validation | Complete | Content/ruleset identity and detached candidates are checked before installation. | None for the existing restore-validation contract; new profile identities must participate. |
| 9.6 | Durable combined checkpoint publication | Partial | State and command receipts publish together, with compatibility files and transactional/failure coverage. | Acceptance: broader filesystem, interrupted-write and platform qualification. |
| 9.7 | Save/execute synchronization boundary | Complete | Current host capture shares its simulation barrier with commands and autonomous ticks. | None for this host boundary; preserve it in additional execution paths. |
| 9.8 | Durable command identity and retry recovery | Complete | Supported receipt/sequence state survives restart and duplicate retries retain their prior result. | None for that frozen identity contract; broader fault/soak coverage remains separate. |
| 9.9 | Restored participant binding | Complete | Valid identities bind restored participants; invalid candidates fail before registration. | None for the supported binding policy; broader join/lifecycle acceptance remains below. |
| 9.10 | Two-participant authored fresh-host recovery | Complete | Scoped two-client recovery restores participant identity/state and ordered receipts. | None for that scenario; a full multiplayer campaign is not claimed. |
| 9.11 | Native Rogue Town/L1/L2 cold resume | Complete | Passed route matches restored player/RNG/cursor/two-floor world before input and retains revisit mutations. | None for that exact route; other classes, floors and quests remain separate. |
| 9.12 | Native L3/L4 retained gameplay recovery | Partial | Four-floor owners and pending-state restore exist; latest L3 run dies before a living checkpoint. | Acceptance: living later-floor checkpoint, fresh-host resume and complete four-floor retained journey. |
| 9.13 | Legacy per-player import into shared world | Partial | Compatibility import exists, but separate player imports can overwrite shared world/RNG. | Integration/compatibility: establish safe migration/adoption rather than combine incompatible shared-state imports. |
| 9.14 | Normal-profile cohort adoption | Partial | Explicit cohort mode has scoped recovery evidence; default launch profiles retain legacy behavior. | Integration/acceptance: broader qualification and the separately retained adoption approval. |
| 9.15 | Host loss and publication/write failure | Partial | Scoped failure, interruption, fallback and restart cases exist. | Acceptance: supported filesystem/platform, reload and process-termination combinations. |
| 9.16 | Multiplayer scale and soaks | Unverified | One/few-participant workloads and bounded performance recording exist. | Acceptance: supported participant/platform matrix and long-running campaign recovery against defined budgets. |
| 9.17 | Join, leave and reconnect ownership | Partial | Session identities, reconnect, disconnect and scoped cohort rejoin paths exist. | Integration/parity: complete native campaign participant lifecycle and retained ownership changes. |
| 9.18 | Shared item/object/service contention | Partial | Central authority serializes commands and supports scoped multi-client interactions. | Acceptance/parity: simultaneous pickup, one-use objects, trades/services and rejected-command atomicity. |
| 9.19 | Cooperative combat and friendly-mode recovery | Partial | Multi-player actor ordering, target admission and scoped authored combat exist. | Native parity/acceptance: PvP/friendly rules and full combat/effect state across joins and restart. |
| 9.20 | Multiple-owner summon retention | Unverified | Owner-bound summon state and secondary-caster behavior exist. | Acceptance/parity: simultaneous owners, replacement, credit, host restart and ownership transitions; see 3.18. |
| 9.21 | Campaign/quest mutation retention | Partial | Current cohort captures implemented world/quest/object state and scoped native floor owners. | Integration/acceptance: all newly added dungeon, quest, boss, vendor and environmental state. |
| 9.22 | Ruleset replacement and save migration | Partial | Ruleset-bound checkpoints, candidate preparation and rollback mechanisms exist. | Compatibility/acceptance: supported upgrade/reload matrix and campaign-wide retained state after replacement/refusal. |
| 9.23 | Graceful host shutdown and checkpoint drain | Partial | Scoped exported Create/Resume and Performance lanes have natural-exit/drain evidence. | Acceptance: complete campaign/multiplayer shutdown and supported failure/platform combinations. |

Basis: [cohort store](../server/src/Devilution.Server.Host/Stores/AuthoritativeCohortStore.cs),
[session persistence](../server/src/Devilution.Server.Host/Stores/AuthoritativeSessionPersistence.cs),
[command boundary](../server/src/Devilution.Server.Host/Commands/AuthoritativeCommandServer.cs),
[cohort codec](../server/src/Devilution.Server.Diablo/Stores/DiabloCohortCodec.cs),
[recovery tests](../server/tests/Devilution.Server.Tests/CompleteCohortRecoveryTests.cs),
[failure tests](../server/tests/Devilution.Server.Tests/CompleteCohortFailureTests.cs) and
[accepted verification](test-history.md).

## 10. Client playability and flow

The client remains a prototype. The reviewed session-menu batch has headless
create/return/rejoin and fresh-host-resume evidence; several class-control
previews remain WIP. Functional controls and headless acceptance do not establish
native visual usability or a complete campaign/multiplayer release flow.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 10.1 | Launch and connection | Partial | Local launchers, handshake/identity checks and reviewed headless menu create/rejoin/fresh-host resume pass. | Integration/acceptance: reliable packaged create/play/return/resume across supported platforms. |
| 10.2 | Session/character setup | Partial | Local native profiles offer Rogue only; optional endpoint-scoped hints preserve generic/remote choices and bypass class selection for resume. Empty cleared hints retain defaults. | Integration/acceptance: maintain supported class/mode/resource choices as profile admission expands and qualify rendered setup. |
| 10.3 | Return to menu and rejoin | Partial | Reviewed lifecycle/menu flow passes pending/bookmark refusal, joined cleanup, model replacement, private credentials, cancellation and scene exit through headless controls. | Acceptance: rendered usability and broader campaign/multiplayer return/rejoin. |
| 10.4 | Ordinary world movement | Partial | World clicks submit movement and server positions drive presentation. | Acceptance: supported routes, obstacles, interruptions and ordinary play across classes/profiles. |
| 10.5 | Automatic approach and route cancellation | Partial | Snapshot-derived stairs/interaction travel, cancellation and endpoints exist. | Integration/acceptance: campaign-wide travel and approach/target-change edge cases. |
| 10.6 | Combat/object/item target selection | Partial | Helpers select visible actors, objects, portals and items with authoritative identities. | Acceptance: all supported target families, overlap, invalidation and ordinary mouse/keyboard use. |
| 10.7 | Click/held attack controls | Partial | Ordinary intents and committed held entity input exist with receipt/readiness pacing. | Acceptance/usability: sustained class/weapon combat and retarget/release behavior across real encounters. |
| 10.8 | Spell-source and class-ability controls | Partial | Learned/item sources, ability selection and targeted intents exist; several previews are WIP. | Integration/acceptance: consistent controls for all supported classes and native generated progression. |
| 10.9 | Belt, healing and resource controls | Partial | Quick-use and resource feedback consume authoritative state/events. | Acceptance: complete belt/resource management during sustained native class play. |
| 10.10 | Inventory/equipment/cursor/stash UI | Partial | Functional item selection, transfers, equipment and stash controls exist. | Integration/acceptance: complete ordinary drag/swap/capacity workflows and clear native-equivalent feedback. |
| 10.11 | Town service controls | Partial | Purchase/sale/repair/recharge/identify/healing/stash paths have scoped controls and journeys. | Integration/acceptance: all native stock/service lifecycles and ordinary full-playthrough use. |
| 10.12 | Dialogue and quest journal flow | Partial | Dialogue/quest state and some towner interactions are presented. | Integration/acceptance: all generated quest conversations, journal updates, rewards and completion feedback. |
| 10.13 | Portals, stairs and depth transitions | Partial | Actual portal use and selected Cathedral transitions have live-control evidence. | Integration/acceptance: every supported dungeon/set-level transition and return path. |
| 10.14 | Player-facing save and resume | Partial | Reviewed menu flow preserves confirmed credentials and bookmarks through cancellation/rejoin and fresh-host resume at the exact next command. | Integration/acceptance: consistent save/exit/resume flow through complete campaigns and supported profiles. |
| 10.15 | Disconnect and error recovery UX | Partial | Host-loss/input blocking/reconnect have scoped evidence; reviewed menu flow covers canceled handshakes and cleanup/bookmark refusal. | Acceptance: clear recovery for broader outages, corrupt/mismatched inputs, cancellation and retry choices. |
| 10.16 | Multiplayer creation and lifecycle UX | Partial | Local two-window launch and selected play/recovery journeys exist. | Integration/acceptance: complete creation/join/leave/reconnect/restore flow during general multiplayer play. |
| 10.17 | HUD, status and combat feedback | Partial | Life/mana, equipment/items, statuses and feedback exist; live Clear events follows the current model across repeated menu replacement. | Integration/acceptance: complete information hierarchy, readable action/error states and native gameplay usability. |
| 10.18 | Damage/healing/rejection audio hooks | Partial | Optional pack-bound feedback cues can play. | Integration/acceptance: production assets, event coverage, audibility and settings behavior. |
| 10.19 | Full music, environment, combat and speech coverage | Partial | Audio pack/feedback infrastructure exists; the complete native sound set is not supplied/qualified. | Implementation/acceptance: supported music, ambient, actor/spell/object sounds and dialogue/quest speech. |
| 10.20 | Mouse/keyboard/controller input coverage | Partial | Prototype mouse/keyboard controls exist. | Implementation/acceptance assessment: complete supported controller navigation, rebinding, focus and input-mode behavior. |
| 10.21 | Accessibility | Partial | High-contrast controls and functional text/UI foundations exist. | Acceptance/implementation: readable scaling, focus/navigation, contrast and supported accessibility behavior throughout play. |
| 10.22 | Localization | Partial | Pack-bound localized presentation strings and fallback support exist. | Implementation/acceptance: complete text/quest/service coverage, layout and supported language behavior. |
| 10.23 | Resolution, scaling and window behavior | Partial | Godot layouts, camera/viewport and isolated visible checks exist. | Acceptance/parity: supported resolutions, aspect ratios, DPI/fullscreen and input alignment. |
| 10.24 | Player settings and preferences | Unverified | Launcher and presentation-pack configuration exist. | Assessment/implementation: establish native-equivalent player-facing audio/input/display preferences and persistence. |
| 10.25 | Exit/cancellation and scene cleanup | Partial | Reviewed joined cleanup passes scene exit during handshake and active return, preserving disposal outcomes and preventing post-exit publication. | Acceptance: broader campaign/multiplayer/application failure paths and rendered flow. |

Basis: [client scope](../godot-client/README.md),
[main scene](../godot-client/Devilution.Client/Main.cs),
[HUD](../godot-client/Devilution.Client/HudPresentation.cs),
[setup](../godot-client/Devilution.Client/CharacterSetupPresentation.cs),
[travel](../godot-client/Devilution.Client.Protocol/AuthoritativeRouteTravel.cs),
[feedback](../godot-client/Devilution.Client/PresentationFeedback.cs) and
[build/application guide](ai/godot-build.md). Uncommitted capability is not absent,
but it is not a finished release deliverable.

## 11. Full Godot graphical parity

The checked-in pack contains SVG stand-ins and fallbacks. Semantic player
bindings and a visual frame clock are rendering foundations, not native scene
coverage. Native asset conversion elsewhere in the repository does not by
itself supply a Godot original-art import pipeline.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 11.1 | Client presentation-pack validation | Complete | Supported manifests validate texture/audio/localization entries, atlases and semantic selectors. | None for the current manifest contract; production coverage and additional selection dimensions remain below. |
| 11.2 | Godot import of optional original assets | Not started | Native bundled-asset tooling exists, but the Godot prototype does not import original MPQ art. | Implementation: client-only discovery/conversion/binding and clear absent/corrupt-asset handling. |
| 11.3 | Complete MPQ-free presentation pack | Partial | Pack schema, asset resolution, SVG sample assets and fallbacks exist. | Implementation/acceptance: complete supported world/actor/effect/UI/audio coverage with the required native parity. |
| 11.4 | Town scene rendering | Partial | Generic layout/geometry and projected NPCs/objects can be drawn. | Implementation/parity: native Town tile/art coverage, placement, layering and animation. |
| 11.5 | Cathedral scene rendering | Partial | Fine geometry, schematic objects and limited visible scene checks exist. | Implementation/parity: complete native tiles, walls, theme variants and actor/environment presentation. |
| 11.6 | Catacombs scene rendering | Partial | Generic geometry/presentation foundations exist. | Implementation/parity: native Catacombs tile sets, walls, objects, palettes and matched scenes. |
| 11.7 | Caves scene rendering | Partial | Generic geometry/presentation foundations exist. | Implementation/parity: native Caves terrain, transitions, occlusion, lighting and actor placement. |
| 11.8 | Hell scene rendering | Partial | Generic geometry/presentation foundations exist. | Implementation/parity: native Hell levels, special/final rooms, animated pieces and matched scenes. |
| 11.9 | Hive scene rendering | Partial | Generic renderer and Hellfire content identities exist. | Implementation/parity: complete native Hive art, geometry presentation and environmental effects. |
| 11.10 | Crypt scene rendering | Partial | Generic renderer and Crypt/fragment metadata exist. | Implementation/parity: complete native Crypt, Cornerstone/Uber Room and mutation presentation. |
| 11.11 | Quest/set-level scene rendering | Partial | Supplied geometry and projected objects can use the renderer. | Implementation/parity: every supported quest/set map, native scene variant and irreversible visual change. |
| 11.12 | Native tile pieces, walls and transparency masks | Partial | Projected geometry/layers are drawn schematically. | Implementation/parity: native piece placement, wall/front masks, transitions and visual collision boundaries. |
| 11.13 | Player class/facing sprite selection | Partial | Semantic rules select by class, action, combat mode and facing when bindings exist. | Implementation/parity: complete supported class/direction/state sprite assets and binding coverage. |
| 11.14 | Equipment-dependent player appearance | Not started | Equipment state exists, but current semantic selectors do not select armor/weapon/shield appearance variants. | Implementation/parity: equipment-derived native sprite-set selection and correct class/action/facing combinations. |
| 11.15 | Player action animation | Partial | Visual frame timing and action indicators exist. | Implementation/parity: native walk/stand/attack/cast/hit/block/death frames and transition synchronization. |
| 11.16 | Monster family art and animation | Partial | Actor state/styles and visibility feed generic drawing. | Implementation/parity: all native families/uniques, directions, actions, special states and animations. |
| 11.17 | Towners and NPC art/animation | Partial | Actor identity and dialogue state can be projected. | Implementation/parity: native NPC assets, idle/talk states, placement and quest-specific changes. |
| 11.18 | Object art, variants and mutation frames | Partial | Generic objects and scoped door/trap drawing exist. | Implementation/parity: all native object families, opened/broken/disarmed states and animated effects. |
| 11.19 | Ground items and Gold piles | Partial | Item/Gold identity and generic asset bindings are projected. | Implementation/parity: native item-specific sprites, denominations, highlights/labels and pickup presentation. |
| 11.20 | Missile and spell-effect art | Partial | Projectile identities/styles and several effect families have projected rendering. | Implementation/parity: complete native missiles/controllers, impacts, areas and spell-specific animation. |
| 11.21 | Corpses and death presentation | Partial | Corpse snapshots can be drawn and retained in the world. | Implementation/parity: native corpse variants, death frames, placement and visibility transitions. |
| 11.22 | Animation cadence and effect synchronization | Partial | A presentation-only clock advances frames independently of server decisions. | Native parity/acceptance: align visible frames/effects with authoritative phase and native reference playback. |
| 11.23 | Depth sorting and occlusion | Unverified | Rendering has an explicit draw order for projected geometry/entities. | Native parity/acceptance: matched actor/wall/object overlaps, foreground hiding and transparency transitions. |
| 11.24 | Palettes, transparency and color cycling | Partial | Texture/palette helpers and basic Godot presentation exist. | Implementation/parity: native palette transforms, masks, blending and scene-specific cycling. |
| 11.25 | Static and dynamic light shading | Partial | Native light metadata and projected world foundations exist. | Implementation/parity: native falloff, animated/object/missile lights and correct tile/sprite shading. |
| 11.26 | Vision, exploration and fog appearance | Partial | Visibility state and exploration foundations affect projected content. | Native parity/acceptance: native visible/explored boundaries, darkness and scene/transition behavior. |
| 11.27 | Visible automap | Partial | Backend automap semantics and exploration metadata exist. | Implementation/parity: native map drawing, markers, discovery/update behavior and user controls. |
| 11.28 | Resource HUD and belt presentation | Partial | Functional life/mana, status and belt controls exist. | Implementation/parity: native globes, icons, layout, feedback and state indicators. |
| 11.29 | Character, inventory and spellbook panels | Partial | Functional stat, item/equipment and spell controls exist. | Implementation/parity: native panel layouts, backgrounds, grids, icons, selection and details. |
| 11.30 | Vendor, dialogue, stash and quest panels | Partial | Functional service/dialogue/quest controls exist. | Implementation/parity: native screens, typography, state-dependent choices and interaction feedback. |
| 11.31 | Setup, menu, death and victory screens | Partial | Setup and menu/recovery foundations exist; some menu work is WIP. | Implementation/parity: all supported native screen states, backgrounds and transitions, including endings. |
| 11.32 | Fonts and iconography | Partial | Godot text/control assets provide usable labels and buttons. | Implementation/parity: complete native font metrics, glyphs, spell/item/status icons and supported language layout. |
| 11.33 | Cursor, targeting, highlights and labels | Partial | Functional cursor targeting and selection exist. | Implementation/parity: native cursor modes, held-item art, outlines, loot labels and hover/selection behavior. |
| 11.34 | Loading, fades and scene transitions | Partial | Level/portal/restart changes update projected state. | Implementation/parity: native loading/fade/return/death/victory visual sequences. |
| 11.35 | Viewport, aspect and pixel scaling | Partial | Godot viewport/camera/layout support exists. | Native parity/acceptance: matched reference camera, viewport and scale, plus supported resolution behavior. |
| 11.36 | Matched native/Godot frame and animation evidence | Unverified | Some visible milestones and asset-binding checks exist. | Acceptance: complete reference scene/action/UI matrix with paired frames and animation sequences. |
| 11.37 | Asset failure and fallback behavior | Partial | Manifest validation, missing-binding fallback and content-pack selection exist. | Acceptance/integration: full production-pack and optional-original-art failure/repair paths. |

Basis: [manifest and selectors](../godot-client/Devilution.Client/PresentationAssets/PresentationAssetManifest.cs),
[semantic player selection](../godot-client/Devilution.Client/PresentationAssets/SemanticPlayerPresentation.cs),
[asset resolver](../godot-client/Devilution.Client/PresentationAssets/PresentationAssetResolver.cs),
[frame clock](../godot-client/Devilution.Client/PresentationAssets/PresentationFrameClock.cs),
[world renderer](../godot-client/Devilution.Client/WorldPresentation.cs),
[asset palette](../godot-client/Devilution.Client/AssetPalette.cs),
[default pack](../godot-client/Devilution.Client/assets/presentation-pack.json) and
[native bundled-asset tooling](../tools/update_bundled_assets.sh).

### Godot graphical parity acceptance

Full graphical parity is a completion requirement separate from functional
controls or readable replacement art. Compare matched native/Godot scenes using
the same assets, authoritative state, camera, viewport and scaling settings.
Cover Town, every supported base/Hellfire dungeon family and set level; all
supported class/equipment/action appearances; actors, NPCs, objects, items,
missiles, effects, corpses, complete UI and screen transitions.

Use images for static scene/frame comparisons and frame sequences for animation.
Check frame selection/cadence, tile/sprite placement, layering/occlusion,
palettes/transparency/color cycling, light/vision, automap, UI layout/fonts/icons,
and cursor/selection behavior. Record differences against the relevant 11.x
subitem. Headless protocol success, SVG placeholders and asset-binding tests do
not close these acceptance entries. Audio coverage is tracked in 10.18–10.19.

Asset loading/conversion remains client-only. Preserve a complete usable MPQ-free
presentation route and optional client MPQ support; the server never requires
MPQs. No GUI check is requested by this inventory task.

## 12. Release readiness and legacy retirement

These are existing M0/M4/M5 completion requirements, made explicit here so they
are not left as an unnumbered list outside the inventory. They do not add a
second production game or unrelated infrastructure project to the migration.

| ID | Feature | Status | Existing scope | Remaining completion work |
| --- | --- | --- | --- | --- |
| 12.1 | MPQ-free authoritative server boundary | Complete | Server/game-module state is driven by external validated content and does not require MPQs. | None for this ownership boundary; retain it as world/asset coverage expands. |
| 12.2 | Engine/data/module/host separation | Partial | Domain, Data, Engine, Diablo and Host boundaries and a non-Diablo test-adapter foundation exist. | Integration/acceptance: preserve boundaries and qualify final supported adapter/package behavior. |
| 12.3 | Complete MPQ-free client distribution | Partial | Presentation-pack and client packaging foundations exist. | Implementation/acceptance: complete playable asset/UI/audio content without a native installation; see area 11. |
| 12.4 | Reproducible server/content/client packages | Partial | Archive/staging, exported-client and adapter packaging tools exist. | Integration/acceptance: reproducible final builds and complete compatible artifacts for declared platforms. |
| 12.5 | Clean install, upgrade, resume and rollback | Unverified | Scoped isolated launch/export/recovery work provides prerequisites. | Acceptance: packaged creation/play, upgrade/rollback and save continuity without a source checkout. |
| 12.6 | Supported platform/participant matrix | Unverified | Scoped local Windows and one/few-client evidence exists. | Acceptance: define and qualify the declared release platform/configuration/participant matrix. |
| 12.7 | Gameplay performance budgets | Unverified | Bounded standalone/exported measurements and targeted host optimizations exist. | Acceptance: actual campaign tick/snapshot/frame/memory costs against defined budgets. |
| 12.8 | Campaign, multiplayer and recovery soaks | Unverified | Fault and short workload cases exist. | Acceptance: long-running supported play, reconnect/restart/reload and interruption without lost committed state or unbounded queues. |
| 12.9 | Protocol/content/save compatibility closure | Partial | Versioned contracts, validators, codecs and substantial compatibility tests exist. | Acceptance/integration: final supported legacy/new matrix and explicit migration decisions for remaining conflicts. |
| 12.10 | Lua-free authoritative execution | Complete | C# authoritative gameplay does not use embedded Lua. | None for authoritative execution; physical source/build removal remains separate. |
| 12.11 | Production C++/SDL retirement | Partial | Godot/C# routes replace selected runtime surfaces; native compatibility/reference execution remains. | Integration: complete replacement capabilities and remove production package/build fallback dependencies. |
| 12.12 | Lua capability replacement and physical removal | Partial | Capability inventory and Lua-free authority exist; compatibility/script/runtime surfaces remain. | Implementation/compatibility: replace required scripting/diagnostic/presentation capabilities and remove active Lua bindings/scripts/build dependencies. |
| 12.13 | Final release and operation closure | Partial | Build/architecture/operation guides and scoped acceptance tools exist. | Acceptance: close all supported completion-contract gaps, final operation flow and compatibility checks. |

Basis: [completion contract and M4/M5](completion-plan.md),
[architecture](csharp-authoritative-server-migration.md),
[Lua capability inventory](lua-capability-inventory.md),
[entity-pool migration strategy](ENTITY_POOL_MODERNIZATION.md) and
[verification history](test-history.md). No production legacy runtime is retired
merely because one replacement scenario passes.

## Delivery planning

The [near-term work plan](work-plan.md) owns the current proposed/active batch
order and exit criteria. Use these stable subitem IDs to define its scope, and
keep the plan, [working status](migration-status.md) and affected inventory entries
consistent as priorities, behavior, evidence or blockers change.

This inventory defines capability scope and completion; it is not an instruction
to turn each subitem into an isolated task or replay every historical suite.

# Native Compatibility Client

The inherited C++/SDL client remains available for legacy play and for parity
testing while Godevilution moves authority to the C# server.

The default CMake configuration builds the unchanged legacy-authority path. To
include the server-backed adapter, configure with:

```powershell
cmake -S . -B build-authoritative -DDEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON -DBUILD_TESTING=ON
cmake --build build-authoritative
```

Connected Town Portal entry is server-authoritative: the client matches the
collision cell against the latest projected portal route and submits
`UsePortal`, including when the server-created portal has no local legacy
missile. The legacy `CMD_WARP` command remains available only outside a
connected server-backed session.

The Godot protocol model treats reconnect as an explicit server-epoch boundary.
`ResetTransientPresentation()` clears queued presentation events and movement
prediction, then admits the next authoritative snapshot even when its tick is
lower than the retained snapshot (for example after a host process restart).
The model keeps the last server command result until a newer acknowledgement
arrives so reconnect feedback is not lost.

The native authoritative world projection retains the sorted, validated server
projectile snapshots alongside monsters, items, and objects. The presentation
bridge now materializes the first supported families (Charged Bolt, Firebolt,
Lightning, Lightning segments, Flash layers, Resurrection beam, Phasing,
Fireball, Holy Bolt, Blood Star, Bone Spirit, Fire Wall, Lightning Wall, Flame Wave, Inferno,
Apocalypse impact, Chain Lightning, Elemental, Guardian, Etherealize, Acid,
Acid Splat, Acid Puddle, player arrows, monster Charged Bolt, Hork Spawn, and
monster Arrow) into legacy missile sprites when
the active native content pack defines the corresponding missile data. These
objects are marked presentation-only: authoritative positions and lifetimes
are applied directly, while local process, collision, damage, and lifetime
logic is skipped. Their sprite animation advances locally as presentation
state only. Unsupported families or content-missing sprites remain
projection-only until their native presentation mapping is available.

On Windows, initialize the matching Visual Studio developer environment first.
See [ai/msvc-build.md](ai/msvc-build.md) for the verified workspace commands.

After building the server and client, launch a local authoritative session with:

```powershell
dotnet build server/src/Devilution.Server/Devilution.Server.csproj
powershell -ExecutionPolicy Bypass -File tools/run-authoritative-client.ps1 -ClientExecutable <path-to-devilutionx.exe>
```

Pass `-Legacy` to the launcher for the local legacy-authority path. In
authoritative mode the server owns gameplay persistence, the native binary save
is not loaded or written, and a lost server connection ends the session rather
than enabling local simulation.

When a native hero is available at connection time, the adapter sends its
stable class enum value as the new-session class selection. The server validates
that ID against active content and creates the authoritative player; reconnects
retain the in-memory or persisted server class instead of reapplying the local
selection.

The handshake also declares the server-owned game mode. The adapter synchronizes
the legacy `gbIsMultiplayer` presentation flag to that value and rejects an
unspecified mode, so native UI behavior cannot disagree with mode-sensitive
authoritative rules. The standalone server defaults to multiplayer; a local
single-player launch passes `--game-mode single-player` while using the same
protocol and server implementation.

The accepted handshake identity is retained and refreshed from each complete
authoritative snapshot, and the refreshed values are used on reconnect. This
lets a connected native client observe a successful live ruleset replacement
without changing gameplay state or falling back to local simulation. A snapshot
that supplies only one of the two identity fields is rejected before
presentation.

Authoritative player snapshots also project the stable content class ID into
the native hero-class enum before applying class-dependent attributes. The
compatibility client rejects an unsupported class ID instead of silently
substituting a local class.

The first player in a network snapshot is the receiving native session's local
entity. The adapter selects subsequent updates by that stable ID and validates
that every projected player ID is nonzero and unique, so adding remote players
cannot silently redirect local state.

The adapter also retains the sorted remote-player projections. For
Resurrection, a legacy player selected under the cursor is resolved by its
authoritative same-level coordinates and sent as a stable server entity ID;
the client never decides whether revival is valid or where the player appears.
The compatibility layer now retains presentation-only remote-player objects
from participant snapshots and draws current-level sprites directly from the
authoritative positions. These objects never enter the native `Players`
simulation pool, so they cannot run local AI, movement, collision, or damage
logic. Their authoritative walk, attack, ranged, block, hit, spell, and death
modes now select the matching native presentation family, authoritative spell
actions select Fire, Lightning, or Magic families, and authoritative
weapon/shield/armor slots select native sprite variants, and action animations
are positioned from authoritative snapshot and action ticks. Same-level friendly
remote projections also draw on the native automap and party panel. Authoritative
one-tile walk destinations now populate each remote presentation player's native
future position, allowing the existing renderer to interpolate the walk without
giving the compatibility client movement authority. Party-panel hover and
right-click inspection now resolve through authoritative entity IDs, and the
inspected presentation player receives authoritative stats, spells, and item
layout. Delayed remote snapshots older than the last applied server tick are
discarded before they can regress presentation position, action, or level state.
The same tick boundary now protects the local player projection and native
feedback from delayed stale snapshots and event batches. Correction buffering
and remaining legacy interaction parity remain separate migrations.

The lower-level authoritative player-state owner enforces the same monotonic
server-tick rule, so delayed snapshots cannot regress state even when an adapter
applies a validated projection outside the normal session poll.

The server now projects the selected class's content-defined wallet, starting
equipment, belt, inventory, class skill, learned spells, item native indexes,
and charge counts. The adapter rebuilds native equipment and spell masks from
each snapshot and rejects unsupported spell IDs or invalid item topology. It
does not retain locally generated creation items as gameplay authority.

For the Bard's server-owned Identify class skill, native spell selection opens
only the legacy item cursor. Clicking equipment or inventory submits the exact
item reference with the class-ability service source, waits for the accepted
snapshot, and does not call the local identification rule. Stash page/cell
selection uses the same authoritative reference path; paid vendor
identification retains its original source-zero path.

The Warrior's server-owned Repair skill similarly opens only the legacy repair
cursor. Equipment or inventory selection sends the class-ability source and
waits for the snapshot before playing presentation audio; it does not call the
local random repair rule. Connected stash repair uses the same authoritative
page/cell reference path. Visual-store and classic Smith flows retain source-zero
vendor repair.

The Sorcerer's server-owned Staff Recharge skill opens only the legacy recharge
cursor. Equipment or inventory selection submits the exact item with the
class-ability source and projects the accepted snapshot instead of invoking the
local recharge rule. Connected stash recharge uses the same authoritative
page/cell reference path; paid Witch recharge retains its source-zero vendor path.

Cain's connected Storyteller menu now opens a dedicated authoritative
Storyteller service session before displaying the paid-identification cursor.
The server owns the town-level service identity and identification admission;
the native cursor keeps its source-zero vendor command for protocol
compatibility. Leaving Cain's menu closes that service session. Offline mode
retains the legacy local menu and pricing behavior. The explicit leave option
and Escape path both close the authoritative session.

The Barmaid stash entry now opens the server-owned stash session while
connected. The native client validates and projects the authoritative 100-page
10×10 grid, session state, and stash gold into the legacy renderer; gold
deposit/withdraw commands are server-owned. Normal stash drags, shift-click
automatic withdrawals, and Ctrl-click inventory-to-stash placement submit stable
page/cell move commands and apply the server snapshot. Disconnected legacy
sessions retain the existing stash behavior.

Native service, store, and charged/consumable spell-source requests now resolve
their item references from the latest authoritative projection, carrying the
stable content item ID when present. Older servers and snapshots retain the
zero-valued positional compatibility path.

Connected native spell casts now consume the server-projected targeting role
for the selected ability, memorized spell, or item source. Tile-targeted and
world-targeted casts keep their entity ID empty even when the cursor is over a
creature, while player/combat-entity casts retain stable projected entity IDs;
missing metadata preserves the legacy selection behavior for older snapshots.
When a connected cursor visibly targets a player or combat entity but that
entity is absent from the authoritative projection, submission now fails closed
instead of silently converting the action into a tile cast. The native
player-first precedence remains unchanged when both projected target kinds are
available, including scroll and rune consumable casts.

The indexed `SellItem`, `RepairItem`, `RechargeItem`, and `IdentifyItem`
convenience entry points use that same projection lookup before submitting a
command. An out-of-range or removed inventory slot is rejected locally instead
of being sent as a positional reference that could target a different item.

Inventory relocation now follows the same boundary: the native indexed move
entry point resolves the latest projected item and sends its stable identity,
while the legacy positional protocol field remains available for older peers.
When the native inventory cursor completes a normal drag into another inventory
cell or belt slot, the final placement uses the authoritative
`MoveItemRequested` transfer. Equipment placement uses `AutoEquipItemRequested`
with the selected equipment slot, so hand selection and two-handed displacement
are resolved atomically by the server. The cursor selection is presentation-only
while the server snapshot remains the source of truth; rejected transfers
reconcile the native projection instead of applying a local swap. Automatic
moves from equipment or belt to inventory, from inventory to an eligible belt
slot, and from inventory to equipment omit the optional target and retain the
server-owned native first-fit policy.

Spell-book use follows the same boundary: a connected authoritative client sends
`ReadBookRequested` with the projected stable item reference and applies the
returned snapshot. The local book-consumption path remains available only for
the explicit legacy runtime.

Potion, rejuvenation, elixir, and supported oil use now follows the same
boundary. A connected client sends `UseConsumableRequested` for the projected
inventory, belt, or open-stash source reference; oil application adds the exact
selected inventory, equipment, or open-stash target. The server applies native
Q6 resource restoration, class multipliers, elixir stat/resource transitions, Hellfire
full-resource effects, oil target restrictions and mutations, and `NoMana`
suppression before consuming the source item. Open-stash oil targets use the
same stable-reference command path; closed-stash targets are rejected and the
cursor remains armed. The Cathedral Map now uses the same command boundary:
the server validates the owned item and emits a transient `MapOpenedEvent`,
while the item remains in inventory and each client opens its native map view.
The Hellfire Na-Krul notes now use the same boundary as `ReadQuestNote`:
the server emits the authored `TEXT_BOOK9` dialogue and leaves the note owned.

Connected stash books use `ReadBookRequested` with the same open-session stable
reference, and stash consumables are consumed by the server rather than a local
stash mutation. Closed-stash sources fail closed without changing player state.

The Hellfire Cathedral Map's adjacent-grave quest remains a separate
`OperateObjectRequested` interaction. The server owns that quest-item
completion; opening the already-owned map is the transient item event above.

Hellfire Rune Bomb use follows the same boundary. When the native Hive-opening
condition is met, the connected client submits the nearest projected Hive
entrance object through `OperateObjectRequested`. The server owns bomb
consumption, Farmer's Orchard completion, and the resulting gated Nest warp;
legacy mode retains the native `OpenHive()` path.

World-item pickup is also server-owned in connected mode. The Hellfire Magic
Rock now activates its content-defined quest transition as part of the accepted
`PickupWorldItemRequested` command; the native client presents the returned
quest and inventory snapshot.
The Anvil of Fury follows the same server-owned pickup boundary.
Optic Amulet and Arkaine's Valor pickups likewise return server-owned quest
completion state only when their native quest is active.
Black Mushroom pickup uses the same boundary and preserves the native spawned
mushroom state check.

The native authoritative adapter now submits `TurnInQuestRequested` when an
authored quest interaction is presented. The server owns adjacency, native
conversation variables, required-item consumption, quest completion, activation,
normal or unique reward spawning, and the dialogue event; the adapter projects
that dialogue into the native quest-text UI. The Hellfire Farmer activation and
post-Hive reward, base Cain's Staff of Lazarus activation and Lazarus
Storyteller completion stage, Hellfire Grave Matters/Barmaid dialogue, base Poisoned
Water/Pepin completion dialogue, Black Mushroom/Pepin staged Brain hand-in, and
Black Mushroom/Adria's repeatable Fungal Tome, mushroom, brain-dialogue, and
Spectral Elixir stages,
base and Hellfire
Magic Rock, Ogden's Sign, Anvil of Fury/Blacksmith, Skeleton King, Butcher/Dead Guy, Little Girl, and Jersey's
Jersey/Complete Nut, and Ogden's itemless post-completion King Leoric dialogue
(including its Grey Suit tease) are shipped
definitions; additional legacy towner paths remain local until authored.
The Little Girl stages preserve native ordering: the server accepts Theodore
even while the quest is still initial, and the opening/active dialogue stages
are offered only when Theodore is absent.
The adapter also consumes server-owned `visited_level_ids` and only presents
the authored King activation after the Catacombs or Caves visitation gate is
satisfied, preserving native `_pLvlVisited` behavior without local history.
The authored Magic Rock and Anvil activation stages use the same server-owned
visited-level boundary for the native Blacksmith gates.
Hellfire Grave Matters/Barmaid uses the same boundary for its native
`_pLvlVisited[21]` dialogue suppression; the adapter never infers that history
locally.
Base Poisoned Water/Pepin now also keeps the native initial visited-level gate
and active-but-unlogged fallback in the authored interaction projection.
Legacy local towner behavior stays available outside the authoritative runtime.

Content-declared unique-monster conversations use the same boundary. When the
projected live unique identity matches an active quest, the native client sends
`TalkToQuestRequested`; the server validates the quest, target identity, level,
and adjacency before returning the dialogue event. Procedural unique targets
receive their authored identity when the spawn planner materializes the
matching encounter. Dialogue definitions may
also declare an inventory hand-in and relative unique or normal-item reward;
the server consumes the item, completes the quest, and emits the authored
completion dialogue. Definitions may instead generate a one-time reward during
the first conversation while leaving the quest active; base Zhar is the first
activation-reward definition and uses a generated level-14 spell book. Hellfire
Defiler is the first conversation-only definition, Lachdanan is the first
hand-in definition, and base Warlord declares its subsequent `_qvar1` transition
into the native attacking state. Base Gharbad, Zhar, Warlord, and Lazarus
identities are now declared independently of static placement and are applied
when their validated procedural unique encounters materialize. Gharbad's four
user-visible dialogue stages now use the server-owned
`quest_dialogue_stages.tsv` contract for authored messages and monotonic
quest-variable transitions. Its random item reward is server-owned; the current
authored dialogue identity is now retained in the authoritative monster
snapshot, while encounter-state transitions remain local fallbacks.
When the active pack includes `dialogue_messages.tsv`, the server also sends
the authored text on `DialogueEvent.text`; older packs continue to use the
message identity fallback.
The shared Ogden's Sign/Snotspill quest now uses the same contract for
Snotspill's banner dialogue, server-owned sign-chest Banner reward, and Tavern
Sign hand-in, including item consumption and quest completion.
Warlord's two authored dialogue transitions now use the same staged contract;
the next authored message identity survives in authoritative snapshots, while
native AI-driven speech completion remains a compatibility fallback.
Unauthored legacy monster conversations retain their local fallback until
their content actions are externalized.

Content-declared quest objects now use the same `OperateObjectRequested`
boundary. `QuestSequence`, quest-transition, and `QuestItemCompletion` effects
keep ordering,
quest-state changes, item consumption, activation, and linked authored quest
dialogue events in the server snapshot; the Hellfire Na-Krul tome sequence is
the first shipped definition. Objects without an authored quest effect retain
the legacy local behavior only in offline sessions; connected sessions fail
closed unless the object has an authoritative projection. The same boundary
can now apply a validated
`WorldMutation` effect: the server persists the active cell overlay and projects
the resulting movement/missile geometry, while the client only presents the
updated level snapshot. Authored object rows can also apply validated quest
activation/completion and native `_qvar1`/`_qvar2` transitions with required
variable gates; the client remains responsible only for selecting and
presenting the projected object. An authored object may also generate one
validated normal or unique item at a relative offset; the server owns catalog
validation, deterministic generation, placement, and persistence, while the
client presents the resulting world item. The generic `GrantSpellLevel` effect
also lets an authored book/lever grant one validated spell level; the client
only presents the updated player snapshot and does not mutate spell state. The
`SpawnSpellProjectile` effect extends that boundary for authored Phasing and
Guardian controller rows: the server validates the spell behavior, creates
the resumable projectile, and the client only presents its projected state.
A direct `WorldMutation` object may now apply its validated cell overlay and
follow it with one authored Phasing or Guardian projectile, matching the native
multi-effect set-piece shape without client-side gameplay logic.
Optional target offsets are also server-owned metadata, so native book-relative
coordinates can be authored without client-side gameplay. Authored Phasing rows
may additionally select the activating player's tile as their projectile origin;
the compatibility default retains the prior target-origin behavior. Set-level placement
and map effects remain content seams still being filled from native data; authored
world mutations can now also assign validated native transparency and native
object-layer rectangles for set-piece effects such as Halls of the Blind.
Object-layer values are retained for the explicit mapping catalog, and active
mutations now reconcile mapped object entities dynamically: unchanged mapped
objects preserve server state, while stale and newly mapped entities are
removed or materialized deterministically. The
Diablo module now parses native `.dun` layers, native little-endian `.til`
mega-tile records, and `.sol` tile-property bytes, then exposes an exact
materializer when those collision inputs are supplied, without guessing from
tile presentation IDs.

Native object activation now resolves any projected object with an authored
server effect or quest/reward link before consulting the legacy object behavior
registry. This keeps future set-piece templates on the authoritative path.
Ordinary doors, chests, and other un-authored objects remain compatible with
the legacy client behavior offline; connected presentation-only objects fail
closed before they can mutate local state. Controller interactions apply the
same rule to highlighted monsters: if the native cursor still shows a monster
after the latest authoritative projection dropped its entity, the client stops
rather than converting the stale highlight into a positional attack.

## Remaining native-only interaction inventory

The following paths were audited against `Source/game` on 2026-08-24 and are
still intentionally local when their authored content is absent. They are
explicit migration seams, not server-authority claims:

- `TalktoMonster` still owns Snotspill's Banner opening object/map
  resynchronization, encounter-state, and native speech/audio timing after the
  server-projected dialogue identity and initial room placement. The opening
  transparency rectangle is now
  server/content-owned, alongside the initial Snotspill encounter,
  encounter-relative sign chest and its server-owned Banner reward,
  `banner1.dun` map fragment, and final item-gated mega-tile/transparency
  transition.
  Gharbad's authored dialogue messages, quest-variable transitions, and
  second-stage random unique-monster reward now move through
  `TalkToQuestRequested`; his remaining encounter-state and presentation
  transitions stay in the native compatibility path until they have an
  authoritative encounter-state contract.
- Warlord's staged unique-monster conversation now uses authored messages and
  `_qvar1` transitions through `TalkToQuestRequested`, while its native AI-driven
  speech completion and encounter-state presentation remain local fallbacks.
- Lachdanan's item-gated dialogue hand-in now uses the authored server transition
  for its completion message, Veil of Steel reward, and post-dialogue defeat;
  native presentation still supplies the legacy speech/audio timing fallback.
- `OperateBook` and `OperateBookLever` still own Chamber of Bone's ordinary
  quest-book activation and the Halls of the Blind and Blood-goal book branches.
  Authored object lifecycle metadata now lets the authoritative projection
  retain a completed native object and select its activated animation frame;
  this is presentation-only and does not make activation repeatable. Native
  behavior, placement, and map resynchronization remain compatibility seams
  until their server contracts are externalized.
  The base pack now carries template-only behavior rows for the native Halls of
  the Blind book, Gateway of Blood book, and Warlord steel tome. These rows
  author their native object identities and initial quest-variable transition;
  presentation metadata also keeps each activated book visible and selects its
  native open-book frame;
  native-DUN placement, book-specific rewards, and map resynchronization remain
  deferred until the corresponding generated geometry and licensed assets are
  available.
  The Godot client now consumes that frame metadata and renders an additive
  open-book cue without introducing native object-ID logic into the client.
  It also preserves server-authored missile-only collision cells in the
  authoritative layout and renders them as presentation-only occlusion cues.
  The Godot HUD and world renderer share the same typed status projection for
  server-owned Mana Shield, Reflect charges, and timed player effects.
  Search-driven item highlighting also consumes that projection and ignores
  expired status entries.
  The Godot HUD exposes the projected status durations and permanent-state
  marker as presentation data only.
  The two fixed Chamber of Bone completion books now clone authored templates:
  the server completes the quest, grants Guardian, and owns the exact
  player-origin target offset. The Vile Betrayer's two fixed book
  placements now clone authored templates: server activation owns the native
  Phasing projectile source and target offsets plus the authored replacement
  rectangles when the native map bundle is available. The templates declare the
  native book and center-circle presentation identities, while the native
  bridge now applies supplied authoritative mega-tile, d-piece, and transparency
  layers. Native DUN object-layer materialization and encounter-progress
  presentation remain compatibility seams. The Vile center circle now uses an
  authored occupancy trigger gated on
  both linked books; the server applies its deferred replacement, advances the
  native quest marker, and launches the activating-player Phasing transition.
  Set-level rows can clone validated
  `objects.tsv` behavior templates, and the generic `GrantSpellLevel`,
  `SpawnSpellProjectile`, and `GrantSpellLevelAndSpawnProjectile` effects cover
  reusable spell-level and Phasing/Guardian-controller transitions. The
  existing Na-Krul `QuestSequence` rows now include the anchor-relative final
  lever, whose server-owned completion gate follows the three-tome sequence.
  The three tomes and final lever declare native presentation identities so
  the compatibility client can render and resynchronize them; native room
  reconstruction and collision/tile-definition presentation remain compatibility
  seams.
- Quest-specific set-piece mutation and resynchronization remains native for
  the Banner opening transition and associated unique-monster state, plus the
  remaining book/lever set pieces. Procedural level/object parity must precede
  externalizing those remaining map mutations.
- Towner methods without authored quest stages retain local dialogue/store
  presentation. Store transactions remain server commands where a store
  service is declared, but the surrounding legacy conversation is not yet a
  generic content resource.

New authored rows must be accompanied by native parity tests and a protocol
projection before these fallback branches are removed. The legacy path remains
available outside the authoritative runtime during this staged migration;
connected sessions do not use these local fallbacks.

When the server reports an in-progress player action, the adapter validates its
native timing and action payload before projection. A deferred move reserves the
authoritative destination in the native future-position field; attack and cast
payloads remain server state and are not resolved by the compatibility client.
Already-applied death and level-transition actions project the server-owned
Death or NewLevel mode and timing without replaying damage or portal effects.
Once the Death action has cleared, both the dead-player Escape path and main
panel button send `RestartTownRequested` when connected. After acceptance, the
adapter projects action kind 6 and invokes the legacy town-loading event only
as presentation; level, position, life, mana, inventory, and store state remain
server-owned.

The native client still requires legally obtained Diablo game assets. Upstream
DevilutionX documentation remains the reference for legacy platform packaging
and asset placement.

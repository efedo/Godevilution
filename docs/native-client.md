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

The native authoritative world projection retains the sorted, validated server
projectile snapshots alongside monsters, items, and objects. The presentation
bridge now materializes the first supported families (Charged Bolt, Firebolt,
Lightning, Lightning segments, Flash layers, Resurrection beam, Phasing,
Fireball, Holy Bolt, Blood Star, Bone Spirit, Fire Wall, Flame Wave, Inferno,
Apocalypse impact, Chain Lightning, Elemental, Guardian, Etherealize, Acid,
Acid Splat, Acid Puddle, player arrows, monster Charged Bolt, Hork Spawn, and
monster Arrow) into legacy missile sprites when
the active native content pack defines the corresponding missile data. These
objects are marked presentation-only: authoritative positions and lifetimes
are applied directly, while local process, collision, and damage logic is
skipped. Unsupported families or content-missing sprites remain
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
modes now select the matching native presentation family, and authoritative
weapon/shield/armor slots select the native sprite weapon and armor variants.
Exact spell-specific frames, party-panel details, and automap parity remain
separate presentation migrations.

The server now projects the selected class's content-defined wallet, starting
equipment, belt, inventory, class skill, learned spells, item native indexes,
and charge counts. The adapter rebuilds native equipment and spell masks from
each snapshot and rejects unsupported spell IDs or invalid item topology. It
does not retain locally generated creation items as gameplay authority.

For the Bard's server-owned Identify class skill, native spell selection opens
only the legacy item cursor. Clicking equipment or inventory submits the exact
item reference with the class-ability service source, waits for the accepted
snapshot, and does not call the local identification rule. Stash items remain
unsupported while connected because they are not yet part of authoritative
item state; paid vendor identification retains its original source-zero path.

The Warrior's server-owned Repair skill similarly opens only the legacy repair
cursor. Equipment or inventory selection sends the class-ability source and
waits for the snapshot before playing presentation audio; it does not call the
local random repair rule. Connected stash repair is blocked until stash state is
authoritative. Visual-store and classic Smith flows retain source-zero vendor
repair.

The Sorcerer's server-owned Staff Recharge skill opens only the legacy recharge
cursor. Equipment or inventory selection submits the exact item with the
class-ability source and projects the accepted snapshot instead of invoking the
local recharge rule. Connected stash recharge remains blocked; paid Witch
recharge retains its source-zero vendor path.

Native service, store, and charged/consumable spell-source requests now resolve
their item references from the latest authoritative projection, carrying the
stable content item ID when present. Older servers and snapshots retain the
zero-valued positional compatibility path.

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
inventory or belt reference; oil application adds the exact selected inventory
or equipment target. The server applies native Q6 resource restoration, class
multipliers, elixir stat/resource transitions, Hellfire full-resource effects,
oil target restrictions and mutations, and `NoMana` suppression before
consuming the source item. The oil cursor is presentation-only and remains
armed when the server rejects a target. Other cursor-driven miscellaneous
items remain local follow-up seams until their content actions are externalized.

The Hellfire Cathedral Map is also server-backed: when its native adjacent-grave
condition is met, the connected client submits the projected grave object through
`OperateObjectRequested`. The server owns the quest-item completion and map
consumption; the native client only presents the resulting snapshot.

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
quest-variable transitions. Its random item reward and quest-specific map
mutations remain local fallbacks until those effects are externalized.
When the active pack includes `dialogue_messages.tsv`, the server also sends
the authored text on `DialogueEvent.text`; older packs continue to use the
message identity fallback.
The shared Ogden's Sign/Snotspill quest now uses the same contract for
Snotspill's banner dialogue and Tavern Sign hand-in, including server-owned
item consumption and quest completion.
Warlord's two authored dialogue transitions now use the same staged contract;
its native AI-driven speech completion and encounter-state presentation remain
compatibility fallbacks.
Unauthored legacy monster conversations retain their local fallback until
their content actions are externalized.

Content-declared quest objects now use the same `OperateObjectRequested`
boundary. `QuestSequence` and `QuestItemCompletion` effects keep ordering,
quest-state changes, item consumption, activation, and linked authored quest
dialogue events in the server snapshot; the Hellfire Na-Krul tome sequence is
the first shipped definition. Objects without an authored quest effect retain
the legacy local behavior. The same boundary can now apply a validated
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
Optional target offsets are also server-owned metadata, so native book-relative
coordinates can be authored without client-side gameplay. Set-level placement
and map effects remain content seams still being filled from native data; the
Diablo module now parses native `.dun` layers, native little-endian `.til`
mega-tile records, and `.sol` tile-property bytes, then exposes an exact
materializer when those collision inputs are supplied, without guessing from
tile presentation IDs.

## Remaining native-only interaction inventory

The following paths were audited against `Source/game` on 2026-08-18 and are
still intentionally local when their authored content is absent. They are
explicit migration seams, not server-authority claims:

- `TalktoMonster` still owns Gharbad/Snotspill quest-specific map changes.
  Gharbad's authored dialogue messages, quest-variable transitions, and
  second-stage random unique-monster reward now move through
  `TalkToQuestRequested`; the remaining dynamic effects need their own
  content-backed map-mutation definitions.
- Warlord's staged unique-monster conversation now uses authored messages and
  `_qvar1` transitions through `TalkToQuestRequested`, while its native AI-driven
  speech completion and encounter-state presentation remain local fallbacks.
- `OperateBook` and `OperateBookLever` still own the Chamber of Bone, Vile
  Betrayer, Halls of the Blind, and Blood-goal book branches. These require
  set-level object placement plus authored projectile/map effects; the generic
  `GrantSpellLevel` and `SpawnSpellProjectile` effects now cover reusable
  spell-level and Phasing/Guardian-controller transitions, but exact native
  target offsets, set-level placement, and map effects remain. The existing
  Na-Krul `QuestSequence` row is not a substitute for those remaining
  semantics.
- Quest-specific set-piece mutation and resynchronization remains native,
  including the Banner set-piece transitions and the associated unique-monster
  state. Procedural level/object parity must precede externalizing those map
  mutations.
- Towner methods without authored quest stages retain local dialogue/store
  presentation. Store transactions remain server commands where a store
  service is declared, but the surrounding legacy conversation is not yet a
  generic content resource.

New authored rows must be accompanied by native parity tests and a protocol
projection before these fallback branches are removed. The legacy path remains
available outside the authoritative runtime during this staged migration.

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

# Godot C# Client

This directory contains Godevilution's presentation client. It submits player
intent to the C# server, receives complete snapshots and transient events, and
renders the latest authoritative projection. It does not simulate gameplay or
own persistence.

The client also converts authoritative damage, healing, and experience events
into transient floating feedback positioned from the latest snapshot. This is
the server-backed presentation path; the native Lua floating-number mods remain
available only to the legacy compatibility client.
Feedback anchors are exposed only for entities visible on the active level, so
off-level snapshot entries cannot create floating labels in the current view.

The accepted `ServerHello` is retained as a typed `AuthoritativeServerIdentity`
across reconnects. The diagnostics panel shows the server build plus shortened
content and ruleset hashes, making it clear which validated ruleset the
presentation client is consuming without giving the client gameplay authority.
Live snapshots repeat the content and ruleset identities, allowing that display
to update when the host replaces a ruleset while the connection remains open.

The protocol model retains the latest 256 accepted event entries. A monotonic
sequence window lets feedback advance past entries that roll off, so a
long-running session cannot grow the client event history without bound or
silently duplicate or skip newly appended feedback.

The HUD presents every server-provided memorized spell, charged item,
consumable, and class ability as a selectable spell source. Selection is keyed
by the authoritative source identity, so reconnects and snapshot updates do
not turn presentation labels into gameplay state.

Monster health bars are also projected directly from authoritative
`MonsterSnapshot` vitality, preferring exact Q6 values and retaining a rounded
fallback for older peers; the client never simulates or mutates monster life.
Player life and mana bars use the same snapshot-only rule for local and remote
players, so presentation never predicts resource changes.

The protocol model rejects contradictory player-resource and monster-vitality
values before Godot consumes a snapshot; presentation clamping is defensive
only.

## Projects

- `Devilution.Client.Protocol` contains generated Protobuf types, TCP framing,
  handshake logic, command retry, and the client-side authoritative model.
- `Devilution.Client.Protocol.Tests` verifies the protocol model and starts a
  real local server for live transport tests.
- `Devilution.Client` is the Godot 4.7.1 Mono presentation project.

## Verify Without Godot

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build godot/Devilution.Client/Devilution.Client.sln
```

The live protocol tests build and launch their own server. A Godot installation
is not required for these commands.

## Run the Complete Local Session

Install Godot 4.7.1 Mono, then run:

```powershell
powershell -ExecutionPolicy Bypass -File godot/run-local-client.ps1
```

The harness builds the server if needed, starts it on an available port, reads
the negotiated ruleset identity, configures the client environment, and launches
Godot. The protocol client retains the server-owned game mode from the handshake.
Pass `-GodotExecutable` when discovery cannot find the Mono executable.
For an automated smoke launch, add `-Headless -QuitAfterSeconds 5`.
Pass `-CharacterClassId <0..5>` to request one of the shipped classes when
creating a new session. Omitting it uses the active content default; a resume
token's existing server state takes precedence. The first snapshot contains
that class's content-defined wallet, resources, equipment, belt, inventory,
class skill, and learned spells. Manual launches can set the same optional value
with `DEVILUTION_CHARACTER_CLASS_ID`.
Pass `-CharacterName <name>` to request a native-valid character name for the
same new session. Manual launches use `DEVILUTION_CHARACTER_NAME`. The server
validates, persists, and restores the name; a resume token takes precedence and
cannot rename an existing character.

`AuthoritativeClient.ReconnectAsync()` can reconnect the same client instance without
losing its server-issued resume token or pending command sequence ledger. Pending
commands become eligible for immediate retry after the replacement connection's
authoritative snapshot is received, so the Godot presentation layer does not need to
recreate its protocol model for a transient disconnect.
When no endpoint or resume token is supplied by the launch environment, the
session setup menu accepts the authoritative server host and port and validates
them before the initial connection. The menu can either create a new character
with the selected class/name or resume an existing session by entering its
resume token; resumed server-owned state takes precedence over the new-character
fields. DEVILUTION_SERVER_HOST and DEVILUTION_SERVER_PORT remain available for
scripted launches.

The `Main` presentation node observes transport closure and poll failures, then
retries with bounded exponential backoff while retaining the same authoritative
model and pending-command ledger. Reconnect also clears transient movement
prediction and the prior event history, so dialogue, victory, and floating
feedback from the interrupted stream cannot be presented as part of the new
authoritative stream.

An authoritative snapshot may also contain an in-progress movement, attack,
cast, death, level-transition, or restart-in-town action. The protocol model
validates its timing and payload. For deferred movement, local presentation
uses bounded prediction while the command is unacknowledged, then falls back to
the server-owned `started_at_tick`/`effect_tick` interval so reconciliation does
not snap the player to a tile. Death and portal transitions arrive already
applied with server-owned Death or NewLevel modes; Godot never resolves their
damage or destination effects itself.
The world presentation also advances a bounded fractional presentation tick
between snapshots at the server-declared tick rate through the
engine-independent protocol projection layer, allowing a timed monster walk to
finish smoothly when the authoritative snapshot cadence is coarser than the
render cadence for both monster and remote-player walks. Occupancy, targeting,
and all gameplay decisions remain
discrete snapshot state; the presentation clock is reset when the local
authoritative level changes.
After terminal Death, Escape sends `RestartTownRequested`. The server chooses
the destination and resource reset; Godot accepts action kind 6 only as an
already-applied NewLevel transition and renders the returned town snapshot.
When the server projects the typed Diablo victory event or terminal victory
snapshot state, Godot displays a victory banner using the server-provided kill
level identity; it does not infer or mutate completion locally.
If a Gold pickup exceeds inventory capacity, the snapshot carries the remainder
as `held_item`. Godot displays it and sends `DropHeldItemRequested` for a clicked
tile (or the current tile with `X`); it never chooses the accepted placement or
mutates the item locally.
Dynamic world items such as PvP ears carry authoritative runtime names and a
native cursor identity. Godot validates and renders those fields from the
snapshot; it does not recreate them from the victim or local class data.
World-item snapshots also carry server-owned `GOLD` and `EAR` presentation
families, so the client can distinguish currency and PvP ears without inferring
their identity from native numeric item fields.
Object snapshots likewise carry server-owned shrine, quest, breakable, and
revealed-trap presentation families; older or unknown metadata uses the generic
object visual.
Persistent defeated non-minion corpse tiles are also server-owned snapshot
entities. Godot retains and renders corpses only when their projected level is
visible, including the native level-zero compatibility wildcard; it never
creates or removes corpse state locally.
Portal rendering, clicked-portal routing, and endpoint range checks use the same
exact server-owned source and bidirectional destination level identities. Level
zero remains the real native town level for portal admission; the client never
chooses a transition destination.

Snapshots contain every assigned participant. The server puts this client's
player first and orders the remaining players by entity ID. The client validates
all identities and action state, applies prediction only to that first player,
and renders same-level remote players from authoritative coordinates. An
in-progress one-tile walk is presented from its server-owned
`started_at_tick`/`effect_tick` interval; discrete reserved destinations remain
available for target selection. Remote players remain presentation-only; the
client never advances their gameplay state. Replacing a snapshot removes
positions for entities no longer present, and disconnecting clears all
displayed entity state.
Authoritative level geometry and warp markers are replaced as a complete
projection; a geometry-less legacy snapshot or disconnect clears the prior
server projection before static presentation fallback is used.
Object snapshots are validated against DevilutionX's native `MAXOBJECTS` pool:
no level may project more than 127 objects, preserving the same fail-closed
boundary as the server and native compatibility bridge.
World-item snapshots apply the corresponding native `MAXITEMS` pool bound:
no level may project more than 127 active world items before presentation.
When a player-targeted Resurrection or Heal Other source is selected, clicking a
same-level remote player sends that player's stable entity ID. The server uses
the active external spell behavior to admit the cast, validate life state,
revive or heal the target, and own every resulting snapshot. Godot does not
infer targeting from numeric spell IDs.
Mana Shield is likewise presentation-only in Godot: the authoritative
`mana_shield_active` flag adds a cyan player halo and a HUD state label. The
client neither converts damage to mana nor predicts shield expiry.
Search is a typed snapshot-only status. While behavior `2` is active, Godot
adds a HUD label and outlines authoritative world items; it never computes,
extends, or expires the duration locally.
For a player whose authoritative class ability is Bard Identify spell `5`, the
inventory Identify button remains available without a store and sends the
explicit class-ability service source. Other players use the existing vendor
request while a store is active. Godot never changes item identification or
Gold locally.
For Warrior Repair spell `26`, the Repair button likewise remains available
without a store and sends the class-ability source. Godot does not roll repair
strength, reduce maximum durability, consume Gold, or remove an exhausted item;
it renders only the resulting authoritative snapshot. Other classes use the
paid vendor request while a store is active.
For Sorcerer Staff Recharge spell `27`, the Recharge button remains available
without a store and sends the class-ability source. Godot does not roll recharge
strength or mutate current or maximum charges; it renders the returned snapshot.
Other classes use paid vendor recharge while a store is active.
Stone Curse is also snapshot-only in Godot. Petrified monsters receive a stone
tint and halo, and behavior-`13` duration/shatter controllers receive a distinct
projectile treatment. Godot does not select immunity, advance the timer, or
release movement locally.
Rogue Trap Disarm is likewise server-owned: a Rogue/TrapSense recipient reads
the additive `ObjectSnapshot.trap_armed` field, marks armed objects with a red
overlay, and enables the typed disarm command only for ability spell `28` on
the same level. Non-TrapSense recipients receive the same object without the
armed flag. The server validates range, ability identity, and state change;
Godot never clears the trap locally.
Teleport behavior `9` and Phasing behavior `15` are server-owned movement
controllers. Godot may render their presentation-only transition cues, but it
renders the resulting authoritative player coordinates and never predicts or
chooses either relocation.
Charged Bolt behavior `1` is a typed Lightning projectile. Firebolt behavior
`2` and Fireball behavior `16` are presentation-only: Godot
renders an orange travel projectile for phase `0` and a larger orange explosion
for phase `1`. The server owns each path, direct collision, splash targets,
damage, and expiry.
Blood Star behavior `20` and Bone Spirit behavior `21` are likewise
presentation-only. Godot renders their authoritative projectile state while
the server owns movement, homing, resistance, impact lifetime, damage, and the
physical self-damage applied at cast time.
Fire Wall behaviors `22` and `23` are presentation-only as well. Godot renders
the authoritative controller and persistent segment snapshots; the server owns
branch growth and stopping, segment duration, repeated fire damage, resistance,
and all persisted wall state.
Flame Wave behaviors `24` and `25` are presentation-only. Godot renders the
authoritative branch placement and moving segment snapshots while the server
owns travel direction, collision, damage, lifetime, and blocking.
Inferno behaviors `26` and `27` are presentation-only as well. Godot renders
the authoritative moving controller and stationary fire segments while the
server owns emission count, damage, lifetime, collision, and persistence.
Chain Lightning behaviors `28` and `29` are presentation-only. Godot renders
the authoritative fan-out and moving bolts; the server owns crawl order,
damage, segment creation, collision, and RNG.
Nova behaviors `30` and `31` are presentation-only. Godot renders the
authoritative radial controller and continuing Lightning balls; the server owns
the native mirrored fan-out order, movement, target collision, damage, lifetime,
and RNG. Apocalypse behavior `32` is its row-major scan controller and behavior
`33` is its persistent boom; Godot renders those as fire and larger FireImpact
families while the server owns scan order, line-of-sight, collision, damage,
lifetime, and first-hit persistence. Elemental behavior `35` uses the fire
family during travel and its burst phase. Guardian behavior `36` projects a
dedicated turret family. Flash behaviors `5`, `6`, and `7` project the native
two-layer blue flash family. Rage behavior `34` projects `HIDDEN`, so the
server-owned controller is never rendered as a generic projectile.
The server also projects `ProjectileSnapshot.presentation_kind` as an additive
visual family for every projectile. Godot prefers that server-owned metadata and
falls back to behavior/phase classification only when communicating with an
older peer that omits field 53.

For a manual production launch, use protocol `0.2.0`: set
`DEVILUTION_PROTOCOL_VERSION` to `0.2.0`, set `DEVILUTION_CONTENT_HASH` to the
server's printed **content manifest** hash, and set `DEVILUTION_RULESET_HASH`
to its printed **ruleset identity**, then open
`godot/Devilution.Client/project.godot`. Protocol `0.1.0` remains an explicit
compatibility mode that uses the ruleset identity for both hash fields.

When no resume token or character identity is supplied through the environment,
the client opens an in-game character setup form and sends the selected class
and name through the same authoritative handshake. The current prototype
supports movement, casting, store and inventory actions,
world and quest interactions, event feedback, diagnostics, and accessibility
HUD toggles. World layout under `Devilution.Client/assets/levels` is
presentation data and must stay consistent with server content.

The HUD also exposes authoritative equipment rows. Select an inventory item
and then an equipment row to submit an explicit server-owned transfer;
selecting equipment enables the stable-reference sell, repair, recharge, and
identify services; selecting an item in an open stash enables its applicable
repair, recharge, and identify service. Stash service targets require the
projected stash session to remain open. Godot never mutates equipment or
resolves slot topology locally.

When the snapshot contains unspent level-up points, the HUD displays the
server-owned primary attributes and enables one-point Strength, Magic,
Dexterity, and Vitality requests. The server validates the spend and returns
the resulting attributes and resources; Godot does not apply the increase
locally.

The store panel also exposes a `Close store` action. It sends the
server-authoritative `CloseStoreRequested` intent and waits for the snapshot to
remove the active store; repeated requests are safe and do not mutate local
vendor stock.

Right-clicking a visible monster or living remote player submits an
authoritative `AttackRequested` intent for that entity. The presentation client
resolves the clicked tile, including a remote player's discrete reserved walk
destination, to a snapshot entity ID; the server owns range,
timing, hit, damage, and action state. The protocol/server path admits adjacent
melee and line-of-sight ranged attacks against a same-level remote player when
friendly-mode rules allow it; it owns physical/elemental damage, target
resistance, shield/class blocking, equipment knockback, death, and PvP-ear
creation.
Left-click remains the cast or item-interaction path; when no cast source or
item target is available, it submits the additive authoritative target-cell
movement intent and the server resolves one deterministic path step.
When a visible unique monster has a server-authored dialogue identity, the
same left-click path resolves its matching active quest and submits
`TalkToQuestRequested`; Godot never advances the dialogue or mutates quest
state locally. Those monsters receive a gold dialogue marker derived from the
same authoritative snapshot field.
Authored routes also carry the additive `MonsterSnapshot.dialogue_state`
projection; Godot preserves the state for presentation and older/unauthored
routes retain the legacy unspecified value.

Targeted Firebolt, Charged Bolt, and Bone Spirit casts can also name a living
same-level remote player. The client supplies any visible remote-player entity
alongside the clicked tile; the server decides from the external spell behavior
whether that identity is a valid player target or merely a presentation hint for
the tile. It then validates friendly-fire, range, and missile line of sight,
owning projectile impact, resistance, blocking, damage, and death handling.

The cast cursor now exposes every source present in the authoritative snapshot.
Memorized spells and item-backed spell sources use server-provided content names
when available (with a numeric compatibility fallback), while the charged
left-hand staff, inventory consumables, belt consumables, open-stash spell
consumables, and the class ability appear in stable order;
`Q` and `E` cycle the selected source, and the HUD shows the active selection.
This changes only
which explicit source the client requests. The server still validates ownership,
targeting, timing, and resource consumption. Item source kind is projected by
the server; Godot does not inspect native miscellaneous IDs to identify staffs,
scrolls, or runes.

See `docs/ai/godot-build.md` for local build details and
`docs/authoritative-protocol-contract.md` for the client/server boundary.

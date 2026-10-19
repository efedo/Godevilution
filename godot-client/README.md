# Godot C# Client

This directory contains Godevilution's presentation client. It submits player
intent to the C# server, receives complete snapshots and transient events, and
renders the latest authoritative projection. It does not simulate gameplay or
own persistence.

This is a working presentation prototype, not a complete replacement client.
It currently uses one main scene and SVG stand-ins. Full asset coverage, menus,
audio, packaging, and a tested complete play flow remain open. The
[completion plan](../docs/completion-plan.md) schedules application acceptance
in M0, the playable loop in M1, and release presentation in M4; see
[migration status](../docs/migration-status.md) for the dated assessment.
The prototype uses no MPQs. A validated client-only presentation manifest now
binds the existing SVG sample, optional audio/localization, texture-atlas
metadata and player class/action/combat/facing rules, with safe fallbacks.
Static HUD captions can localize while control identities and server labels
remain unchanged. The frame clock is visual-only, not native action timing. See the
[presentation-pack contract](../docs/ai/godot-build.md#client-presentation-packs).
Complete asset coverage and optional original-asset tooling remain open; the
server has no archive dependency.

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

## Prototype controls and routes

The zero-argument root `run-godot-multiplayer.ps1` starts one authoritative
multiplayer host and two independently named Godot windows. Each window has its
own setup form and private resume bookmark in `.local/godot-multiplayer/player-1`
or `player-2`; both use the shared server saves beside them. Keep these files
together when backing up, and do not exchange the bookmarks. Use each window's
ordinary controls; closing one leaves the other running, and closing both ends
the owned host. This local profile starts in town with seed 0 and unchanged base
content. Normal single-player and preview launchers keep their original saves.

The responsive HUD separates Hero, Items, Trade, Stash, and World tabs from the
world view (minimum window 960x600). WASD/arrows move; Ctrl+left click requests a
step toward a tile without casting. Ordinary left click retains interaction or
selected-spell casting, and right click attacks a target. Q/E selects spell sources.
The always-visible Quick use belt row and keys 1–8 use the matching belt slot
without changing item or spell selection; the server remains authoritative for
whether use is allowed in the current context.
Explicit spell selection or Q/E cycling clears pending HUD item selection so
the next world click casts instead of dropping the previously selected item.
The authoritative held cursor retains its separate drop priority.

When the server reports zero life, Hero shows Restart in Town. The button stays
disabled until the projected death action has settled and the client is connected.
It and Escape submit the same recovery intent; only the server restores the player.

World lists the current level's server-projected routes. Travel repeats the
existing target-cell movement intent, waiting for each accepted command and
settled authoritative step. Cancel, Escape, manual movement, another action,
disconnect, death, a missing/unavailable route, or level change stops it; it never
resumes automatically. One already-submitted step may still finish after cancel.
No client pathfinding, collision, teleportation, or gameplay simulation is added.
Clicked items, portals, objects, traps, and dialogue targets use the same
acknowledged-step discipline until in range, then submit one interaction.
Escape or another gameplay action cancels that approach too. The server still
validates movement, range, target presence, and the interaction itself.
World also lists already-projected towners and portal endpoints on the current
level, including destinations outside the viewport. Select one and use Approach
and interact to walk there through the same server-acknowledged steps; Cancel
travel stops either repeater. This metadata selector does not reveal additional
world entities or bypass the server's interaction checks. Towner numbers remain
placeholder labels until the client localization/presentation pack supplies names.
Application acceptance covers the authored town-to-Cathedral encounter, distant
loot pickup, and fresh-host restore. The optional town-loop preview extends this
with equipment, a reverse stair, Smith purchase, and stash transfers. A separate
portal preview exercises scroll purchase/consumption, dungeon-to-town travel,
active-portal restoration, and owner-return closure through actual controls.
The separate zero-argument root `run-godot-lifecycle-preview.ps1` uses
`.local/godot-lifecycle-preview`, the exact-pinned `town-lifecycle-preview` overlay
and seed 5. Create a Sorcerer to exercise an itemless quest, charged staff,
Recharge, Firebolt, death and Town restart with normal starting resources.
Actual scene acceptance also verifies fresh-host resume and the exact off-level
staff drop. This is an authored diagnostic, not native population or complete
quest/class/campaign acceptance. Broader gameplay/recovery journeys, the complete
loop and generated-world parity remain open. Native-role store entry
requires the projected vendor's level, distance at most one tile (including
diagonals), and an empty held cursor; opening Stash requires Gillian (towner 7).
Use World to approach Smith (towner 0) before Trade or Gillian before Stash.
These are entry checks, not a completed open-service lifecycle or vendor-price
parity claim. Generic/custom stores and metadata-free compatibility paths remain.

Two-client application acceptance covers mutual movement/friendly-mode
visibility and fresh-host resume through both setup forms, including reversed
connection order. Legacy persistence can reallocate numeric entity IDs after
host restart; each private bookmark identifies the durable character. Explicit
complete-cohort acceptance additionally requires both stable entity bindings.
Separate explicit complete-cohort acceptance also retains two clients across
host loss, checks shared movement and retries committed-but-unobserved commands.
These checks do not certify cooperative combat, broader world interactions,
all fault scenarios or complete multiplayer/campaign recovery.

Items shows all seven equipment destinations, including empty slots. Select an
equipped item and an empty inventory cell to unequip it; select an inventory
item and an equipment slot to request equipping. The server validates the move.
For storage, approach Gillian, open Stash, select an item in Items, then choose a
stash cell.
Trade and Stash expose gold transfers and existing store stock; placeholder
stock is not a claim of complete native vendor generation or usable equipment.

## Projects

- `Devilution.Client.Protocol` references the shared generated
  `Devilution.Protocol` types and owns TCP framing, handshake logic, command
  retry, and the client-side authoritative model. It does not generate a second
  copy of the Protobuf bindings.
- `Devilution.Client.Protocol.Tests` verifies the protocol model and starts a
  real local server for live transport tests.
- `Devilution.Client` is the Godot 4.7.x Mono presentation project, with the
  checked-in C# SDK pinned to 4.7.1.

## Verify Without Godot

```powershell
dotnet run --project godot-client/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build godot-client/Devilution.Client/Devilution.Client.sln
```

The live protocol tests build and launch their own server. A Godot installation
is not required for these commands.
These checks do not instantiate the Godot scene or verify the visible
create-game flow. Use application/visible acceptance for those claims.

## Run the Local Prototype Session

Install a stable Godot 4.7.x Mono engine, then run:

```powershell
powershell -ExecutionPolicy Bypass -File run-godot-client.ps1
```

The root launcher is zero-argument and delegates to the local-session
harness. It discovers Godot Mono, builds the current server, imports assets in
an isolated copy of the current client sources, builds that imported copy, and
starts the host on an available port. Import-time SDK changes remain temporary;
the checked-in 4.7.1 SDK pin and local source edits are unchanged. Unsupported
engines or unexpected project upgrades fail before gameplay starts.
It validates single-player
mode and content/ruleset identities before launching Godot. The protocol client
retains the server-owned mode from the handshake. Normal launches keep server
checkpoints and a private resume bookmark under `.local/godot`; the next launch
offers Resume. A resume token must accompany the corresponding server saves.
Do not publish the token or reuse the same save directory for concurrent hosts.

Existing zero-argument launchers retain legacy persistence and their original
profiles. The inner harness accepts `-PersistenceMode cohort` only with an
explicit `-SessionDirectory` for a separate save/bookmark profile. It never
merges or migrates legacy saves. Cohort mode uses one leased, integrity-checked
`save/authoritative-cohort.json` for the whole authoritative world and roster;
keep it with the original world seed and private client bookmarks. Use the
[isolated acceptance commands](../docs/ai/godot-build.md#application-acceptance)
before adopting a new profile. Broader fault/platform acceptance and a policy
for conflicting legacy multiplayer saves remain open.

Use the separate zero-argument `run-godot-preview.ps1` root launcher for an
authored round trip. Its exact-pinned `town-loop-preview` overlay starts in town
with seed 0, adds a Cathedral return stair, and makes the existing encounter
drop an equippable native sword. Choose Rogue to follow the tested route.
World -> Travel reaches Cathedral; right-click the monster, click its drop,
and use Items to replace the starting bow. Select the preview return stair in
World, then select Towner 0 and Approach and interact before using Trade in town.
Select Towner 7 and approach Gillian before opening Stash. The starting 100 gold
can buy Smith's
25-gold buckler. No extra gold, portal scroll, or equipment is granted.
Saves/bookmarks live in `.local/godot-preview`, not `.local/godot`; do not mix
the two identities. The inner harness exposes this profile as `-PreviewContent`.

The separate zero-argument `run-godot-portal-preview.ps1` adds the exact-pinned
`town-portal-preview` stock overlay to that route. Buy the 50-gold Town Portal
scroll after approaching Smith before entering Cathedral, select it under Hero,
then left-click
the dungeon world to cast. Click the portal to return to town; the owner's return
through the town endpoint closes it. World can approach an offscreen declared
portal endpoint after resuming. Equipment, trade/stash and an active portal
survive a fresh host/client restart. This stock/price is an authored integration
sample, not native vendor generation or pricing parity. Starting resources stay
unchanged; saves/bookmarks use `.local/godot-portal-preview` and the inner flag is
`-PortalPreviewContent`. Original base/preview identities and saves are unchanged.

The accepted native Rogue baseline covers Town, earned L1 equipment/Gold,
Pepin supplies, L2 combat and Town return, followed by exact fresh-host restore
and retained two-floor revisit. The earlier L1-only route is a narrower
historical result. See [test history](../docs/test-history.md#native-cathedral-baselines)
for accepted scope and [working status](../docs/migration-status.md) for current
limits. Complete L1-L4 gameplay, other native classes, rendered parity and
normal-profile adoption remain open.

For the focused native chest journey, run `tools/test-godot-session.ps1` with
`-PersistenceMode cohort -CathedralNativeProfileDirectory <pinned-profile>`
and `-CathedralNativeChest -CathedralNativeChestSessionDirectory <copied-session>`.
The supplied session must be a copy of the passed final native Town cohort
under that profile's `sessions/` directory. The headless pair opens an ordinary
chest, saves in Town, then starts fresh host/client processes to verify retained
L1 loot/open state, no duplicate loot from repeat clicks and Town return.
The scoped pair passes from Town sequence 196 through checkpoint 272 and
fresh-host Town return 313, retaining two opened chests and one exact loot item.

The client reports server loss, blocks new gameplay input while disconnected,
and automatically reconnects when the same endpoint returns. Application
acceptance covers creation, fresh-process resume, and host termination/restart
with one Godot process retained through the outage. See the
[Godot build and acceptance notes](../docs/ai/godot-build.md) for commands and
the distinction between headless, visible, and full campaign evidence.
For diagnostics or automated smoke tests, invoke the underlying
`godot-client/run-local-client.ps1` harness directly with its optional switches.
For an automated smoke launch, add `-Headless -QuitAfterSeconds 5`.
Pass `-CharacterClassId <0..5>` to request a recognized shipped class ID when
creating a new session. Admission depends on the selected profile: the explicit
native populated/progression profiles currently admit only canonical Rogue in
single-player Normal. Other profiles may admit the broader class catalog.
Omitting the option uses the profile/content default; a resume
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
Command scheduling accounts for monotonic time since the latest valid snapshot
arrived before adding the adaptive retry lead. This prevents stale 250ms polling
snapshots from making ordinary movement late when retry estimates fall to 150ms.
Godot queues interactive input with `QueueRealtime`: the requested tick is chosen
under the transport write lock immediately before its first dispatch, so a stalled
frame cannot consume its lead while it is still unsent. The explicit `Queue`
API retains caller-supplied ticks for scripted commands. Once a write is attempted,
all retry/reconnect command bytes remain fixed, even after a partial/failed write.
Concurrent polls are serialized; replacement handshakes wait for the old poll to
finish against the closed transport. Retransmitted ACKs do not supply ambiguous
round-trip samples. Rejections remain terminal, not automatically rescheduled.
The timing anchor resets on reconnect; none of this advances authoritative state
or presentation time, or relaxes server admission. Host/network stalls after
dispatch can still exceed the normal adaptive lead and require separate profiling.
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
Authored native presentation identities are also rendered when available: the
Warlord armor stand and weapon rack use distinct stand/rack silhouettes from
their server-projected presentation families (`NATIVE_ARMOR_STAND` and
`NATIVE_WEAPON_RACK`). Older snapshots may fall back through identities `89`
and `90`; unknown identities and raw DUN layer values retain the generic
fallback, and the client never interprets raw layer conversion IDs.
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
`godot-client/Devilution.Client/project.godot`. Protocol `0.1.0` remains an explicit
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

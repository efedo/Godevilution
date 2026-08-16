# Godot C# Client

This directory contains Godevilution's presentation client. It submits player
intent to the C# server, receives complete snapshots and transient events, and
renders the latest authoritative projection. It does not simulate gameplay or
own persistence.

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
The `Main` presentation node observes transport closure and poll failures, then
retries with bounded exponential backoff while retaining the same authoritative
model and pending-command ledger.

An authoritative snapshot may also contain an in-progress movement, attack,
cast, death, level-transition, or restart-in-town action. The protocol model
validates its timing and payload. For deferred movement, local presentation predicts from the
server-reserved destination so the accepted command does not snap back before
the authoritative end tick. Death and portal transitions arrive already
applied with server-owned Death or NewLevel modes; Godot never resolves their
damage or destination effects itself.
After terminal Death, Escape sends `RestartTownRequested`. The server chooses
the destination and resource reset; Godot accepts action kind 6 only as an
already-applied NewLevel transition and renders the returned town snapshot.
If a Gold pickup exceeds inventory capacity, the snapshot carries the remainder
as `held_item`. Godot displays it and sends `DropHeldItemRequested` for a clicked
tile (or the current tile with `X`); it never chooses the accepted placement or
mutates the item locally.
Dynamic world items such as PvP ears carry authoritative runtime names and a
native cursor identity. Godot validates and renders those fields from the
snapshot; it does not recreate them from the victim or local class data.

Snapshots contain every assigned participant. The server puts this client's
player first and orders the remaining players by entity ID. The client validates
all identities and action state, applies prediction only to that first player,
and renders same-level remote players from their authoritative coordinates.
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
Teleport behavior `9` and Phasing behavior `15` are invisible movement
controllers. Godot renders only the resulting authoritative player coordinates
and never predicts either relocation.
Fireball behavior `16` is also presentation-only: Godot renders an orange
travel projectile for phase `0` and a larger orange explosion for phase `1`.
The server owns its path, direct collision, splash targets, damage, and expiry.
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
Nova behaviors `30` and `31`, Apocalypse behaviors `32` and `33`, Rage
behavior `34`, Elemental behavior `35`, and Guardian behavior `36` are
presentation-only. Godot renders the
authoritative radial controller and continuing Lightning balls; the server owns
the native mirrored fan-out order, movement, target collision, damage, lifetime,
and RNG.

For a manual production launch, use protocol `0.2.0`: set
`DEVILUTION_PROTOCOL_VERSION` to `0.2.0`, set `DEVILUTION_CONTENT_HASH` to the
server's printed **content manifest** hash, and set `DEVILUTION_RULESET_HASH`
to its printed **ruleset identity**, then open
`godot/Devilution.Client/project.godot`. Protocol `0.1.0` remains an explicit
compatibility mode that uses the ruleset identity for both hash fields.

The current prototype supports movement, casting, store and inventory actions,
world and quest interactions, event feedback, diagnostics, and accessibility
HUD toggles. World layout under `Devilution.Client/assets/levels` is
presentation data and must stay consistent with server content.

Right-clicking a visible monster or living remote player submits an
authoritative `AttackRequested` intent for that entity. The presentation client
only resolves the clicked tile to a snapshot entity ID; the server owns range,
timing, hit, damage, and action state. The protocol/server path admits adjacent
melee and line-of-sight ranged attacks against a same-level remote player when
friendly-mode rules allow it; it owns physical/elemental damage, target
resistance, shield/class blocking, equipment knockback, death, and PvP-ear
creation.
Left-click remains the cast or item-interaction path.

Targeted Firebolt, Charged Bolt, and Bone Spirit casts can also name a living
same-level remote player. The client supplies any visible remote-player entity
alongside the clicked tile; the server decides from the external spell behavior
whether that identity is a valid player target or merely a presentation hint for
the tile. It then validates friendly-fire, range, and missile line of sight,
owning projectile impact, resistance, blocking, damage, and death handling.

The cast cursor now exposes every source present in the authoritative snapshot.
Memorized spells and item-backed spell sources use server-provided content names
when available (with a numeric compatibility fallback), while the charged
left-hand staff, inventory consumables, belt consumables, and the class ability
appear in stable order;
`Q` and `E` cycle the selected source, and the HUD shows the active selection.
This changes only
which explicit source the client requests. The server still validates ownership,
targeting, timing, and resource consumption. Item source kind is projected by
the server; Godot does not inspect native miscellaneous IDs to identify staffs,
scrolls, or runes.

See `docs/ai/godot-build.md` for local build details and
`docs/authoritative-protocol-contract.md` for the client/server boundary.

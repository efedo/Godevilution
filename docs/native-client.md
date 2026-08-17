# Native Compatibility Client

The inherited C++/SDL client remains available for legacy play and for parity
testing while Godevilution moves authority to the C# server.

The default CMake configuration builds the unchanged legacy-authority path. To
include the server-backed adapter, configure with:

```powershell
cmake -S . -B build-authoritative -DDEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON -DBUILD_TESTING=ON
cmake --build build-authoritative
```

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
The compatibility layer does not yet create complete legacy remote-player
presentation objects from participant snapshots, so broader native multiplayer
rendering remains migration work even though targeting and authoritative state
are retained.

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
active quest's content-declared towner is presented. The server owns adjacency,
native conversation variables, required-item consumption, quest completion,
unique-reward spawning, and the dialogue event; the adapter projects that
dialogue into the native quest-text UI. The Hellfire Anvil of Fury/Blacksmith
path is the first shipped definition. Legacy local towner behavior stays
available outside the authoritative runtime.

Content-declared unique-monster conversations use the same boundary. When the
projected live unique identity matches an active quest, the native client sends
`TalkToQuestRequested`; the server validates the quest, target identity, level,
and adjacency before returning the dialogue event. The Hellfire Defiler
conversation is the first shipped definition. Unauthored legacy monster
conversations retain their local fallback until their content actions are
externalized.

Content-declared quest objects now use the same `OperateObjectRequested`
boundary. `QuestSequence` and `QuestItemCompletion` effects keep ordering,
quest-state changes, item consumption, and activation in the server snapshot;
the Hellfire Na-Krul tome sequence is the first shipped definition. Objects
without an authored quest effect retain the legacy local behavior.

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

# Devilution.Diablo

`Devilution.Diablo` is the first-party game module for authoritative Diablo and
Hellfire behavior. It targets .NET 8, references the Domain kernel, Data runtime,
and Engine capability contracts, and has no dependency on the server host,
Protobuf, Godot, or filesystem persistence.

The module currently owns:

- authoritative item state, item generation, affixes, unique items, stores,
  and service pricing;
- protocol-independent player attributes, owned inventory, equipment, belt,
  and read-only authoritative player projections;
- a validated player-class catalog with native-compatible IDs, base and
  Hellfire metadata, content-selected starting attributes, and exact native Q6
  starting-life and starting-mana formulas;
- validated per-class starting wallets, equipment, belt and inventory layouts,
  native class skills, learned spell levels, deterministic item seeds, and
  native item-index/charge compatibility metadata;
- protocol-independent store/inventory intents, results, mutable state, and
  deterministic transitions, including multi-cell placement and swaps;
- protocol-independent world state, navigation and occupancy rules, movement,
  portals, world-item pickup, object effects, quest transitions, and gameplay
  events;
- combat rules, damage types, monsters, drops, spells, projectiles, combat
  intents, status advancement, deterministic autonomous combat ticks, and
  content-selected player-death Gold loss/drop/pickup with native inventory
  ordering, seed reuse, grid compaction, cursor-held overflow/drop/death
  handling, aggregate-save compatibility, and explicit-mode multiplayer
  suppression selected by validated level metadata;
- canonical starting Gold plus native-order store spending, purchase placement,
  sale top-off/new-pile creation, inventory-only transfer enforcement, and
  transactional capacity rejection;
- a shared-state `DiabloSimulation` aggregate and engine-registered simulation
  factory;
- an engine-registered content bootstrap that validates the declared schema,
  selects normalized or legacy table variants, constructs every Diablo catalog,
  and returns activated simulation state;
- stable protocol-neutral player/world/store snapshots plus module-owned restore
  validation;
- normalized player templates and lifecycle hooks consumed by the generic Engine
  runtime, including independent per-session templates selected by stable class
  ID;
- levels, portals, objects and their effects, quests, statuses, and world
  entities;
- transport-independent Diablo store validation and pricing results.

`Devilution.Server` is the composition-only executable. The separate
`Devilution.Server.Diablo` adapter translates
Protobuf store, inventory, world, attack, and cast commands into module intents;
supplies host-ordered players at tick boundaries; maps module failures and
events into protocol messages; and resolves capabilities from the engine
registry. `DiabloModule` registers its rules, simulation factory, and content
bootstrap through the typed `Devilution.Engine` capability registry. Server
passes the bootstrap only generic `ComposedContent`; table names, fallback
selection, schema validation, and catalog construction remain here. The
protocol adapter must remain thin; Diablo state transitions, formulas, and
content schemas belong here.
The Engine-owned generic session coordinator now supplies deterministic player
ordering, reverse state lookup, serialized state access, exactly-once tick
advancement, and transient event queues. The Server keeps the versioned Protobuf
and JSON compatibility representations outside this module. A Server-owned
Diablo adapter bridges this module to those representations behind the generic
`IAuthoritativeGameHostAdapter`; the executable composition root does not
reference Diablo types.

`AuthoritativeWorld.CaptureGeometryState` captures detached backing levels and
warps, including native layers and explicit-versus-derived vision. Restore
prevalidates a temporary world before replacing its backing dictionaries. It
preserves registered routes made temporarily blocked by `ReplaceLevel`; new
public warp registrations retain their existing walkability checks.
`StoreCatalog.CaptureAll`/`RestoreAll` cover every registered store, including
closed or empty stock, with exact content-owned identities/roles and detached
item state. Both helpers require the caller's simulation barrier.

`DiabloSimulation.CaptureCohortState` captures the complete detached module graph
at the owning Engine runtime's exact barrier tick: ordered participant bindings,
raw players/actions, monsters and historical drop templates, items, objects,
full quests, projectiles, portals, corpses, stores, RNG, backing/effective geometry,
cached mutations, procedural markers, nullable portal clock and allocator cursors.
The six processor cursors retain zero exhaustion; the separate raw procedural
monster cursor retains legacy zero-to-one and occupancy-skipping behavior.
Do not infer cursors from surviving entities or refresh cached projection on capture.

`DiabloSimulationFactory.CreateCohortCandidate` validates a fresh, detached
candidate against supplied trusted active content/rules catalogs. It restores
without ordinary startup scaling, generation, entry-event replay or participant
creation. Full checkpoints require bound nonzero participant identities, exactly
one owning runtime, and module-owned class/item policies (or explicit absence).
Unsupported independent template policies and unbound legacy players are rejected
only by this new checkpoint path; ordinary runtime APIs remain unchanged.
The candidate owns one fresh runtime installation and does not implicitly admit
new participants. The host must provide explicit trusted admission and
outer content/ruleset identity fences. Transport, JSON, durable publication and
command ledgers remain outside this module. The opt-in cohort host now uses this
API for complete-world cold recovery, including scoped two-client acceptance.
Normal launcher profiles still use legacy persistence; their adoption of cohort
mode requires approval and broader acceptance. Legacy save/import behavior
remains unchanged.

When validated towner greetings are supplied, native-role store entry and
Gillian's stash require the declared NPC level, Chebyshev distance at most one,
and no held item. Reserved Adria/Cain IDs retain their town-only guard. Rejected
entry occurs before inventory-dimension hydration; accepted entry and the
metadata-free compatibility path retain hydration. Custom Generic stores are
unchanged. A successful greeting to the roster-bound Healer (towner ID 1) restores a
living player's current life to LifeMaximum without changing mana or maxima. Ongoing
item actions do not reapply the entry cursor/range check;
closing services remains possible anywhere. The simulation clears a metadata-bound
native vendor session after an accepted cross-level warp/portal into a non-town
level, matching native level-load store cleanup. Rejected/dry-run transitions,
same-level movement/warps, town returns, Generic and metadata-free stores retain
their existing behavior. This does not change stash sessions, save/restore, general
inventory hydration, or pricing; complete service lifecycle parity remains open.

The ordinary Cathedral level-1 geometry adapter expands the generator's
one-based coarse IDs through validated external TIL/SOL catalogs into native
112x112 fine coordinates at offset (16,16), with exterior mega 22 and local
Cathedral SOL corrections. Its immutable result retains the separate 40x40
coarse/seed/stair output and fine level rather than changing generic DUN or wire
geometry contracts. No MPQs are read by this module. The separate opt-in native
oracle now matches full coarse/fine layers and named SOL flags for ordinary
depths 1-4, six seeds and MAIN/PREV entry. Its temporary raw-SOL normalization is
test-only. A separate opt-in four-depth runtime and private playtest profile
support traversal/restore; native population, campaign mapping, broader topology
and remaining families are still separate gaps. Exact shared vision parity is
not claimed: the existing level
contract conservatively includes movement blockers in vision blockers. See
`docs/ai/msvc-build.md` for the isolated local-asset comparison.

The separate native Rogue L1 oracle uses the playable profile's fresh hero tuple
(Q6 life 2880, mana 1440, idle frames 8, pre-entry future 57,40; MAIN seed 2588).
It compares complete raw population and preparation state through CreateThemeRooms
against the native loader. Select `CaptureNativeRogueL1Population` and
`PlayableRogueL1TupleMatchesNativeLoader` with the dedicated
`GODEVILUTION_CATHEDRAL_NATIVE_ROGUE_L1_ORACLE_DIRECTORY`; retain outputs outside
the repository. The accompanying native-rogue-l1-inventory.tsv captures actual
CEL cursor sizes and inventory cells (pixel dimensions divided by 28, rounded
down); the opt-in profile must use these dimensions. This proof preserves the
existing corpus and does not establish live populated gameplay or normal-profile
adoption.

The separate `Cathedral.Playtest --native-cathedral-progression` profile selects
`devilution.diablo.cathedral-native-progression.v1` for Rogue, Original Diablo,
single-player Normal Town/L1/L2. L2 is generated once at its actual first stair
entry from the hero tuple and shared unique history. Detached admission checks
geometry, actors, objects, item bindings and collisions before publication;
revisits reuse each floor's immutable owner and retained mutations. Native
initial skeleton SpecialStand phases pause off-level and survive cold restore.
Depth-owned chest/sarcophagus/barrel loot and Arrow impact scaling include L2;
useful contents consume the native L2 portal-scroll roll. Legacy compilation
retains its exact 110-row semantics; progression explicitly adds five jewelry
rows. Unsupported wall-trap
variants remain refused. Cohort v9 records first-entry contexts and reconstructs
both trusted owners before mutable restore; v1-v8 retain their prior schema.
The profile remains explicit and makes no whole-campaign parity claim.
Queued consumables that execute after death are refused before hydration, item
removal or RNG use; potions cannot leave a living player in a Death action.

The offline `Cathedral.Playtest --native-cathedral-population` mode prepares a
separate Rogue, original-Diablo, single-player Normal L1 profile. It requires the
complete population metadata pack, native monster-runtime table, captured item
footprints, and a geometry directory containing only
`native_dun_piece_definitions.tsv` and `native_dun_tile_properties.tsv`. Its depth
seeds are `[2588,2588,1,4294967295]`; it generates item semantics and selects exact
native stock identities in the output. Existing profiles keep their catalogs.
The profile requests native population with `nativePopulationClaim=false`.
Canonical TSV digests cover LF content; pack digests cover published CRLF bytes.
The scoped native Town/combat/earned-gear/fresh-host/Town-service route passes
headlessly; normal-profile adoption remains held.

The opt-in L1 policy permits unopened, untrapped ordinary `OBJ_CHEST1` through
`OBJ_CHEST3`. Native seed/count/branch fields drive the two loot branches,
including their distinct full-pool RNG behavior. Opening is one-way
(activated, animation frame +2); raw provenance, item pools and opened state
survive cold restore. Initial generation identity remains compatible with
existing native cohorts. Trapped and unsupported variants remain refused.
`ThemeItemPlacementTest.CaptureNativeChestOperations` writes the 36-case
`native-chest-operations.json` corpus (six live-animation, 30 loading cases).
Set `GODEVILUTION_CATHEDRAL_CHEST_ORACLE_DIRECTORY` to a new capture directory
for that native test, then use it for
`AuthoritativeCathedralChestLootNativeOracleTests.OrdinaryChestLootMatchesNative`.
The native overlay completes newly created active Gold stacks from its admitted
raw identity and denomination cursor. Revaluation validates the original stack
before changing its value/cursor; ordinary catalogs retain their existing state.
This keeps the managed inventory contract `IdentifiedValue == Value`, and does
not claim native MakeGoldStack scalar parity. Fresh world drops and strict cold
restore are covered. Approved inactive-slot removal preserves the updated Gold
value/cursor and refuses malformed metadata before mutation; bound-world
revaluation now follows the admitted denomination cursor through cold restore.
The explicit native L1 owner resolves bow hit/damage at collision through the
shared RNG, and persists arrow distance plus attack, recovery and walk animation
phases in cohort v5. Walks retain their destination reservation until the native
terminal frame; visible native actors use their active window instead of legacy
aggro radius. Older schemas keep their prior bytes and require trusted timing
hydration; existing legacy moves complete on their original next update.
The managed native Town/combat/earned-gear/cold-resume/stash journey and in-flight
arrow, melee and walk continuation pass, as does the scoped Godot native-profile
route. Full native AI/world-loop parity and normal adoption remain open.

Timed casts consume their source at the native effect frame, before the animation
ends. Save/restore retains a bounded historical consumable reference afterward,
without replaying consumption or the resolved effect. Pre-effect and charged-item
ownership checks remain unchanged.

Run the independent xUnit v3 suite from the repository root:

```powershell
dotnet run --project server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj
```

The suite includes the checked-in base content pack and deterministic class,
loadout, item, store, combat, spell, projectile, autonomous-tick, status,
inventory, navigation, portal, pickup, object-effect, quest, and
world-validation coverage.

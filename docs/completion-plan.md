# Godevilution Completion Plan

Plan date: 2026-09-30. Implementation baseline: `8c03b365d`.

This is the active delivery plan. The [architecture](csharp-authoritative-server-migration.md)
defines the objective and ownership rules; [migration status](migration-status.md)
owns current estimates, evidence, and unresolved gaps. Historical plans are not
active backlogs. Milestones below are acceptance gates, not claims of completion
or calendar estimates.

## Objective and completion contract

Deliver a DevilutionX-compatible Diablo/Hellfire implementation with a C#
authoritative engine and a Godot C# presentation client. Single-player and
multiplayer use the same server and protocol. Domain primitives, external
validated data, engine composition, game-specific rules, hosting, and
presentation remain separate. The server owns all gameplay, randomness, world
state, and persistence. Production gameplay and presentation require neither
embedded Lua nor the C++/SDL runtime.

Execute this goal through the milestone dependencies and acceptance gates in
this plan, with current evidence recorded in `migration-status.md`. The primary
agent owns planning, integration, acceptance, documentation, and cohesive
commits. Delegate substantial, bounded implementation packages to GPT-5.6 Luna
workers and independent critical review to a separate GPT-6.1 Sol reviewer,
both at extra-high reasoning effort,
following the [worker delegation policy](#worker-delegation). Use focused tests
per commit and broader integration/application acceptance at the relevant gates.

The C# server must never require MPQ archives, an installed game, or a native
client. It consumes explicit validated content packs. The finished Godot client
must support a complete presentation pack without MPQs; optional use or local
conversion of legally supplied original assets belongs exclusively to client
tooling. Do not silently transfer archive loading or installation discovery to
the server. Preserve the inherited license and provenance notices.

Completion requires all of the following evidence:

- Full supported base-game and Hellfire progression, including new characters,
  town, dungeon families, set levels, quests, inventory/services, combat,
  death/recovery, final encounters, difficulty progression, and save/resume.
- Matching native behavior for supported classes, rulesets, modes, seeds,
  timing, fixed-point arithmetic, RNG order, and relevant edge cases; exceptions
  remain explicit gaps until resolved, never silently redefined as parity.
- Playable Godot menus, input, world rendering, animation, audio, HUD, feedback,
  accessibility, reconnect, and error handling with no client gameplay owner.
- Preserved supported protocol, content, and save compatibility; reliable
  multiplayer/restart recovery and validated deployment artifacts.
- A production dependency graph free of C++/SDL, embedded Lua, and mandatory
  MPQs, with the native compatibility build retained in isolation while it is
  still required by compatibility or parity tests.
- Eventual physical Lua removal from the active project once all supported
  capabilities have replacements. Disabling it or retaining it in an isolated
  compatibility build does not close this separate removal objective.

## Baseline and strategy

The dependency boundaries, host, shared protocol, persistence, content loaders,
and many gameplay slices already exist. Reuse them. Godot has substantial
protocol coverage but placeholder presentation. Native dungeon synthesis and
full campaign integration remain major gaps. See the dated
[assessment](migration-status.md#progress-assessment) for estimates and their
limitations; do not infer completion from commits, fixture counts, or schema
version numbers.

Use playable milestones to select work. Repair a gameplay family and its
dependent content, restore, protocol, and presentation paths in one coherent
batch. Add parity cases for that batch's risks. Avoid an open-ended sequence of
isolated spell or table rows while the playable route stays unchanged.

| Gate | Deliverable | Dependencies | Current position |
| --- | --- | --- | --- |
| M0 | Reproducible launch and honest acceptance evidence | Existing implementation | Godot session/recovery passes; opt-in one/four-participant host and actual-client baselines recorded in status; native GUI and release-budget/platform gaps remain |
| M1 | Usable Godot town-to-dungeon loop | M0 launch/acceptance harness | Authored combat/loot/equipment, real NPC approach/service entry, purchased-scroll portal return and fresh-host owner-return closure pass; full loop remains |
| M2 | Native-compatible generated world | M0 geometry/parity baseline | Ordinary Cathedral level-1 coarse/fine oracle passes two seeds and MAIN/PREV entry; runtime integration, population and remaining families remain |
| M3 | Complete base and Hellfire gameplay routes | M1 plus the relevant M2 world slices | Many rules exist; campaign and remaining behavioral coverage remain |
| M4 | Release-quality client, recovery, and packaging | M3; preparation begins during M1/M2 | Recovery and adapter packaging foundations exist; product acceptance remains |
| M5 | Production legacy-runtime retirement | M4 and capability replacement gates | Authoritative mode is Lua-free; compatibility runtime remains |

M1 and M2 can progress together after their M0 prerequisites. M3 work can start
as individual M2 level families become available; it need not wait for every
map generator. Asset presentation starts in M1 and improves through M4. Recovery
and multiplayer checks accompany every slice rather than being postponed until
M4. M5 is the final removal gate, not a reason to postpone replacement work.

## M0: Reproducible launch and acceptance baseline

1. Preserve the zero-argument native and base Godot launchers, plus the isolated
   optional Godot preview and two-client multiplayer launchers. Make Godot's launcher build the
   current server and client assembly, validate handshake mode and identities,
   report startup failures, and clean up only processes it owns. The Godot
   harness now rebuilds both projects, imports assets, and retains local saves
   and a resume bookmark. Preserve these guarantees with application tests.
   Isolate writable engine settings/cache for every importer/client process.
   The reported Windows error-dialog accumulation was reproduced as an editor
   import access violation with denied AppData writes. Check native process
   exits and fatal import logs, retain stage process evidence, and fail before
   launching another phase. Scene success alone is not clean-process acceptance.
   Stage current client sources per launch, import/build/run only that copy,
   and retain engine/SDK evidence. Preserve the source SDK pin, local edits,
   upgrade backups and saves; reject unsupported or unexpected migrations.
   Do not import separate save profiles into a shared client cache.
   Preserve the retained fresh-host two-client resume `TooLate` failure and strict
   authoritative admission. Clean process cleanup is not multiplayer acceptance.
   The loopback counterexample now isolates peer-driven future-tick advancement;
   the clock-gated scheduler is now integrated under an explicit whole-host
   capability negotiation, with physical-clock sampling, queued sequence resume,
   checkpoint publication, restore/reload fences and awaited shutdown. Godot
   profiles select it; default CLI/native timing stays immediate until the native
   synchronous transport is separately adapted and verified. Preserve the legacy
   direct API and strict late policy. Final-source host regressions and actual
   headless/rendered two-client creation/mutual movement/reverse-order fresh-host
   resume now pass, alongside single-player creation/resume/server-loss recovery
   and the corrected portal TownLoop/resume. Keep delayed ACK, timing probe,
   failure and epoch-composition regressions; these scene/rendering checks are
   not campaign acceptance. Initial unmeasured
   latency and later spikes are not bounded by historical probes. Pending-command
   fences do not solve the adapter's broader shared-world cold-restore overwrite.
2. Add an application-level acceptance path through the actual Godot scene and
   controls: launch, create a character, receive/render an authoritative world,
   submit movement, observe the acknowledged position, save, exit, and resume
   through a fresh host. Use isolated test saves and deterministic content.
3. Cover native launch-to-new-game with the same explicit single-player mode,
   current binaries, and available local assets. Retain the existing launcher
   contract and native mode-policy tests; they are useful but do not prove GUI
   creation. Missing proprietary assets produce an explicit unverified native
   acceptance result, not an assumed pass or a blocker for Godot work.
   Actual GUI startup now exposed and corrected a native/managed reward-offset
   limit mismatch. Preserve visible connection-error recovery and the boundary
   regressions. Deferred native projection and graphics/frame admission now
   address the prior object-graphics assertion in source and focused tests.
   Complete the fresh visible check after manual cancellation of the Windows
   firewall prompt, then exercise movement/save/resume before claiming native
   application acceptance. Do not enable network permissions for loopback tests.
4. Record a fresh integration baseline for the current revision. Capture exact
   commands, configuration, selected tests, outcomes, and any exclusions in
   migration status. Establish the target platform/participant matrix and
   measured tick/frame/memory budgets there before claiming performance gates.
   The opt-in standalone host workload now measures one/four participants over
   loopback; it is a preliminary baseline, not a Godot-frame or release-budget
   pass. Preserve settled authoritative outcomes when extending this workload.
   The separate opt-in actual Godot workload uses fresh one/four-client profiles,
   real controls, all-observer staging/round barriers, bounded callback evidence
   and clean engine exits. Keep callback-body cost separate from start intervals
   and GPU/whole-frame timing; last-GC heap and lifetime peak working set are not
   live allocation or soak evidence. Preserve failed captures and reject invalid
   metrics. Ordinary launches and two-client create/resume remain unchanged.
   Local Debug town measurements do not establish a release budget or full
   platform/campaign matrix; use status for exact configurations and outcomes.
   Use the recorded checkpoint-phase timings to target publication costs without
   weakening durability; retain no-ack-on-write-failure and fresh-host duplicate
   recovery coverage. Local Windows receipt-file reuse does not close the wider
   performance/platform gate. Client queue-to-first-dispatch stalls now have a
   reproduced transport regression and actual-scene acceptance; preserve deferred
   first-send timing and immutable retry identities. Continue reproducing the
   recorded heavy-load route `TooLate` rejection with host/network timing evidence.
   This client fix does not certify admission under arbitrary post-send stalls;
   do not relax server authority or silently retry rejected gameplay.
   The earlier two-window rendered resume timeout also remains unexplained;
   use the bounded per-input queued-sequence/receipt/snapshot traces to distinguish
   input loss, rejected admission and unsettled movement. Acceptance now recognizes
   successful duplicate receipts while still rejecting duplicates of rejected
   commands. That test correction and passing reruns do not establish the older
   timeout's cause.

Exit: the Godot launch/create/move/save/restart/resume journey passes from
rebuilt artifacts without MPQs; visible UI is checked separately from headless
execution; native creation has recorded evidence or an explicit asset-related
gap. Server loss must show failure/reconnect without local simulation fallback.
The native creation gap remains mandatory before M4 compatibility sign-off.

## M1: A usable Godot gameplay loop

Use an existing authored level and deterministic encounters initially. This
deliberately does not claim native generated-world parity, which belongs to M2.

1. Integrate session/new-character/resume menus, persistent local resume-token
   handling, disconnect feedback, and clean return/exit flows. Authoritative
   saves remain on the server; launching again must not accidentally discard
   access to the previous session's persistent save directory. Local bookmarks
   are implemented; expand the complete menu/recovery journey from that base.
2. Build a coherent town -> dungeon -> combat -> loot -> inventory/equipment ->
   portal -> vendor/stash -> save/resume journey. Add one quest interaction,
   death/restart, class skill, and resource-consuming cast to expose integration
   gaps. Reuse existing server intents and protocol models. Responsive tabs,
   explicit tile walking, and cancellable server-controlled route traversal now
   cover town-to-Cathedral entry through actual controls. Combat/loot and
   fresh-host restore now extend that acceptance. The optional exact-pinned
   preview adds equippable earned loot, reverse stairs, Smith purchase and stash
   transfers without changing base saves or granting resources. A separate
   exact-pinned portal overlay now adds a purchased, consumed scroll, actual cast,
   town return, active-portal fresh-host restore and owner-return closure.
   Preserve original and new profile identities/saves. Declared local-level NPC
   and portal destinations now support cancellable, acknowledged approach through
   World controls. Native-role vendor/stash entry requires the declared NPC level,
   adjacency and empty cursor; metadata-free/custom-store compatibility remains.
   Owned portals take priority over coincident automatic stairs. Metadata-bound
   native vendors now close after accepted cross-level entry into a non-town level;
   stash and compatibility-store policies remain unchanged. Close remaining
   native placement exclusions, full service lifecycle/pricing, quest, death/restart,
   class skill, and cooperative gameplay/recovery journeys. Two actual Godot clients now
   have basic town movement/state visibility and reverse-order fresh-host resume
   acceptance; retain local-first identity and separate bookmarks when expanding
   it to combat/world interactions. These authored previews do not close M1.
3. Give geometry, actors, projectiles, items, and interaction targets readable
   Godot presentation. Split the main scene/script only where this separates
   concrete input, rendering, menus, or HUD responsibilities. Keep placeholder
   art usable while the client asset-pack boundary is established.
4. Define that presentation boundary with stable IDs, missing-asset fallbacks,
   animation metadata, audio/localization lookup, and a small redistributable
   sample pack. Any original-asset importer is optional client tooling with
   explicit input/output and an MPQ-free alternative.

Exit: a person can complete the loop through Godot controls without debug
commands; application tests observe authoritative outcomes and visible-state
transitions. Two Godot clients can join one multiplayer host and observe each
other's actions. Automated model/transport tests supplement, but do not replace,
this evidence. A sample art pack does not satisfy M4's full presentation gate.

## M2: Native-compatible world generation and placement

1. Inventory the native generators, seed streams, level/quest selection,
   geometry layers, stairs/warps, set pieces, objects, and encounter placement.
   Extend the existing world-gap record in migration status with a row per
   family; record source reference, implemented path, and missing oracle/data.
2. Complete Cathedral as the first generated family integrated with M1, then
   Catacombs, Caves, Hell, Hive, Crypt, and dependent quest/set levels. Treat
   town and transition topology as part of the same world contract. Adjust
   independent family order only when it reduces a documented dependency.
   The ordinary original-Diablo level-1 coarse port now matches two native DUN
   goldens, continuation RNG and successful-attempt restoration but has no
   runtime call sites. Its isolated adapter now expands validated TIL/SOL catalogs
   into 112x112 fine coordinates while retaining the separate coarse result.
   Full fine-layer comparison using locally supplied original assets now passes
   seeds 2588/743271966 with MAIN/PREV entry. The optional oracle retains raw data
   privately and normalizes only the unnamed SOL bit for test-only named-flag
   comparison; production loaders are unchanged. Broader seeded/topology checks,
   runtime/coordinate integration, population and route acceptance remain.
   The shared vision contract remains conservative, not exact native light parity.
   Integrate validated external pattern data; do not invent campaign seed mapping
   or add server MPQ loading. Keep licensed oracle inputs outside the repository.
3. Compare seed-for-seed native and managed generation: RNG consumption,
   mega-tiles/d-pieces, walk/missile/vision blockers, transparency, entrances,
   reachable routes, set-piece anchors, object and encounter placement. Reuse
   current DUN/TIL/SOL loaders and authored mutation contracts. The generic
   connected-grid generator is not a substitute for native map synthesis.
4. Keep map generation and mutable world state on the server. Preserve geometry
   and world-seed identity through transitions, reconnect, and restart. Expand
   replay representation only where a required world scenario cannot otherwise
   be asserted; do not build another production simulation in the harness.

Exit per family: seeded native comparison, topology/collision checks, populated
Godot traversal, object/encounter interaction, and restore all pass. Synthetic
test fragments validate contracts but do not prove complete native set pieces.
Track unavailable original asset references precisely and keep unrelated work
moving; do not fabricate native-equivalent assets or add MPQ runtime dependence.
M2 closes only when every supported family and required set level meets its gate.

## M3: Gameplay and content closure by playable route

Use the following work packages as a coverage checklist, not another numeric
progress scoreboard. For each package, record open behavior and evidence in
migration status; put detailed fixture semantics in the replay reference.

| Package | Required closure |
| --- | --- |
| Player lifecycle and classes | All six classes, action timing/chaining, resources, equipment modifiers, class skills, difficulty, death, restart, and resurrection |
| Combat and spell families | Native scaling, target admission, collision, owner-specific RNG, simultaneous controller/projectile ordering, resistance/block/recovery, and PvP/friendly-mode rules |
| Monsters and summons | Every supported AI/encounter family, occupancy/pathing, target selection, autonomous timing, unique behavior, minion ownership, multiple simultaneous summons, and drops |
| Items and economy | Generation/affixes/uniques, equipment requirements, vendor/services, inventory/belt/cursor/stash, consumables, quest rewards, death drops, and rollback on rejected actions |
| Base world and quests | Towners/dialogue, quest selection/state, books/levers/traps, environment mutations, set-level progression, final encounter, and victory/progression consequences |
| Hellfire world and quests | Hive/Crypt progression, unique quests/rewards/objects, Na-Krul room/collision/tile behavior, variant items/spells, and final encounters |
| Persistence and multiplayer | Every new state survives save/restart; cross-player commands, join/leave/reconnect, duplicate retries, and concurrent world interactions preserve one authority |

Prioritize defects blocking M1/M2 routes, then complete base-game routes through
Diablo and Hellfire routes through their final encounters. Reuse shared systems
across both variants rather than finishing all base edge cases before allowing
Hellfire work. Cover each supported class and difficulty with appropriate
deterministic scenarios; test combinations that change behavior rather than
brute-force every combination.

Current replay limitations need explicit closure: compact single-target Golem
coverage does not prove multiple-owner summons or replacement allocation;
Guardian's descriptive markers are not full native/managed canonical hash
equivalence. Add exact comparable-state assertions/native characterization and
multi-actor scenarios as needed. A fixture that omits a resistance, block,
geometry, or second participant branch does not certify that branch.

Quest selection also needs native-order characterization: the managed catalog
currently sorts entries within each pool by quest ID, while native loading
retains TSV order. Correct this with explicit seeded parity and save/ruleset
compatibility evidence, not by changing the optional preview's base identity.

Exit: supported base and Hellfire campaign routes complete in Godot, and the
coverage ledger has no unsupported required gameplay/world behavior. Each
ported behavior has native characterization, managed tests, and relevant live
integration evidence; changes retain existing save, content, and wire behavior.

## M4: Release-quality presentation and operation

1. Complete the asset inventory, animation, audio, menus, input/controller
   behavior, accessibility, resolution handling, localization, and feedback.
   Supply a complete usable MPQ-free presentation route. Test optional original
   assets separately, including absent/corrupt assets and clear import errors.
2. Package the server, external packs, and Godot client reproducibly for the
   declared platforms. Reuse the adapter staging/archive verification tools.
   Test clean installation, creation, upgrade, resume, and rollback; packaged
   clients must not depend on a source tree or a native client installation.
3. Run measured campaign/multiplayer/reconnect/restart and ruleset-replacement
   soaks. Include latency/disconnects, rejected commands, malformed content,
   corrupt saves, failed replacement, and process termination during writes.
   Verify no duplicate effects, lost committed state, or unbounded queues.
4. Profile actual gameplay at the M0 budgets: server tick cost, snapshot size,
   client frame time, and memory. Optimize measured bottlenecks. Add bounded
   presentation prediction only when measured responsiveness requires it;
   reconciliation must never change server outcomes.
5. Exercise the generic Engine/Host boundary with the existing non-Diablo test
   adapter or a minimal second test adapter if evidence is missing. A second
   production game or general plugin marketplace is not a completion dependency.

Exit: clean-install acceptance, supported compatibility matrix, complete
MPQ-free play, soaks, and measured performance pass with documented artifacts.
Record security/operational failures within the existing host boundaries rather
than expanding the migration into unrelated infrastructure work.

## M5: Remove production legacy dependencies

Use the [Lua inventory](lua-capability-inventory.md) and
[entity-pool strategy](ENTITY_POOL_MODERNIZATION.md) as capability/compatibility
gates under this milestone. Replacement presentation and diagnostics can land
earlier. Runtime disablement alone is not physical dependency removal.

1. Characterize and replace each remaining supported script, callback, REPL,
   diagnostic, and presentation capability. Keep authoritative gameplay in C#;
   offline native debug handlers are not server admin APIs.
2. Verify the Godot/server distribution runs without native binaries, SDL,
   embedded Lua, or MPQs. Remove obsolete production build/package dependencies
   and redundant compatibility routing only after their consumers migrate.
3. Retain the native oracle and supported compatibility build in an isolated
   test/reference configuration. Preserve supported legacy saves, protocols,
   commands, and script behavior while that compatibility surface remains.
   Once those capabilities have replacements, remove embedded Lua, its bindings,
   and bundled scripts from the active source/build. Historical Git revisions
   can retain a reference oracle. If continued script compatibility still
   requires Lua, keep physical removal open and record the exact compatibility
   decision needed; do not redefine removal as runtime disablement.
4. Run final release and compatibility acceptance, update operation/handoff
   docs, and close every remaining completion-contract gap with evidence.

Exit: the production implementation meets the completion contract, has no
legacy runtime dependency, and active-project Lua removal is complete. Retained
native reference code has no hidden production fallback. A still-required Lua
compatibility build keeps the physical-removal gate open.

## Execution workflow

1. Select the smallest coherent deliverable that advances the earliest open
   gate or unblocks another work package. Write its expected user-visible or
   observable behavior, dependencies, native reference, and acceptance checks
   before editing. No new standalone planning file per spell or schema change.
2. Trace the change through content -> game module -> state/restore -> adapter
   and protocol -> Godot/native presentation. Touch only layers required by the
   behavior. Keep game rules in Diablo; extract generic primitives only for
   demonstrated reuse. Do not redesign established boundaries without evidence.
3. Implement, add risk-focused tests, and run the affected checks. Review the
   complete diff for authority leaks, identity/RNG/order changes, compatibility,
   failure rollback, and missing acceptance branches. Re-run only checks made
   stale by subsequent edits or an unresolved failure.
4. Update the relevant contract/reference, concise status/open gaps, and
   changelog in the same logical commit. Commit a reviewable behavior family or
   playable slice, including its tests and documentation; avoid separate tiny
   commits for every fixture, row, or documentation sentence.
5. Integrate at milestone/subsystem boundaries and run broader affected suites
   once against the integrated revision. Record failures as open gaps. Do not
   use a historical green result or a source-text assertion as runtime evidence.

The immediate queue is M0 launch/build freshness and application acceptance,
then M1's end-to-end authored loop and M2's Cathedral generation slice. Existing
parity defects blocking those deliverables take precedence within the relevant
batch. Do not restart completed engine extraction or package architecture work.

### Worker delegation

- Use GPT-5.6 Luna (`gpt-5.6-luna`) implementation workers and a separate
  GPT-6.1 Sol (`gpt-6.1-sol`) reviewer, both with `xhigh` reasoning effort.
  This supersedes the earlier high-effort and Terra-reviewer instructions.
  Do not silently substitute another model
  or effort setting; report unavailability and obtain direction for a change.
- The primary agent selects milestone-aligned work packages, supplies scope,
  file/contract ownership, native references, and acceptance criteria, and owns
  integration, final verification, documentation, and commits.
- Delegate substantial implementation packages that can proceed independently
  alongside useful work. Allow up to five independent workers, including
  reviewers, subject to the runtime's actual concurrency limit. Assign disjoint
  files/contracts; serialize shared
  simulation, protocol, fixture-hash, and status edits.
- Have a separate GPT-6.1 Sol reviewer at extra-high effort critically review each
  substantial package. The reviewer inspects the actual diff, native behavior,
  test evidence, compatibility, and plan acceptance criteria, and identifies
  missing branches or regressions instead of relying on the implementer's
  summary. Reviews do not silently expand or rewrite the implementation scope.
- Implementation workers report changed files, checks performed and results,
  and unresolved gaps. The primary agent resolves review findings, verifies
  the integrated result, and includes code, tests, and relevant documentation
  in a cohesive commit. Review is not a substitute for execution evidence.
- Handle small or tightly coupled tasks locally when delegation would mainly
  duplicate work or add coordination overhead. Missing worker availability does
  not prevent independent local progress, but local self-review does not count
  as the required separate-worker review for a delegated package.

## Verification policy

| Change | Minimum relevant evidence |
| --- | --- |
| Domain/data/engine/module behavior | Focused existing xUnit v3 project tests; native characterization/replay for changed compatibility behavior |
| Protocol, identity, command, or persistence | Affected server and protocol-client tests; live TCP/restart/retry tests where behavior crosses processes |
| Native compatibility | Focused GTest/CTest targets registered in `CMake/Tests.cmake`; build the affected adapter/client when changed |
| Godot scene, controls, or launcher | Godot build plus application smoke through actual controls; visible check for rendering/window behavior; model tests alone are insufficient |
| Content/geometry | Validation/rejection and deterministic generation tests; compare native state and traverse affected world route |
| Documentation only | Check paths, anchors, claims, CRLF, diff whitespace, milestone dependencies, and cross-document consistency; no gameplay rebuild |

Build the relevant binaries before using `--no-build`. Use
[MSVC setup](ai/msvc-build.md) before native configuration/build/testing and
verify `cl.exe`, `rc.exe`, and `mt.exe` resolve. Use the
[managed/Godot guide](ai/godot-build.md) for focused runner syntax. Protocol
tests, headless engine startup, visible scene interaction, and full campaign
acceptance are separate evidence levels and must be reported accurately.

## Reporting and document ownership

- `completion-plan.md`: active order, dependencies, acceptance gates, workflow.
- `migration-status.md`: dated estimate, current gate position, open gaps,
  coverage/evidence records, exact verification revision and exclusions.
- Architecture and protocol documents: stable ownership and compatibility rules.
- Server/content/client/Lua/pool/replay guides: specialized contracts and usage;
  link here for priority instead of maintaining another milestone sequence.
- `CHANGELOG.md`: chronological completed changes; old entries are historical.
- `history/`: superseded plans; never execute their unchecked tasks as a backlog.

Advance a milestone only on its exit evidence. Update the overall estimate at
those gates or when scope/evidence changes materially, not after each commit.
The present estimate has no calibrated effort denominator; do not average
subsystem percentages or publish an invented completion date. After M1 and the
first M2 family, use observed batch throughput and the remaining coverage ledger
to form an effort range. Keep missing data, unknown parity, implementation,
presentation, and verification gaps distinguishable in that ledger.

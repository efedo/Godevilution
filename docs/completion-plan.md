# Godevilution Completion Plan

Plan updated: 2026-10-18. See [working status](migration-status.md) for the
current baseline and [test history](test-history.md) for accepted verification scope.

This is the full migration roadmap; the [near-term work plan](work-plan.md) owns
the current short execution order. The [architecture](csharp-authoritative-server-migration.md)
defines ownership rules; the [numbered inventory](feature-gap-inventory.md) owns
feature completion and gaps. Working status owns current memory and constraints.
Historical plans are not active backlogs. Milestones below are acceptance gates, not claims of completion
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
this plan, with accepted verification summarized in `test-history.md`. The primary
agent owns planning, integration, acceptance, documentation, and cohesive
commits. Use a fixed pool of two GPT-6 Luna implementation workers at extra-high
reasoning effort and one independent GPT-6.1 Sol reviewer at medium effort.
Reuse these same workers for successive bounded packages until completion,
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
- Full native graphical parity in Godot for supported Diablo/Hellfire scenes
  and UI, including complete assets, animation, layering/occlusion, palettes,
  transparency, lighting/vision and automap, demonstrated by rendered reference
  comparisons. Functional controls and placeholder art do not satisfy this gate.
- Preserved supported protocol, content, and save compatibility; reliable
  multiplayer/restart recovery and validated deployment artifacts.
- A production dependency graph free of C++/SDL, embedded Lua, and mandatory
  MPQs, with the native compatibility build retained in isolation while it is
  still required by compatibility or parity tests.
- Eventual physical Lua removal from the active project once all supported
  capabilities have replacements. Disabling it or retaining it in an isolated
  compatibility build does not close this separate removal objective.

## Recovery plan: 2026-10-21

The user adopted a playable-first recovery plan. The [near-term work plan](work-plan.md)
owns its phases: stabilize CI and the tree, make the native world the default launch
path, port native gameplay modules faithfully, then deliver a rendered playable
client before Hellfire and release hardening. Exact parity is required where players
or saves can observe it and is obtained by close translation of the native source.
Diablo single-player precedes Hellfire; user-supplied original assets are the primary
visual path. The M0-M5 exits below are unchanged; where the 2026-10-18 reset below
differs on batch selection, the recovery plan takes precedence.

## Delivery reset: 2026-10-18

The user requested a step back and selected a **bounded inventory of remaining
gameplay gaps** as the first deliverable. The [inventory](feature-gap-inventory.md)
separates implemented capability, incomplete native parity, missing integration
and unproved application behavior. It is now prepared; the
[near-term work plan](work-plan.md) owns the proposed next batches. The full M0-M5
requirements and dependency graph remain unchanged.

1. Maintain the existing numbered inventory against changed source and evidence;
   do not repeat its construction or run a blanket test campaign to rebuild context.
2. Use the near-term work plan to select a substantial batch with the user. Keep
   its scope tied to inventory IDs and reuse existing systems and WIP. Update the
   plan, working status and affected inventory entries together as work changes.
3. For the chosen batch, integrate the complete behavior, resolve independent
   review findings, build the affected projects, run necessary focused checks and
   one relevant gameplay journey, then commit selectively. Repeat passing evidence
   only when changes invalidate it. Rendering claims still require rendered proof.

The passing native Town/L1/L2 route is a real baseline. The experimental L3 and
four-floor bot scenarios remain failed acceptance evidence, not proof of an
unspecified transport/combat defect and not prerequisites for every unrelated
capability. Require a concrete counterexample before changing native rules or
shared timing. Scenario changes may use ordinary native progression and actions;
do not grant resources, weaken server admission or relabel a narrower result as
campaign completion. Full four-floor, class/difficulty, quest and campaign gates
remain open until their actual outcomes are demonstrated.

Ordinary source work, including Main.cs/Main.Session.cs, is authorized. The
user rejected the agent-written Main hold. Do not create per-patch permission
requirements or treat historical cautions as new user restrictions. Preserve the
separate, narrow user-directed holds listed in the current session handoff.

## Baseline and strategy

The dependency boundaries, host, shared protocol, persistence, content loaders,
and many gameplay slices already exist. Reuse them. Godot has substantial
protocol coverage but placeholder presentation. Native dungeon synthesis and
full campaign integration remain major gaps. See the dated
[working status](migration-status.md) and numbered inventory for the current
position; do not infer completion from commits, fixture counts, schema versions
or uncalibrated percentages.

Use playable milestones to select work. Repair a gameplay family and its
dependent content, restore, protocol, and presentation paths in one coherent
batch. Add parity cases for that batch's risks. Avoid an open-ended sequence of
isolated spell or table rows while the playable route stays unchanged.

| Gate | Deliverable | Dependencies | Current position |
| --- | --- | --- | --- |
| M0 | Reproducible launch and honest acceptance evidence | Existing implementation | Dedicated-root cohort host passes TCP recovery, scoped faults, production CLI participant/item recovery and lease refusal, isolated Godot create/resume/outage and surviving-client committed-but-unobserved replay; managed first-publication interruption and a scoped Windows metadata profile pass; approval-gated normal-launch adoption and broader fault/filesystem/platform acceptance remain; legacy cold imports, native GUI and release-budget gaps remain |
| M1 | Usable Godot town-to-dungeon loop | M0 launch/acceptance harness | Authored combat/loot/equipment, NPC/service approach, purchased-scroll portal and isolated Sorcerer quest/cast/Recharge/death/restart/restore journeys pass; two-client authored combat/loot and complete-cohort cold resume pass headlessly and visibly with inspected milestones; full loop remains |
| M2 | Native-compatible generated world | M0 geometry/parity baseline | Ordinary Cathedral L1-L4 geometry/population comparisons, explicit populated profiles and versioned floor owners exist. Native Rogue Town/L1/L2 gameplay and fresh-host retention pass. Complete L1-L4 application play, quest/campaign lifecycle, exact vision, native art and remaining dungeon families are open; a pre-join generator alone is not a ready-to-tick campaign. |
| M3 | Complete base and Hellfire gameplay routes | M1 plus the relevant M2 world slices | Many rules exist; campaign and remaining behavioral coverage remain |
| M4 | Release-quality client, recovery, and packaging | M3; preparation begins during M1/M2 | Recovery and adapter packaging foundations exist; product acceptance remains |
| M5 | Production legacy-runtime retirement | M4 and capability replacement gates | Authoritative mode is Lua-free; compatibility runtime remains |

M1 and M2 can progress together after their M0 prerequisites. M3 work can start
as individual M2 level families become available; it need not wait for every
map generator. Asset presentation starts in M1 and improves through M4. Recovery
and multiplayer checks accompany every slice rather than being postponed until
M4. M5 is the final removal gate, not a reason to postpone replacement work.

## M0: Reproducible launch and acceptance baseline

The explicit single-client PerformanceCreate/Resume route keeps the established
workload, validates the first admitted wire before input and requires exact
next-sequence movement. Its accepted exported headless scope and graceful host
exit are in [test history](test-history.md). Default-profile adoption, broader
world/campaign restore, rendered acceptance and release budgets remain open.

The client retains credential-free ServerHello fields and a defensive clone of
the first decoded wire snapshot for the latest connection attempt. Admission is
recorded separately; consumers must bind the current attempt and connection state.
Focused transport/reconnect checks and the exported single-client cold-resume
journey verify this observation API. Broader M0 restore acceptance remains open.

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
   Bound owned-process termination and retain terminal native exits before
   fallible log storage; preserve the engine failure as primary. Child-only
   crash-dialog suppression is mitigation, not proof of every historical cause.
   Base recovery must retain matching stopped-host identity, terminal native exit
   and observation-time/port evidence before disposal or host replacement.
   Unattended visible diagnostics outside an acceptance phase must opt into
   `-Automated` with a separate explicit session directory; retain the scene
   quit timer and parent deadline. Normal launches remain interactive. Reject
   otherwise successful runs with cleanup/evidence failures and do not retry
   until owned processes are confirmed stopped. Direct-handle cleanup is not
   OS process-tree containment or a persistent retry lock.
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
   profiles and the native pair select it; the standalone CLI and default low-level
   native configuration retain Immediate compatibility. The native synchronous
   adaptation and presentation source review are approved and affected native
   regressions pass, including the final loopback-only fixture rerun. The build
   now enables existing authoritative guards in all independently compiled
   gameplay object targets. Actual GUI acceptance remains: a prior isolated
   run was stopped behind a Windows Security prompt requiring manual cancellation.
   The later Windows automation retry could not initialize its helper after the
   documented reset/retry, so no new native client was launched or GUI pass claimed.
   Preserve the legacy
   direct API and strict late policy. Final-source host regressions and actual
   headless/rendered two-client creation/mutual movement/reverse-order fresh-host
   resume now pass, alongside single-player creation/resume/server-loss recovery
   and the corrected portal TownLoop/resume. Keep delayed ACK, timing probe,
   failure and epoch-composition regressions; these scene/rendering checks are
   not campaign acceptance. Initial unmeasured
   latency and later spikes are not bounded by historical probes. A first-command
   `TooLate` counterexample is retained from the generated-geometry playtest.
   Godot now requires a correlated, unexpired current-epoch sample before the
   first realtime send, even with unsent input queued. Validate bounded startup
   refusal/reconnect and preserve explicit ticks/frozen retries. A passing local
   rerun does not bound later spikes or remove large-projection costs; address
   those without relaxing late admission. The C# gated admission estimate now
   retains its elapsed-time anchor when a delayed snapshot falls behind or equals
   the projection, including newer raw ticks. Preserve ceiling-rounding and
   reconnect-rate/reset regressions, Immediate compatibility and frozen retries.
   The native gated counterpart now preserves its elapsed-time anchor too,
   retaining native whole-millisecond/floor projection and saturating arithmetic.
   Preserve deterministic delayed/equal/ahead, epoch/reset and overflow checks,
   alongside individually initialized registered native presentation fixtures.
   Focused and affected native regressions do not establish GUI gameplay acceptance.
   This reproduced estimator defect does not classify historical `TooLate`
   failures without requested-tick and host arrival/admission evidence.
   Use the opt-in bounded client/host command-timing capture on fresh isolated
   acceptance sessions to compare frozen requested ticks with exact admission
   ticks and software read/write boundaries. Keep normal launchers uninstrumented,
   retain failed runs, and verify drop/fault accounting and operation terminals.
   Tracing overhead is not a release-latency measurement. A passing repeat is
   neither a causal explanation nor a timing fix; change behavior only for a
   demonstrated counterexample, preserving strict admission and frozen retries.
   A controlled TCP counterexample now reproduces same-connection ingress waiting
   behind the first future command's ACK. The bounded clock-gated pipeline admits
   later envelopes independently, preserves whole-envelope FIFO and ordered
   replies, and fences authority failures without canceling admitted work on peer
   loss. Keep causal held-clock, capacity, half-close, fault and shutdown checks,
   plus actual two-input Create/save/resume acceptance. This fixes a demonstrated
   ingress stall, not the unclassified historical first-resume `TooLate` cause;
   Direct write-gated TCP fixtures now verify stalled-writer response-budget
   retention and canceled queued-snapshot provider avoidance. Preserve their
   actual reset/read exception identity, joined cleanup and repeated causal
   checks; writer cancellation must not replace the initiating read error.
   Live protocol fixtures now launch the evaluated matching-configuration host
   DLL directly, drain output independently of caller cancellation, and confirm
   actual host exit before registered fixture-root cleanup. Preserve startup
   refusal, borrowed-root, faulted-reader, teardown-failure and Release/artifact
   checks; do not infer the historical receipt-file lock owner from passing tests.
   Pending-command fences do not solve the adapter's broader shared-world
   cold-restore overwrite. A real multiplayer loopback regression now reproduces
   claimed-item resurrection in both cold-resume orders from actual same-tick
   durable saves. Keep this known-gap fixture explicit until recovery publishes
   one atomic, complete world/RNG/participant/receipt cohort and restores it
   before admission or advancement. Include stable owner identities and inactive
   modified stores; do not choose a participant by newest tick or join order.
   Preserve the existing complete-world ImportSessionState migration contract.
   Generic runtime/command capture and fresh one-shot initialization, detached
   backing geometry/all-store restore, and a separately leased canonical cohort
   store are now implemented prerequisites. Complete module graph capture,
   exact allocator/portal clocks and same-tick markers, fresh one-shot participant
   restoration, owner-graph validation and a strict host-owned Diablo codec are
   implemented. The dedicated-root host mode restores all module
   state, bindings, ledgers and exact cursors before listening, publishes under
   the command barrier before exposure, and fences live mutation/publication
   failure. Both resume orders now pass actual Immediate/gated TCP recovery;
   corruption/identity refusal, durable-write failures and rejected-admission
   cursor gaps have focused coverage. Actual partial-executor/snapshot failure
   and successful/rejected/post-publication-failure reload checks now pass.
   Isolated Godot-driven CLI participant create/resume/outage and headless/rendered
   two-client reverse-order resume now pass with stable bindings. Preserve the
   retained interrupted Windows replacement failure: canonical publication now
   uses one handle-based POSIX rename with shared/delete reader support, not
   multi-step ReplaceFile. Owned managed child termination now verifies seed and
   first-cohort write/flush/rename/marker boundaries, canonical-only restart and
   committed-history refusal. An explicit initializing/committed layout preserves
   strict regular stage files without reading, promoting or deleting them. Windows
   staging verifies supported security before payload and preserves the documented
   owner/group/DACL, creation/basic attributes, named-stream and compression profile;
   unsupported access policy and metadata fail closed. See the
   [supported profile](ai/godot-build.md). Keep audit-SACL/full-metadata equivalence,
   power-loss recovery and the wider filesystem/platform matrix outside these claims.
   Normal-launch adoption requires approval for separate dedicated profiles;
   existing legacy/conflicting saves remain untouched. Broaden standalone operator CLI
   and fault scenarios; do not substitute these scoped results for
   broader multiplayer or application acceptance. Existing conflicting legacy multiplayer saves
   must remain untouched pending an approved migration policy.
   Rebuilt CLI empty-roster startup/quit/resume and live second-process lease
   refusal now pass. Missing-seed and legacy-root refusals preserve their
   canonical/legacy inputs without seed regeneration; these are not gameplay tests.
   Production CLI TCP acceptance now additionally creates two real participants,
   drops/claims a shipped starter item across abrupt restart and opposite-order
   cold resume, and checks duplicate suppression and both owners' next exact
   accepted receipts after a second restart. Terminal integrity/strict module
   decoding, live lease refusal and malformed-terminal preservation pass; this
   realtime check does not independently certify exact RNG continuation.
   Actual headless/rendered cohort multiplayer host loss now retains exactly two
   clients, freezes offline snapshot/input/rendering, restores stable bindings and
   requires mutual settled movement plus exact canonical accepted owner receipts.
   Unread production CLI replies and deliberately withheld actual-Godot replies
   now cover already committed but unobserved outcomes. Require canonical effect
   and original Accepted receipt before host termination, exact parsed-command
   identity on retry, original successful Duplicate receipt, stable owner bindings
   and the next genuinely new sequence. Godot's friendly-mode control is an
   idempotent assignment; non-idempotent item drop and production C# relative
   movement supply independent effect coverage. Future scheduler intents remain
   intentionally nondurable and outside this committed-outcome contract.
   Keep broader faults/campaign, retained unclassified `TooLate` failures and
   release acceptance distinct. Retained full-suite lease/initialization fixture
   failures remain unclassified despite later green checks. Reviewed
   test-only remediation now uses dedicated controlled gates, bounded joins and
   stage-labelled read deadlines; focused/default-parallel qualification is in
   status. Independent review closed a disposal-time primary-error masking gap;
   neither these checks nor a green rerun establish the historical failure cause.
2. Add an application-level acceptance path through the actual Godot scene and
   controls: launch, create a character, receive/render an authoritative world,
   submit movement, observe the acknowledged position, save, exit, and resume
   through a fresh host. Use isolated test saves and deterministic content.
   Inspect legacy checkpoints after verified owned-host exit, not while their
   Windows File.Move publisher is live. Preserve abrupt-stop durable-state and
   exact Accepted-receipt checks, the causal held-reader limitation, strict
   decoding/cancellation and owned-root cleanup evidence alongside scene journeys.
3. Cover native launch-to-new-game with the same explicit single-player mode,
   current binaries, and available local assets. Retain the existing launcher
   contract and native mode-policy tests; they are useful but do not prove GUI
   creation. Missing proprietary assets produce an explicit unverified native
   acceptance result, not an assumed pass or a blocker for Godot work.
   Actual GUI startup now exposed and corrected a native/managed reward-offset
   limit mismatch. Preserve visible connection-error recovery and the boundary
   regressions. Deferred projection and graphics/frame admission retain strict
   missing-art rejection; current diagnostics identify the failing object/template.
   Preserve the earlier dead-origin Cathedral and missing-art failures. The new
   optional strict `level_start_positions.tsv` contract and exact-pinned
   `native-start` pack now select town `(75,68)` only for the native pair, without
   rewriting base/Godot content or widening the existing level/overlay schemas.
   Validate final effective geometry atomically and refresh projected metadata;
   saved-player positions remain authoritative. Pre-adaptation GUI creation and guarded
   fresh-host resume now show town/HUD and full-life server state, with normal
   exits, but no accepted native movement. Recorded clicks are rejected `TooLate`;
   the rebuilt native adaptation corrects first-dispatch timing, imports the
   resume sequence floor and restores signed player-display markers. Verify these
   paths without relaxing authority, then demonstrate accepted
   movement and moved-position restore under the selected host mode.
   Player presentation is not yet established by the town captures. Use the
   marked version-2, exclusively locked profile and retain per-phase logs/exits;
   inspect legacy checkpoints only after confirmed owned-publisher exit or a
   verified publication barrier; delete sharing alone is insufficient for the
   Windows File.Move path. Cohort POSIX-rename reader support is a separate
   qualified contract. Helper tests and world rendering do not close this gate. Never automate
   security prompts or enable network permissions for loopback tests.
4. For an affected integration gate, summarize the verified revision, scope and
   material exclusions in test history. Keep repeatable commands in the relevant
   build guide. Establish the target platform/participant matrix and measured
   tick/frame/memory budgets in working status before claiming performance gates.
   The opt-in Release standalone host workload measures Immediate and ClockGated
   with one/four participants over loopback. Preserve settled observer outcomes,
   complete metric applicability and clean owned-host exits. Bind ClockGated
   first-dispatch ticks to actual positive receipts and refuse assembly overrides;
   synthetic retry checks complement the no-retransmission live workload. This
   scoped baseline is not a Godot-frame or release-budget pass.
   The separate opt-in actual Godot workload uses fresh one/four-client profiles,
   real controls, all-observer staging/round barriers, bounded callback evidence
   and clean engine exits. Keep callback-body cost separate from start intervals
   and GPU/whole-frame timing; last-GC heap and lifetime peak working set are not
   live allocation or soak evidence. Preserve failed captures and reject invalid
   metrics. Ordinary launches and two-client create/resume remain unchanged.
   Local Debug town measurements do not establish a release budget or full
   platform/campaign matrix. Scoped exported recovery and one/four-client
   Performance results, including graceful Release-host exits, are summarized
   in [test history](test-history.md). Preserve their stated profile and workload
   boundaries; they do not establish full campaign/world restore, rendered/GPU
   performance, native machine-code optimization, normal cohort adoption or
   release/platform/soak qualification. Old run dossiers belong in Git history.

   Godot now reuses installed geometry between model-accepted snapshot generations.
   Preserve full replacement, same-tick/epoch admission and eager legacy behavior;
   do not treat ownership as public protobuf immutability. Measure remaining
   per-snapshot validation/cloning, full-world drawing and large-map costs before
   selecting broader caches or presentation changes. Callback comparisons remain
   local observations, not whole-frame or release-budget passes.
   Retain unclassified live store movement rejections alongside later isolated
   or complete passing runs. Exact queued/receipt/model/action diagnostics
   now accompany travel assertions; observed state is not server admission state.
   Do not infer a cause or timing correction from a later pass. Use working
   status for current limits, test history for accepted scope, and keep broader
   M0 gates open.
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
   The reviewed session batch now passes focused checks and actual headless
   create/return/rejoin/fresh-host resume. It retains pending-command/bookmark
   refusal, private credentials, generation fences and joined cancellation/exit
   cleanup. HUD callbacks follow the current model; local setup matches profile
   class admission. Rendered usability and broader campaign/multiplayer flow
   remain open; see test history for scope.
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
   A separate Sorcerer lifecycle profile now verifies itemless quest activation,
   charged staff/class Recharge, mana-consuming Firebolt and observed
   monster-caused player death,
   Town restart and exact off-level dropped-staff restore through actual controls.
   A separate actual TCP prerequisite now preserves the complete still-equipped
   39/39 recharged starting staff and four resources through a stopped checkpoint,
   fresh-host pre-input resume and the next accepted movement. Its clean/working
   checks do not replace rendered controls or full inventory/world acceptance.
   Normal resources and original profile identities remain unchanged.
   Preserve original and new profile identities/saves. Declared local-level NPC
   and portal destinations now support cancellable, acknowledged approach through
   World controls. Native-role vendor/stash entry requires the declared NPC level,
   adjacency and empty cursor; metadata-free/custom-store compatibility remains.
   Owned portals take priority over coincident automatic stairs. Metadata-bound
   native vendors now close after accepted cross-level entry into a non-town level;
   stash and compatibility-store policies remain unchanged. Optional live store
   service hints now gate Godot Repair, Recharge, Identify and mana-refill buttons
   by existing vendor permissions. Legacy peers and no-store class/scroll choices
   retain their behavior. Live service quotes now show server-owned costs and
   rule refusals for the current selected item or mana refill. Missing quote
   containers preserve legacy presentation; transactions and saves remain
   unchanged. Rendered shop interaction remains an acceptance gap.
   Close remaining
   native placement exclusions, full service lifecycle/pricing, broader quest,
   death/class coverage and broader cooperative gameplay/recovery journeys. Two
   actual Godot clients now extend town/session checks with a separate explicit
   two-client authored combat/loot and complete-cohort cold-resume profile.
   Preserve real controls, ordinary resources/AI, stable local-first identities,
   separate bookmarks, post-receipt settled outcomes and exact canonical owner
   receipts. Headless/rendered acceptance and paired milestone inspection now
   pass. Continue inspecting actual frames: cached resource bindings alone are not
   per-frame draw proof. These authored previews do not close M1.
   The explicit authored generated-depth route now joins ordinary Smith scroll
   purchase, L1 object/combat/earned-sword equipment, L2 save and fresh-host/client
   resume at the exact next accepted command. Retained L1 mutations, consumption
   of the restored scroll, owned portal Town return, exact sword stash round trip
   and Smith refusal pass through actual headless controls. Wallet/stash Gold and
   earned item state remain exact; claimed loot does not respawn. This closes that
   authored route's save/resume and portal/stash integration. Native population,
   rendered controls, normal profile adoption and the broader M1 loop remain open.
3. Give geometry, actors, projectiles, items, and interaction targets readable
   Godot presentation. Split the main scene/script only where this separates
   concrete input, rendering, menus, or HUD responsibilities. Keep placeholder
   art usable while the client asset-pack boundary is established.
4. The client-only presentation boundary now has stable IDs, atomic manifest
   validation, missing/undersized texture fallbacks, atlas metadata and visual
   frame clocks, optional audio/localization lookup, and a default manifest
   reusing the existing six SVG stand-ins. Focused pure tests and actual base
   create/resume/outage journeys cover default/animated/missing/undersized/refused
   packs, offline frame freeze and admitted reconnect-epoch reset. Keep the
   [manifest contract](ai/godot-build.md#client-presentation-packs) outside server
   content, protocol identities, gameplay/RNG and saves. Player class/action/
   combat/facing rules and static HUD localization now extend that boundary;
   retain atomic ambiguity/count refusal, exact per-entity draw evidence and
   missing-selected-art fallback. The clock remains global and visual-only.
   The sprite-free `NativeAnimationState` prerequisite now has an independent
   native proof for all nine cursor scalars; it is not yet wired into players,
   protocol, saves or Godot. A separate prepared-graphics context catalog now
   provides explicit class exceptions, per-level town identity and native spell
   school bindings; town application retains incoming combat frame fields.
   This is metadata qualification, not equipment eligibility, sprite availability
   or runtime activation. The explicit Engine `ExecuteTick` prerequisite now
   integrates ordered actor command slots into ordinary/cohort Diablo lifecycles,
   with chronological events and fail-stop faults, without default host adoption.
   Ordinary attack ready/completion caller skips have independent native tests;
   this is not whole native mode processing or cursor integration.
   Seven prepared native Move/Cast fixtures verify admission and two direct
   animation steps, including town run, actual attack-completion walk and ready
   Spell carry. Managed cases verify actor order, Move completion before Cast
   and ready Cast/same-tick refusal. Two full ProcessPlayers tests now capture
   all nine post-update fields through prepared Search effect/completion and
   the first two walking updates. A matched 8/5 managed Cast trace observes
   actual post-ExecuteTick state; both prepared effect/completion boundaries
   are 105/107 after admission at 100. Classes/effects differ. A further full
   North-walk trace reaches native arrival/Stand at logical label 109, while
   the managed eight-frame Move changes position/clears its action at tick 108
   after admission at 100. The ten post-update rows establish a phase difference,
   not an adopted parity mapping. Prepared Spell re-admission now has full-loop
   traces: first effect before replacement at 105, second effect at 110 and
   observed Stand at 112. Native repeated rows carry two frames with modifier
   256; the initial rows carry none with modifier 170. Full Attack/Ranged ready
   repetition, shipped/world-tick parity and cursor adoption remain open.
   An internal callback phase contract now distinguishes ordinary slots, attack
   ready repetition, Spell re-admission and completion-loop starts. It is visible
   only for the active actor reference under native timing, captured before the
   callback and cleared on success or fault. Existing timing stays unchanged.
   A full native unarmed attack-completion walk starts at frame 0 on label 100
   and arrives at 108, compared with idle admission at frame -1/arrival 109.
   Next settle the persisted command-phase/anchor contract:
   production authoritative commands currently follow actor advancement, but native Pending
   assumes processing later in the same tick. Neither publishing negative phase
   until the next tick nor blindly processing every accepted command again proves
   native post-tick parity. Keep duplicate/rejected and same-tick command tests.
   Before host adoption, qualify typed input/equipment preparation, native player
   slot order, early weapon-break completion and queued intent/receipt handling.
   Then implement module-owned class/gear/context transitions, effective graphics
   and exactly-once actor-order tick processing, including pending,
   repeated, hit/block/death and metadata-change behavior. Preserve existing
   gameplay-effect boundaries and RNG order. Carry the complete cursor through
   versioned persistence/protocol and project each actor using only a bounded
   render fraction; do not reconstruct authority from elapsed client time.
   Keep legacy absent-state bytes/hashes and default profiles unchanged until
   the new policy has compatibility, restore/reconnect and actual draw acceptance.
   Then integrate remaining actor/object/projectile
   art, audible feedback and complete usable localized names; this foundation
   does not close M1 or M4. Any original-asset importer remains optional
   client tooling with explicit input/output and a complete MPQ-free alternative.

Exit: a person can complete the loop through Godot controls without debug
commands; application tests observe authoritative outcomes and visible-state
transitions. Two Godot clients can join one multiplayer host and observe each
other's actions. Automated model/transport tests supplement, but do not replace,
this evidence. A sample art pack does not satisfy M4's full presentation gate.

## M2: Native-compatible world generation and placement

The native offline join characterization now sends the real `CMD_PLAYER_JOINLEVEL`
packet through `ParseCmd`: it verifies first-free placement, player occupancy,
light/vision activation and unchanged RNG, plus invalid-level/out-of-bounds refusal
before presentation mutation. The two new cases, full eight-case player set and
three shuffled iterations pass. This scopes the legacy join baseline; generated
world integration and campaign join/restore acceptance remain open.

1. Inventory the native generators, seed streams, level/quest selection,
   geometry layers, stairs/warps, set pieces, objects, and encounter placement.
   Keep family gaps and completion status in the numbered feature inventory;
   record source references and technical contracts in the existing world guides.
2. Complete Cathedral as the first generated family integrated with M1, then
   Catacombs, Caves, Hell, Hive, Crypt, and dependent quest/set levels. Treat
   town and transition topology as part of the same world contract. Adjust
   independent family order only when it reduces a documented dependency.
   The ordinary original-Diablo level-1 coarse port now matches two native DUN
   goldens, continuation RNG and successful-attempt restoration but has no
   automatic campaign seed/depth mapping. Its fine adapter expands validated
   TIL/SOL catalogs
   into 112x112 fine coordinates while retaining the separate coarse result.
   Full fine-layer comparison using locally supplied original assets now passes
   seeds 2588/743271966/0/1/0x80000000/0xFFFFFFFF with MAIN/PREV entry. The optional
   oracle retains raw data
   privately and normalizes only the unnamed SOL bit for test-only named-flag
   comparison; production loaders are unchanged. Explicit adapter
   `devilution.diablo.cathedral-l1.v1` now eagerly generates exactly ordinary level 1
   from compatible validated packs, resolves native Town/stair anchors, and fences
   saved geometry/catalog/route identity before restore. Synthetic SP/MP stair-boundary
   checks and a separately explicit local-asset semantic-catalog activation/return/cold
   restore pass. Those earlier checks remain stair-boundary evidence only.
   An offline explicit-semantic-TSV compiler now provides a separately hashed,
   isolated playtest pack and zero-argument Godot launcher. Actual headless/rendered controls
   now walk from Town through generated level 1, activate an authored object,
   defeat/pick up an authored encounter, continuously traverse fine cells, reject
   an exact wall input, restore through a fresh host/client and return via native
   Up stairs. See test history for scoped outcomes and working status for limits. This proves
   the diagnostic integration, not native population or the family exit gate.
   Preserve it while extending native Cathedral generation/depth and population;
   multiplayer and broader seed/topology checks remain relevant gates.
   Ordinary Cathedral depths 1-4 now share the native-compatible generator and
   materializer: `level_l1.cpp` minimum areas are 533/693/761/761. Private oracle
   capture explicitly disables quests and selects original-Diablo stairs; all
   six seeds with MAIN/PREV match attempt/final RNG, complete coarse/fine layers
   and entry/stairs. Existing L1 APIs/results and its separate oracle remain.
   Multi-depth activation now uses a separate opt-in catalog/adapter/profile and
   strict version-3 save identity, preserving existing L1 v1 identities/version-2
   saves and authored defaults. Profile IDs 1-4 replace the base prototype's other
   families only inside the isolated pack; dependent references are validated before
   publication. Eight reciprocal routes bind Down to next.Main and Up to
   previous.Previous; only L1.Up returns Town. L4.Down has no route until native
   Catacombs-5 is supported. See test history for ordinary-control descent/ascent,
   collision, middle-floor fresh-host restore and unchanged default/L1 evidence.
   Next, close native object/door and population placement before widening playable
   topology coverage: some exact generated layouts currently have disconnected SOL
   routes and the diagnostic compiler rejects them rather than altering geometry.
   The separate `-CathedralDoors` native scanner oracle now characterizes L/R
   placement, closed/open/reclosed piece and collision mutations, delta opening
   and observed non-restoration. An isolated opt-in managed core implements
   discovery, repeatable operations and strict mutable-geometry restoration;
   seventeen private native cases compare its complete layers and transitions.
   A separately identified door-aware compiler/profile now validates activated
   occupancy and routes without repairing native SOL, replacing seeds or changing
   depth/L1/default packs. Its isolated Godot path draws schematic server-owned
   CLOSED/OPEN/BLOCKED cues and uses ordinary input. Headless create/save/fresh-host
   resume passes. A fresh rendered diagnostic repeat now passes creation and cold
   resume, with exact first-command dispatch/read/admission evidence. The earlier
   rendered first-resume TooLate remains retained and causally unclassified; this
   passing repeat is not its fix. Keep strict admission and the incomplete-capture
   limits in status. Obtain a traced counterexample or bounded fault characterization
   before changing timing policy, and qualify native loaded-art mapping and broader
   door traversal before widening playable topology. Existing
   profiles and launchers must not silently adopt this new capability.
   Portable synthetic fixtures now cover actual four-depth None-policy compiler
   publication/activation, deterministic hashes, overwrite refusal, light-only
   pool overflow and a corrupted-pack authored/scanner-door activation refusal.
   Preserve their diagnostic-only scope. Actual compiler-level tests now cover
   safe adjacent versus blocked authored item footprints and mixed door/light
   scans at exactly 127 slots, with refusal at 128. Independent production
   materialization checks all four candidate depths; refusal preserves base
   inputs and publishes no output/staging. These six portable synthetic facts
   close those compiler prerequisites, not native generated-layout, broader
   door traversal or the Cathedral-family exit gate.
   Captures are independent expected fixtures, not seed-specific production
   transition tables. Port native-compatible discovery/transition logic for
   arbitrary seeds using validated external catalogs, preserving mutable geometry
   and operation order through restore rather than rebuilding only from closed
   base layers and open/closed flags.
   An explicit DiscoverNativePoolPrefix scanner now reports the y-major/x-major
   allocation prefix and skipped suffix at the native 127-slot limit, preserving
   strict Discover and compiler overflow refusal. A complete native special layer
   is required; missing geometry is not initialized. Managed ordering/capacity checks
   and native AddL1Objs boundary tests pass; lighting initialization, slot identity,
   population RNG and profile adoption remain separate.
   Scanner RNG, including L1 lights, is not production `InitObjects`/population
   RNG. The separate `-CathedralPopulation` oracle targets the complete native
   `InitObjects` phase after real trigger/stair reservations, monster-type and
   theme initialization, an independent population seed reset and `InitGolems`.
   Its initial corpus omitted the level-generation table and therefore uses
   zero trap percentages; its piece layer is pre-object initialization. Preserve
   that legacy reference. The separate `-CathedralPopulationParity` route now
   compares sixteen cases using loaded trap metadata, explicit pre-phase inputs
   and final mutated layers against the independent managed initializer. Its
   version-2 sidecar now extends this gate through independent `GetLevelMTypes`,
   `InitThemes` and `HoldThemeRooms`, using actual pre-theme inputs and managed
   selected types/reservations rather than native expected selections. Preserve
   the earlier version-1 initial-object comparison and successful wall traps,
   exact pool/RNG checks and population-stream reseed. The separate
   `-CathedralEncounters` route extends the composed comparison through ordinary
   depths 1-4 `InitMonsters`: complete active/unused 200-slot state, target count,
   signed refs, RNG, trigger visibility/transparency and indexed light history.
   Fresh pool clearing uses its recorded pre-clear seed, not the population
   reseed or native expected inactive records. A strict external raw
   monster/object TSV catalog loader is available; a separate 100-row unique
   catalog derives quest suppression from source IDs. These are not a complete
   pack compiler. A separate `-CathedralInitialItems` route extends the managed
   phases through ordinary `InitItems`, preserving all 128 item slots and native
   item-seed reseeding with externally supplied metadata. Public scalar/string
   fields and five public animation scalars are compared, not private visual
   distribution or sprites; unsafe native signed coordinates fail closed.
   The four prior Hork RNG failures are addressed by the controller/save batch
   recorded in implementation history; this is bounded decision parity, not complete
   Hork animation, directional-corner or whole-`ProcessMonsters` parity.
   The separate zero-argument `tools/test-theme-item-generation.ps1` now compares
   detached `SetupAllItems`, `TryRandomUniqueItem` and public ground-animation
   scalars against raw native metadata for original-Diablo SP Normal depths 1-4.
   Its 3,040 cases cover affixes, books, gold, staff outcomes and unique history.
   The separate `tools/test-theme-item-placement.ps1` extends detached proof to
   raw selectors and complete `CreateRndItem`/`CreateTypeItem` pool, placement,
   setup/unique/ground and world RNG for the same bounded policy. Preserve all
   128 item slots, 127 IDs, signed grid and 128 history flags, including
   selector-before-capacity RNG, signed overwrites and unreferenced allocations
   after exhausted placement. Unsafe native out-of-bounds writes fail closed.
   A separate detached `CreateThemeRooms` port now uses these verified item
   dependencies for all fourteen ordinary Cathedral theme types, in source
   order. It retains full object/monster/item pools, signed occupancy, corpses,
   target state, persistent fit scratch/flags, RNG and zero-offset static light;
   unconditional `ItemNoFlippy` remains even after full-pool creation returns.
   The separate zero-argument `tools/test-theme-room-generation.ps1` compares
   88 complete selected-theme worlds and 40 static-light cases, independently
   capturing actual native pre-call inputs and comparison-only final state.
   This detached pass is not composed full-level population or a runtime pack.
   The separate zero-argument `tools/test-cathedral-full-population.ps1` now
   compares sixteen controlled depths 1-4/seeds/MAIN-PREV cases through this
   final pass, including real multi-room source order. Managed selections,
   held flags, pools, scratch XY/variant, RNG, initial-object physical lighting
   and encounter/item state feed the next phase; native expected intermediate
   or final state is comparison-only. Independent pool clearing consumes its
   recorded post-CreateLevel pre-clear stream, not the population reseed.
   This extends the controlled oracle, not the full application-order lifecycle:
   normal level loading clears monsters before geometry creation. Later
   `SavePreLighting`/`ProcessLightList`, retained pools, hero/store/quest setup
   and campaign seeds remain separate acceptance work.
   The complete nineteen-table population metadata schema and explicit offline
   compiler now externalize geometry, effective SOL, raw prerequisites, items,
   uniques and policy. Preserve strict publication/reload and native comparison
   gates; metadata compilation alone is not playable profile acceptance.
   An additive fresh-cold generator now derives pre-phase channels from the
   compiled metadata and explicit dungeon/story/hero inputs. It uses native
   clear-before-geometry order, the actual transparency counter, generated
   special/trigger/stair reservations and the player's pre-population light,
   public idle-animation scalars and pending vision. Preserve the independently
   supplied pre-entry future and changing flag: native synchronization/join
   happens after population. This is not retained-state campaign loading or a
   ready-to-tick level. A separate cold continuation now covers missile/corpse
   initialization, SavePreLighting, local-player occupancy, monster light/charge
   cleanup and physical light/vision through the pre-join boundary. Preserve its
   explicit post-InitPlayer context, complete corpse/allocator/vision tails,
   signed grids and independently validated 256-row automap metadata. The old
   nineteen-table pack and earlier native corpora remain unchanged. Join,
   retained-level/campaign lifecycle and runtime acceptance remain open.
   The separate runtime monster catalog and detached binding adapter now supply
   native combat/experience/selection/treasure metadata and explicit immutable
   native-slot/type/unique-to-stable provenance. Exact zero seeds and Q6 HP use an
   explicit policy, with cohort v3 preserving metadata even for an empty roster;
   legacy v1/v2 schemas/default bytes remain frozen. These are import/checkpoint
   prerequisites, not live populated acceptance or a lossless raw checkpoint.
   Detached native death-time loot now matches actual SpawnLoot across ordinary
   Cathedral depths 1-4, including effective selection versus raw setup level,
   current item pools, signed references and unique-history regeneration. It is
   not live gameplay: wire this compositor at actual death, never precompute
   drops during generation. Preserve its independent native proof and old item
   creation behavior. Live item integration and the specifically approved Gold/fixture
   correction are delivered within the scoped native route; the earlier draft
   refusal is superseded. Extend remaining item behavior through the existing
   simulation mutation boundary and explicit populated profiles. Current economy
   gaps belong in inventory item 4; retained narrow holds are in working status.
   Retain full pools/order/tails and signed
   footprint grids. Pickup/drop, object/door and combat mutations must update
   raw state and projections atomically; a pristine copy that becomes stale is
   not persistence. Bind generated doors directly without a second discovery;
   preserve valid native zero seeds/item values instead of authored fallbacks.
   Fence the new capability/checkpoint with metadata and generation identities,
   leaving legacy cohort/generated-world/save schemas unchanged. Negotiate new
   actor policy with compatible clients before activation. Never use expected final state
   as production input. Preserve the narrower earlier oracles and require
   ordinary Godot interaction plus cold restore before claiming playable native
   population.
   Static lighting is independent of live object collision. Live object
   collision is a separate channel, not a new SOL, missile or vision wall.
   Full light animation, rendering and RNG population,
   quest-dependent map replacement and campaign activation remain unqualified.
   Do not infer that door placement explains disconnected routes or repair native
   geometry to make a diagnostic path pass.
   Explicit per-depth seeds and optional authored encounters do not close native
   campaign seed selection or population. Keep existing timing failures visible;
   one successful diagnostic route is not M0 or Cathedral-family acceptance.
   Quest fragments/set pieces, wider topology and native population remain open;
   do not silently apply the base prototype's different-family quest fragment to L1.
   The generic shared runtime vision contract remains conservative; the detached
   cold loader's native rays and physical lighting do not qualify that live contract.
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

Use the bounded inventory and the delivery reset to select the next batch.
Native Town/L1/L2 combat, earned equipment/Gold, Town services, exact fresh-host
restore and retained revisit pass. Cathedral L1-L4 generation/owners and several
native object families are implemented; the complete four-floor gameplay journey
has not passed. Some experimental runs killed an L3 actor; latest R12 exercised
held entity attacks but died before a kill or living checkpoint.

Keep these separate from the full campaign requirements below. A failed bot
scenario does not establish a native-rule defect. Preserve failures, classify
concrete implementation/integration/evidence gaps and reuse unchanged parity,
transport and persistence proof. Current results and limitations belong in
[migration status](migration-status.md#current-delivery-position-and-verification-limits),
not another expanding run chronology in this plan.

Use the following work packages as a coverage checklist. Record open behavior
and completion in the numbered inventory, accepted verification in test history,
and detailed fixture semantics in the replay reference.

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

Native quest-pool TSV order and eight literal seeded selections are characterized
by the registered `quests_test`, including zero, signed RNG edges and the loaded
special-seed skip. The managed catalog now exposes explicit `NativeTsvRowsV1`
selection, with the same eight literal vectors verified through shipped TSV and
direct construction. Existing overloads/defaults retain `LegacyQuestIdSortedV1`
and public views stay sorted; 223 focused catalog/legacy-activation cases pass.
Complete versioned runtime adoption and old-save/ruleset world reconstruction
compatibility without changing the optional preview's base identity. Catalog
vectors do not certify full generated-world parity or a new default policy.
Three further managed facts apply the native vectors to shipped state rows,
checking unavailable/active/logged transitions and completed/non-pool preservation.
A fresh 19 metadata plus 35 quest catalog cases pass. This test-only evidence
does not close bootstrap adoption or old-save/ruleset reconstruction compatibility.
The strict checkpoint loader and explicit CurrentRulesetOnly host policy now
pass 120 focused cases. Non-cohort admission rejects identityless sidecars and
captures checkpoint restore, hello and first snapshot coherently across reload.
Failed preparation fences that token; it does not roll back provider mutations.
Compatibility remains the default. Complete production restore/profile adoption,
cohort policy and leased world-seed root binding before exposing a new runtime
policy; the opt-in library path does not close old-save reconstruction.
Explicit module policy preserves legacy identity/defaults and assigns the native
selector distinct identity 0.2.0. The 257-case integration passes base/Hellfire
activation and all-draw sequence validation. Nine subsequent focused facts pass
generated Cathedral L1 nonempty selection forwarding and unchanged quest-disabled
depth/door activation under both policies, with stable identities/content hashes.
Complete native signed-edge no-tome object presence, nonempty quest support in
depth/door profiles and host/save/root compatibility before runtime adoption.
Synthetic compiler fixtures do not establish native geometry or population parity.

Exit: supported base and Hellfire campaign routes complete in Godot, and the
coverage ledger has no unsupported required gameplay/world behavior. Each
ported behavior has native characterization, managed tests, and relevant live
integration evidence; changes retain existing save, content, and wire behavior.

## M4: Release-quality presentation and operation

Full graphical parity is a required deliverable, not only improved readability.
Use the [graphical-parity acceptance contract](feature-gap-inventory.md#godot-graphical-parity-acceptance)
for matched native/Godot scene and animation comparisons across supported worlds,
actors and UI. Preserve the complete MPQ-free client-pack route and server MPQ
independence. Remaining visual differences stay recorded as open gaps.

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

Exit: clean-install acceptance, supported compatibility matrix, full graphical
parity, complete MPQ-free play, soaks, and measured performance pass with documented artifacts.
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

1. Select a substantial, coherent deliverable that advances the earliest open
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

The inventory and near-term plan are prepared. Use [work-plan.md](work-plan.md)
for the current next-batch order. Normal cohort/profile adoption remains a separate held
change; eligible work in existing explicit profiles need not wait for it. Preserve
working launch/recovery behavior and full migration requirements. Do not restart
completed extraction work or turn old experimental acceptance scripts into
universal product gates.

### Worker delegation

- Use a fixed pool of two GPT-6 Luna (`gpt-6-luna`) implementation workers with
  `xhigh` reasoning effort and one independent GPT-6.1 Sol (`gpt-6.1-sol`)
  reviewer with `medium` reasoning effort. This supersedes all earlier worker
  models, effort settings, and five-worker policies. Do not silently substitute
  another model or effort setting; report unavailability and obtain direction.
- Create the pool once and reuse the same three workers through follow-up
  assignments until the goal is complete. Do not spawn a new worker per package
  or let workers spawn additional agents. At most three child workers may run,
  subject to the runtime's actual thread and concurrency limits. Replace an
  unavailable worker only with explicit human approval; never bypass a limit
  with extra chats or alternate orchestration.
- The primary agent selects milestone-aligned work packages, supplies scope,
  file/contract ownership, native references, and acceptance criteria, and owns
  integration, final verification, documentation, and commits.
- Delegate substantial implementation packages that can proceed independently
  alongside useful work. Before each assignment, refresh the worker's bounded
  scope, current checkpoint, file/contract ownership, prerequisites, native
  references, and acceptance criteria. Require a handoff before reassigning a
  worker. Assign disjoint files/contracts; serialize shared simulation, protocol,
  fixture-hash, and status edits.
- Have the same independent GPT-6.1 Sol reviewer at medium effort critically
  review each substantial package. The reviewer inspects the actual diff, native
  behavior, test evidence, compatibility, and plan acceptance criteria, and identifies
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
- Every worker and reviewer follows the [worker guide](ai/worker-guide.md):
  batch contracts, porting method, verification, documentation budget, stop
  conditions, handoff format and reviewer checklist.

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

- `work-plan.md`: short current/proposed execution order, batch scope and exits;
  kept synchronized with working status and affected inventory entries.
- `completion-plan.md`: full milestone dependencies, acceptance gates and workflow.
- `feature-gap-inventory.md`: stable numbered feature items, completion status,
  implemented scope, remaining gaps and feature acceptance criteria.
- `migration-status.md`: current working status/memory, milestone position,
  material blockers, WIP and operating context; revised in place.
- Architecture and protocol documents: stable ownership and compatibility rules.
- Server/content/client/Lua/pool/replay guides: specialized contracts and usage;
  link here for priority instead of maintaining another milestone sequence.
- `CHANGELOG.md`: concise chronological implementation history and consequential
  compatibility decisions, without verification transcripts.
- `test-history.md`: concise dated verification outcomes, scope and open limits;
  no raw reports, repeated attempts or historical QA dumps.
- Git history: superseded file versions, obsolete plans and deleted detail. Do
  not create separate archives or documentation backups.

Advance a milestone only on its exit evidence. Use defined completion states
and scoped evidence rather than uncalibrated percentages or invented completion
dates. Keep implementation, native parity, application and graphical acceptance
distinct in the numbered inventory. Update current summaries in place and leave
past versions to Git history; do not append run diaries to working status.

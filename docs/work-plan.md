# Near-term work plan

Updated 2026-10-21. The project was reset onto a playable-first recovery plan.
This file owns the execution order. [Working status](migration-status.md) owns
current facts, the [feature inventory](feature-gap-inventory.md) owns completion
states, and the [completion plan](completion-plan.md) keeps the full M0–M5 exits.

## Why the reset

Backend infrastructure is substantial, but no normal launch shows a playable
native Diablo world. The default client draws authored content with SVG stand-ins;
native Town/Cathedral/Catacombs exist only behind opt-in profiles. Each native
feature was rebuilt inside bespoke owner/registry/fingerprint/admission plumbing
(Catacombs: about 10k C# lines plus 10k test lines against the 2.9k-line native
`level_l2.cpp`, with no playable floor). CI had been red since 2026-08-01, most
parity tests need local-only reference data, and documentation upkeep competed
with delivery.

## Adopted decisions

1. **Parity bar.** Keep exact parity where players or saves can observe it:
   seeded generation, combat numbers and item rolls. Obtain it by close
   translation of the native source, verified by a small CI-run set of seed and
   replay comparisons, not per-feature RNG trace suites.
2. **Scope order.** Diablo single-player, all classes, Town through L16 and
   Diablo first. Multiplayer stays on the same protocol with a smoke check per
   milestone. Hellfire follows complete Diablo.
3. **Art.** User-supplied original-asset import is the primary visual path; the
   server never requires MPQs and SVG remains the fallback.
4. **Unfinished work.** Uncommitted drafts were shelved, not merged (see Phase 0).

## Phases

| Phase | Deliverable | Exit | State |
| --- | --- | --- | --- |
| 0 | Stabilize | CI green on `master`, clean tree, parity tests visible in CI | In progress |
| 1 | One world, one default path | `run-godot-client.ps1` enters native Town/Cathedral with any class; save/resume works | Next |
| 2 | Faithful native port, by module | Each dungeon family is walkable, fightable, lootable and resumable through the normal launch | Pending |
| 3 | Playable Godot client | A human plays Town to L4 rendered, then Town to Diablo | Pending |
| 4 | Finish and harden | Hellfire, multiplayer/recovery hardening, packaging, M5 legacy retirement | Pending |

### Phase 0: Stabilize

Done 2026-10-21/22:

- `authoritative-parity` CI is green on `master` (run 39720085387) for the first
  time since 2026-07-31: native replay and server-backed suites plus all six
  managed suites. It still skips 577 Diablo and 58 Server tests, mostly oracle
  cases that need local reference data.
- Restored the CI gate. The content-contract validator accepted only literal
  overlay schemas, so no managed tests had run in CI; CI also lacked the .NET 8
  runtime most projects target.
- Fixed stale tests that failed in a clean checkout: the Cathedral barrel
  useful-item fixture and two store consumable tests (both broken by
  `17b114a54` on 2026-10-16), and two launcher contract expectations that no
  longer matched refactored script lines. The Trap Disarm launcher test moved
  to the shelf with the launcher it covers. All six managed suites pass in
  Release locally.
- Made the Linux CI build portable: protoc 3.12 needs the proto3-optional flag,
  and the standalone-host package test now runs the configuration it was
  built with.
- Restored the native replay suite: the hand-written C++ replay model had not
  been updated when the managed HorkDemon AI moved to the native RNG stream on
  2026-10-04. Such parallel models are a maintenance cost the Phase 2 port
  should retire.
- Fixed a clock-gated host reporting gap: when host cancellation stopped a
  connection after its scheduled command had poisoned the lane, but before the
  writer dequeued the response, that client failure was never reported. The
  observer-fault test also raced its command failure against a second periodic
  checkpoint failure; it now fails only the command's publication and runs on
  CI again. A deterministic writer-gate test covers the host fix.
- CI regenerates the 14 archive-free native oracle corpora (about 335 MB, about
  4 minutes) and runs their 137 managed comparisons with skips treated as
  failures. Retail-archive corpora cannot be committed or published from this
  public repository and remain local. Restoring the Cathedral chest capture's
  sixth lane (accidentally dropped on 2026-10-15) was part of this.
- Shelved all uncommitted work on branch `wip/phase0-shelf`, one commit per
  group: Catacombs Magma/Acid caster chains (with the shared vision change),
  native weapon-completion fixtures, Godot class-preview and four-floor
  acceptance scenarios, and their documentation. Recover pieces from there
  when a later phase needs them; do not merge the branch wholesale.
- Moved root clutter to `.local/root-cleanup-2026-10-21/` and ignored the
  runtime files that produced it.

Remaining:

- Freeze new Catacombs-only parity work until Phase 2 reaches that family.

### Phase 1: One world, one default path

Work this queue in order, one batch at a time, using the
[worker guide](ai/worker-guide.md). Refine a batch's details in its contract;
do not add, drop or reorder batches without the planner.

| Batch | Player-visible outcome | Exit | State |
| --- | --- | --- | --- |
| 1.1 First-run native import | `run-godot-client.ps1` finds the player's `DIABDAT.MPQ` (configured path or known installs), compiles the existing native profile once under `.local/godot`, and starts in native Town. | Scripted create-game journey reaches native Town and L1; a second launch reuses the profile; with no archive the launcher explains and keeps authored content. The server still opens no MPQ. | Next |
| 1.2 One profile, all supported classes | The default native profile admits Rogue, Warrior and Sorcerer, replacing class-bound profiles and launchers. | Create each class through the normal launch, play L1, save and resume; existing class-profile saves still load. | Pending |
| 1.3 Cathedral L1–L4 by default | The existing native depth owners are part of the default profile, so a normal game reaches L4. | Scripted Town→L4→Town journey with fresh-host resume through the normal launch; obsolete Cathedral playtest launchers removed. | Pending |
| 1.4 Shared per-level state | Cathedral and Catacombs owner registries collapse into one per-level state model with no behavior change. | All suites and CI oracle comparisons green; save bytes unchanged; net reduction in owner/registry code. | Pending |
| 1.5 Split the combat processor | `DiabloCombatCommandProcessor.cs` splits into player, monster AI, missile and spell files mirroring the native modules, with no behavior change. | No file over about 2,000 lines; all suites and CI green. | Pending |

### Phase 2: Faithful native port

- Translate native gameplay modules close to line for line, one module per
  batch, keeping native structure and names: `level_l2`/`l3`/`l4`,
  `monsters.cpp` AI, `missiles.cpp`, `items.cpp`, `objects.cpp`, `quests.cpp`,
  themes and triggers.
- Order: Catacombs, Caves, Hell, quests and set levels, then the Diablo fight.
- Verify each module with a small CI-run set of seed/replay comparisons.

### Phase 3: Playable Godot client

- Render levels, actors, missiles, effects, lighting and wall transparency from
  imported original art.
- Build inventory, character, spellbook, store and quest panels, audio and the
  main menu.
- Move the acceptance harness out of the shipped client assembly or behind a
  build flag; stop adding a root launcher per scenario.

## Working rules

The [worker guide](ai/worker-guide.md) expands these into batch contracts,
porting and verification rules, a documentation budget, stop conditions and
a reviewer checklist.

- **Done means playable.** Each batch ends with a short gameplay demonstration
  through the normal launch, not only a scoped check.
- **CI stays green.** Do not commit on red; skipped parity tests are reported.
- **Lean documentation.** Record facts once, briefly. Do not add tests that
  assert documentation wording; prefer link and heading checks.
- **Refactor on contact.** Split any file over about 2k lines when it is next
  changed.
- **Fewer, larger batches** organised around a playable family and reviewed
  against the native source.

## Keep the plan current

Update this file, [working status](migration-status.md) and the affected
[inventory subitems](feature-gap-inventory.md) together when a phase or batch
starts or finishes, or when scope, evidence or blockers change. Keep history in
the [changelog](CHANGELOG.md), [test history](test-history.md) and Git.

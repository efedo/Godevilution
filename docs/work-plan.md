# Near-term work plan

Updated 2026-10-23. The project was reset onto a playable-first recovery plan.
This file owns the execution order. [Working status](migration-status.md) owns
current facts, the [feature inventory](feature-gap-inventory.md) owns completion
states, and the [completion plan](completion-plan.md) keeps the full M0–M5 exits.
Workers follow the [worker guide](ai/worker-guide.md).

## Why the reset

Backend infrastructure is substantial, but nothing a person can launch shows a
playable native Diablo world. The normal client draws authored content with SVG
stand-ins. Native Town/Cathedral routes run only as headless scripted journeys on
profiles compiled by hand from extracted retail files, and a stale local profile
is already rejected by the current launcher. Each native feature was rebuilt
inside bespoke owner/registry/fingerprint/admission plumbing (Catacombs: about 10k
C# lines plus 10k test lines against the 2.9k-line native `level_l2.cpp`, with no
playable floor). CI had been red since 2026-08-01.

## Adopted decisions

1. **Parity bar.** Keep exact parity where players or saves can observe it:
   seeded generation, combat numbers and item rolls. Obtain it by close
   translation of the native source, verified by a small CI-run set of seed and
   replay comparisons, not per-feature RNG trace suites.
2. **Scope order.** Diablo single-player, all classes, Town through L16 and
   Diablo first. Multiplayer stays on the same protocol with a smoke check at
   each phase exit. Hellfire follows complete Diablo.
3. **Art.** User-supplied original-asset import is the primary visual path; the
   server never requires MPQs and SVG remains the fallback.
4. **Playable early.** A rendered, human-playable slice comes before refactors and
   before further dungeon families, then grows floor by floor.
5. **Unfinished work.** Pre-reset drafts are shelved on `wip/phase0-shelf` and
   recovered piecemeal only when a batch needs them.

## Phases

| Phase | Deliverable | Exit | State |
| --- | --- | --- | --- |
| 0 | Stabilize | CI green on `master`, clean tree, CI-runnable parity tests in CI | Done |
| 1 | Playable native Cathedral | A human plays Town→L4→Town in a window through `run-godot-client.ps1` with native art, any supported class, save/resume and a two-player smoke | Next |
| 2 | Faithful native port, by family | Each dungeon family is playable the same way (rendered, all classes, resumable), with CI-run parity for its ported modules | Pending |
| 3 | Complete client | Every panel, quest, store and effect needed for Town to Diablo, plus audio and menus; a human plays Town to Diablo | Pending |
| 4 | Finish and harden | Hellfire, multiplayer/recovery hardening, packaging, M5 legacy retirement | Pending |

### Phase 0: Stabilize (done 2026-10-23)

CI is green with native replay, server-backed native suites, all six managed
suites and 137 CI-generated native oracle comparisons. Stale tests, a host
failure-reporting race, Linux portability and the dropped Cathedral chest
capture lane were fixed; the client renders with Vulkan. Retail-archive oracles
stay local (public repository). See the [changelog](CHANGELOG.md) for detail.
Catacombs-only parity work stays frozen until Phase 2 reaches that family.

### Phase 1: Playable native Cathedral

Work this queue in order, one batch at a time. Refine details in each batch
contract; do not add, drop or reorder batches without the planner.

| Batch | Player-visible outcome | Exit | State |
| --- | --- | --- | --- |
| 1.1a C# MPQ reader | Client-side tools read `DIABDAT.MPQ` (and `spawn.mpq`) directly with a faithful C# port of DevilutionX's `mpqfs` reader and PKWARE explode; no external extractor. | Native `mpqfs` digests of synthetic archives and of every `spawn.mpq` file match the C# reader in CI; a local run matches every needed `DIABDAT.MPQ` file. | Done |
| 1.1b C# semantic table export | The profile's semantic tables are built in C# from the open `txtdata`, source constants and archive level files, replacing the native capture and export steps. | Output equals the existing native-exported tables exactly (local retail check); the profile compilers accept it unchanged. | Next |
| 1.1c First-run setup | `run-godot-client.ps1` finds the player's archive (configured path or known installs), builds the profile and art once under `.local/godot`, rebuilds when inputs, tools or schemas change, and starts a windowed game in native Town. | Scripted create-game journey reaches native Town and L1 from a clean `.local/godot`; a second launch reuses the profile; a changed fingerprint rebuilds; with no archive the launcher explains and keeps authored content. The server still opens no MPQ. | Pending |
| 1.2 One profile, all supported classes | The default native profile admits Rogue, Warrior and Sorcerer, replacing class-bound profiles and launchers. | Create each class through the normal launch, play L1, save and resume; existing class-profile saves still load. | Pending |
| 1.3 Cathedral L1–L4 by default | The existing native depth owners are part of the default profile, so a normal game reaches L4. | Scripted Town→L4→Town journey with fresh-host resume through the normal launch; obsolete Cathedral playtest launchers removed. | Pending |
| 1.4 Rendered slice | Town→L4 renders with native players, scenes, residents and objects, and the existing inventory, character, spellbook and store panels work with real controls. | A visible scripted journey captures each floor; the user plays Town→L4→Town and reports it playable. | Pending |
| 1.5 Two-player smoke | Two windowed clients join one host on the native profile and fight together on L1. | Scripted two-client journey with cold resume; both clients render each other. | Pending |
| 1.6 Shared per-level state | Cathedral and Catacombs owner registries collapse into one per-level state model with no behavior change. | All suites and CI oracle comparisons green; save bytes unchanged; net reduction in owner/registry code. | Pending |
| 1.7 Split the combat processor | `DiabloCombatCommandProcessor.cs` splits into player, monster AI, missile and spell files mirroring the native modules, with no behavior change. | No file over about 2,000 lines; all suites and CI green. | Pending |

### Phase 2: Faithful native port, by family

- Translate native gameplay modules close to line for line, one module or
  function family per batch, keeping native structure, names and RNG order.
- Order: Catacombs (L5–L8), Caves (L9–L12), Hell (L13–L16), then quests, set
  levels and the Diablo fight. Each family ends playable like the Phase 1 exit.
- Catacombs reuses what is already proven by native comparisons (generation,
  population, actor admission, doors, chests, ranged clocks, Arrow flight) and
  translates the rest; the shelved Magma/Acid caster chains are reviewed for
  reuse, not merged wholesale.
- Every new native comparison must be CI-runnable (see the worker guide).
- Replace the legacy seeded `(seed * 1664525 + 1013904223) % N` rolls (PvP melee
  hit, item affix/unique rolls, C++ replay model) with the native RNG stream as
  part of the combat and item ports; see inventory 2.8.

### Phase 3: Complete client

- Remaining panels and flows: quests, the full store set, belt, spellbook
  details, automap, death and game-over, main menu and options.
- Effects, lighting, palette cycling and wall transparency; audio.
- Move the acceptance harness out of the shipped client assembly or behind a
  build flag; stop adding a root launcher per scenario.

## Working rules

The [worker guide](ai/worker-guide.md) expands these into batch contracts,
porting and verification rules, a documentation budget, stop conditions and
a reviewer checklist.

- **Done means playable.** Each batch ends with a gameplay demonstration through
  the normal launch, not only a scoped check.
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

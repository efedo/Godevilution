# Worker Guide

Instructions for agents (implementers and reviewers) working on Godevilution.
Read this file, [AGENTS.md](../../AGENTS.md) and the current phase of the
[work plan](../work-plan.md) before starting. Do not read the long histories
(`test-history.md`, `CHANGELOG.md`, `replay-fixtures.md`, the architecture
document) end to end; search them for the specific contract you need.

## The goal

Ship a playable game: a player launches the normal client, creates a character,
and plays Diablo from Town to Diablo with server-authoritative rules that match
the original. Every batch must move that playable route forward or remove a
concrete blocker to it. Work that only adds evidence, characterization, harness
scenarios, status prose or bespoke infrastructure is not progress by itself.

## How this project went off track

Avoid repeating these. Each item is a real pattern from this repository's history.

1. **Parity depth replaced breadth.** Weeks went into exact traces of single
   mechanics (16.16 arrow movement, Magma light radii, puddle frame timing)
   while no normal launch showed a native world and L3+ was unplayable.
2. **Bespoke plumbing per feature.** Each native feature got its own owner,
   registry, fingerprint, detached-admission and cold-restore path. Catacombs
   grew to about 10k C# lines plus 10k test lines against a 2.9k-line native
   source, with no playable floor.
3. **Parallel opt-in worlds.** Native content lived behind per-class,
   per-family profiles and launchers while the default launch stayed on
   authored SVG content, so nothing a player could run improved.
4. **Red CI ignored.** CI was red from 2026-08-01 to 2026-10-22. Regressions
   (a broken chest capture, stale tests, a C++ model left behind by a C# change)
   accumulated unseen because most parity tests skipped everywhere but one PC.
5. **Documentation as output.** About 9,700 lines of dense status prose, plus
   tests asserting exact documentation sentences, consumed effort and broke on
   rewording.
6. **Unfinished work piled up.** Seventy uncommitted changes across five
   unrelated streams had to be shelved wholesale.
7. **Scope narrowed to pass.** When a journey failed, the claim was narrowed
   ("scoped headless boundary") instead of the defect being fixed or reported.

## Choosing work

- Take the next unstarted batch from the current phase's queue in the
  [work plan](../work-plan.md). Do not invent batches, reorder the queue, or
  start a later phase. If the queue is empty or blocked, stop and report.
- Before writing code, write the batch contract below into your handoff (not a
  new file) and get it acknowledged by the planner.
- Frozen until the work plan reopens them: new Catacombs-only parity work,
  new opt-in profiles or launchers, new acceptance scenarios that are not part
  of the batch exit, and Hellfire.

### Batch contract

State each item in one or two lines:

- **Player-visible outcome:** what a player can do after this batch that they
  could not before, through the normal launch.
- **Native source:** the native files and functions being ported or matched.
- **Files and contracts owned:** what you will change; everything else is out
  of scope.
- **Exit check:** the exact commands and the gameplay demonstration that prove
  the outcome. A demonstration is a scripted acceptance run that drives the real
  client controls against the normal launch configuration (headless by default;
  visible runs only when the user authorizes them).
- **Out of scope:** what you will deliberately not do.
- **Size:** expected diff size. A batch is roughly one to five days of work.
  If it will clearly exceed that, split it along a playable seam first.

## Porting method

- **Translate, do not reinvent.** Port native gameplay code close to line for
  line into C#: same function boundaries, names (in C# casing), branch order and
  RNG call order. Cite the native file and function in a short comment once per
  ported function. Port a whole module or function family per batch, not single
  branches.
- **Native code is the specification.** When behavior is unclear, read the
  native source in `legacy-native/`; do not build a trace harness to discover
  what the code plainly says.
- **One state model.** Store per-level gameplay state in the shared level state,
  not in a new per-feature registry with its own fingerprint, admission and
  restore path. If shared state is missing something, extend it once for all.
- **No new parallel models.** Do not add logic to the hand-written C++ replay
  model (`legacy-native/game/replay/replay_fixture.cpp`) unless a fixture you
  own requires it; prefer captures from the real native code.
- **Keep boundaries.** Game rules stay in `Devilution.Diablo`; the host,
  protocol adapter and Godot client stay thin. The server never requires MPQs.
- **Backward compatibility.** Existing saves and content packs keep loading.
  Add additive schema versions; do not rewrite frozen formats.
- **Files stay reviewable.** Split any file over about 2,000 lines when you
  next change it, as a separate zero-behavior-change commit.

## Verification

- **Smallest sufficient evidence.** Unit tests for new logic, the existing
  oracle or replay comparisons the change could affect, and one gameplay
  demonstration through the normal launch. Do not add per-feature native trace
  suites unless a concrete counterexample shows the translation is wrong.
- **Oracles must run in CI.** A new native capture is only acceptable if CI can
  regenerate it: add it to `tools/run-native-oracle-parity.sh` with its consumer
  classes. Captures that need the retail archive stay local and must not be the
  only evidence for a batch. Never commit or upload retail-derived data; the
  repository is public.
- **No timing assumptions in tests.** Poll with a bounded loop instead of
  fixed delays; do not depend on observing a state that lasts a few ticks; do
  not race two independent failures in one test. Use deterministic seams
  (test gates, injected clocks) for concurrency.
- **No documentation-wording tests.** Test behavior, not prose. Link and
  heading checks are acceptable.
- **Explicit tests** (`[Fact(Explicit = true)]`) are for local-only evidence.
  Anything CI can run must not be explicit.
- **Report truthfully.** If a check fails or was not run, say so with the
  output. Never narrow a claim to make it pass; fix the defect or report it.

## Documentation budget

Per batch, at most:

- one to three lines in the [work plan](../work-plan.md) (queue item state);
- one changelog bullet of up to three lines;
- one test-history entry of up to six lines, only for new acceptance evidence;
- the affected inventory rows in
  [feature-gap-inventory.md](../feature-gap-inventory.md), state and one line.

Do not add new documentation files, status essays, run diaries or duplicated
summaries. Update guides only when a command or contract changes.

## Commits and CI

- `master` CI must stay green. If CI is red, fixing it is the next task.
- Commit each batch as one or a few coherent commits containing code, tests and
  the documentation above. Run the affected suites before committing.
- Do not leave uncommitted work at the end of a batch. If a batch must stop
  early, commit finished parts and move the rest to a `wip/` branch, noted in
  the handoff.
- Never stage unrelated files. Do not commit scratch, logs or retail data.
- Push only when the user has instructed it.
- Use CRLF for project-authored files, except shell scripts and
  Godot-written files (`project.godot`, `.import`), which keep LF.

## Stop and escalate

Stop work and report to the planner instead of continuing when:

- the batch has run past its size estimate by half with no demonstrable
  progress on its player-visible outcome;
- the exit check cannot be met without changing a shared contract, save format
  or protocol outside the batch's ownership;
- a native behavior seems to contradict the current C# design;
- you are about to add a new profile, launcher, registry, oracle family or
  documentation file;
- CI turns red for a reason outside the batch.

## Handoff format

End every batch or assignment with:

- outcome against the batch contract (met or not, in one line);
- commits, and any `wip/` branch;
- checks run, with pass/fail counts, and anything not run;
- the gameplay demonstration performed, and what it showed;
- open defects or risks found, each in one line;
- suggested next batch, if the queue needs refinement.

## Reviewer checklist

Reviewers check the actual diff and evidence, not the summary:

1. Does the diff deliver the batch contract's player-visible outcome, and only
   that? Flag scope creep and unrequested infrastructure.
2. Does ported code follow the native source structure and RNG order? Spot-check
   against `legacy-native/`.
3. Is state kept in the shared model, with no new per-feature registry,
   profile or launcher?
4. Were the stated checks run, do CI-capable comparisons run in CI, and is CI
   green?
5. Are tests deterministic (no fixed sleeps, short windows or racing faults)?
6. Is the documentation within budget, with no prose assertions in tests?
7. Do existing saves, content packs and protocol peers stay compatible?

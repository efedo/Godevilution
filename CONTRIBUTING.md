# Contributing to Godevilution

Godevilution is migrating a DevilutionX-derived native engine toward a C#
authoritative server and Godot presentation client. Preserve native behavior
and the protocol/replay contracts while making changes incrementally.

## Before Changing Code

Read `docs/csharp-authoritative-server-migration.md`,
`docs/migration-status.md`, and `docs/completion-plan.md`. Select work against
the active milestone's acceptance gate. Keep gameplay and persistence
server-owned, the Godot client presentation-only, and the native path backward
compatible.

## Tests

- Add native C++ tests with GTest and register new targets in
  `CMake/Tests.cmake`.
- Add C# domain, data, game-module, server, and protocol-client tests to the
  corresponding xUnit v3 projects.
- Update shared replay fixtures when authoritative behavior or canonical state
  hashing changes.
- Run the narrowest meaningful affected tests for each logical change. Run
  broader affected suites at integration/milestone gates or when changes,
  failures, or unresolved risks justify them; do not repeat unchanged suites
  after every small edit.
- Use actual application acceptance for launchers and Godot controls. A
  source-contract test, protocol test, or headless startup does not prove that
  the visible client can create and enter a game.

See `docs/ai/msvc-build.md` for native MSVC setup and
`docs/ai/godot-build.md` for C# and Godot verification.

## Documentation and Format

Use CRLF for project-authored documentation and source unless `.editorconfig`
specifies another platform format. Keep documentation concise and avoid
duplicating current status across multiple files. Update `docs/CHANGELOG.md`
with a brief summary of completed changes, and summarize the change in the
commit message.

Use cohesive commits containing a completed behavior family or playable slice,
its relevant tests, and documentation. Avoid separate commits for individual
fixture rows or status sentences. Follow `docs/completion-plan.md` for delivery
order; update the milestone evidence in status rather than creating another plan.

Do not commit generated build output, local saves, proprietary Diablo game
data, or temporary agent planning files.

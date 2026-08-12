# Contributing to Godevilution

Godevilution is migrating a DevilutionX-derived native engine toward a C#
authoritative server and Godot presentation client. Preserve native behavior
and the protocol/replay contracts while making changes incrementally.

## Before Changing Code

Read `docs/csharp-authoritative-server-migration.md` and
`docs/migration-status.md`. Keep gameplay and persistence server-owned, keep the
Godot client presentation-only, and keep the native path backward compatible.

## Tests

- Add native C++ tests with GTest and register new targets in
  `CMake/Tests.cmake`.
- Add C# domain, data, game-module, server, and protocol-client tests to the
  corresponding xUnit v3 projects.
- Update shared replay fixtures when authoritative behavior or canonical state
  hashing changes.
- Run the narrowest affected tests, then the broader relevant suite.

See `docs/ai/msvc-build.md` for native MSVC setup and
`docs/ai/godot-build.md` for C# and Godot verification.

## Documentation and Format

Use CRLF for project-authored documentation and source unless `.editorconfig`
specifies another platform format. Keep documentation concise and avoid
duplicating current status across multiple files. Update `docs/CHANGELOG.md`
with a brief summary of completed changes, and summarize the change in the
commit message.

Do not commit generated build output, local saves, proprietary Diablo game
data, or temporary agent planning files.

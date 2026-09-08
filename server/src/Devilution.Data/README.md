# Devilution.Data

`Devilution.Data` is the engine- and host-independent runtime for validated
authoritative content. It targets .NET 8, references only `Devilution.Domain`,
and must not depend on the server host, Protobuf, Godot, or a game-specific
rules module.

The runtime currently owns:

- deterministic TSV parsing with typed required and optional fields;
- canonical relative resource paths that reject traversal and path aliases;
- ordered directory loading for external content packs;
- manifest-declared pack dependencies and composite-key table overlays;
- versioned schema identities and inclusive semantic dependency constraints;
- deterministic table composition with exact ordered-schema validation;
- transactional content activation and reload diagnostics;
- content-pack and manifest identity with native-compatible canonical hashes;
- validated, bidirectional stable symbolic content IDs.

Run its independent xUnit v3 suite from the repository root:

```powershell
dotnet run --project server/tests/Devilution.Data.Tests/Devilution.Data.Tests.csproj
```

Game-specific schemas consume `TsvTable` from separate gameplay modules. The
first-party schemas live in `Devilution.Diablo`. The generic runtime must not
acquire Diablo rules merely to simplify a caller.

`ComposedContent` applies packs in manifest order. An existing table can only
be changed by an explicit `upsert` declaration; an optional table may also be
introduced by an overlay when its declared keys are unique and non-empty.
Replacements retain their base row position and new keys append in overlay
order. `ContentRuntime.TryReload`
publishes a candidate only after the complete pack set composes successfully,
so callers retain the previous immutable content after a rejected reload.
Compatibility metadata is optional for legacy packs. New manifests should use
`formatVersion: 1`, declare their schema family/version, and constrain every
dependency whose version affects their interpretation. Compatibility metadata
contributes to identity without changing hashes for legacy manifests that omit
it.

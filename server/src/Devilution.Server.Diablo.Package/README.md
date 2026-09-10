# Packaged Diablo host adapter

`Devilution.Server.Diablo.Package` is the first production adapter package
entry point. It exposes the Diablo host factory through the generic
`game-host-adapter.json` boundary while keeping the implementation and its
game-specific assemblies in the package's private dependency closure.

Build the project, copy its entry assembly plus the private
`Devilution.Server.Diablo`, `Devilution.Diablo`, and `Devilution.Domain`
assemblies into a package directory, and list each file with its SHA-256 in
`dependencies`. `Google.Protobuf` is a shared runtime contract and must not be
copied into the package.
`Devilution.Data`, `Devilution.Engine`, `Devilution.Server.Host`, and
`Devilution.Protocol` remain shared contracts and must not be duplicated in
the package. For upgrade-safe deployments, add a `sharedContracts` object to
the manifest mapping each shared assembly name to its SHA-256; the host checks
those hashes before loading the entry assembly. The field is optional for
backward-compatible development packages. Select the package with `--game-adapter`
`godevilution.diablo.package` and repeat `--game-adapter-package` with its
directory.

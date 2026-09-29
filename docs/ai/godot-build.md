# Godot Build Notes

The presentation project targets Godot 4.7.1 Mono. Godot is not assumed to be
installed on every development machine, and repository tests that do not render
the client must remain runnable without it.

## Protocol and Solution Verification

From the repository root:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build server/src/Devilution.Server/Devilution.Server.csproj
dotnet build godot/Devilution.Client/Devilution.Client.sln
```

The protocol-test project builds the current server once as an explicit,
build-only dependency, then the live tests launch that Debug server without
starting concurrent rebuilds.

For a focused class or method, invoke the xUnit v3 in-process runner directly.
This avoids requiring the VSTest testhost package cache:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj --no-build -- --filter-class Devilution.Client.Protocol.Tests.AuthoritativeTargetSelectorTests --no-ansi
```

Replace the class name with the focused test class under investigation. Use
`--filter-method` with the fully qualified method name for one test method.
Build the affected project first; `--no-build` is valid only when its binaries
already include the change being checked. Run broader suites at integration
gates or when new changes/failures justify them, as described in the
[completion plan](../completion-plan.md#verification-policy).

## Finding Godot

Use a Godot 4.7.1 Mono console executable. When installed through WinGet it may
not immediately appear in an existing shell's `PATH`. Discover it rather than
hard-coding an installation directory:

```powershell
$godot = (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application).Source
& $godot --headless --path godot/Devilution.Client --quit-after 5
```

The root launcher is the normal zero-argument entry point:

```powershell
powershell -ExecutionPolicy Bypass -File run-godot-client.ps1
```

It delegates to `godot/run-local-client.ps1`, which also checks the standard
per-user WinGet package directory. For headless diagnostics or an explicit
executable, invoke that underlying harness directly. The harness builds the
authoritative server if its Debug executable is absent,
starts it on a free port, passes the current ruleset identity to the client, and
reports the temporary server-log directory. It also creates a new save root in
that directory per launch. Explicitly build both the server and Godot solution
before launching after source changes; an existing server binary is not
rebuilt by this harness. A resume token requires the matching host save root.
Automatic build freshness and user-accessible persistent resume are M0/M1 work.

Protocol tests exercise the model/transport without the Godot scene. A headless
startup verifies a different boundary and does not prove that a visible window
renders or its controls create a game. The M0/M1 application acceptance must
exercise actual controls, authoritative movement, and save/restart/resume, with
a separate visible check for rendering and window behavior.

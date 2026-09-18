# Godot Build Notes

The presentation project targets Godot 4.7.1 Mono. Godot is not assumed to be
installed on every development machine, and repository tests that do not render
the client must remain runnable without it.

## Protocol and Solution Verification

From the repository root:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build godot/Devilution.Client/Devilution.Client.sln
```

The protocol-test project builds the current server once as an explicit,
build-only dependency, then the live tests launch that Debug server without
starting concurrent rebuilds.

For a focused class or method, invoke the xUnit v3 in-process runner directly;
this avoids requiring the VSTest testhost package cache:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj --no-build -- -class Devilution.Client.Protocol.Tests.AuthoritativeTargetSelectorTests -noColor
```

Replace the class name with the focused test class under investigation. The
same runner accepts `-method` for one test method.

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
reports the temporary server-log directory. Build the solution explicitly
before a direct Godot smoke launch; this is more deterministic than relying on
the editor's solution-build switch.

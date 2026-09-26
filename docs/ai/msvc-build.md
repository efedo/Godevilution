# MSVC Build Notes for Agents

Use the Visual Studio developer environment. Plain PowerShell may find `cl.exe`
or CMake but still miss standard MSVC headers such as `cstdint`, `array`, or
`Windows.h`.

The current working command prefix is:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && <command>'
```

The Visual Studio bundled CMake used by this workspace is:

```bat
"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
```

Configure `x64-Debug` from a clean tree:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON'
```

Build the game:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target devilutionx'
```

Build a focused test target:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target debug_console_history_test && build\x64-Debug\debug_console_history_test.exe'
```

Run CTest after test binaries are built:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe" --test-dir build\x64-Debug --output-on-failure'
```

Avoid using `--target all` as a shortcut for the test suite unless benchmarks are
also intended; this target builds benchmark executables too. To build only the
registered GTest executables, use the target names listed in `CMake/Tests.cmake`
under `tests` and `standalone_tests`.

If CMake regeneration reports dubious repository ownership, use repository-local
safe-directory flags where possible:

```bat
git -c safe.directory=D:/Programming/Godevilution status --short
```

If vcpkg reports permission errors under `%LOCALAPPDATA%\vcpkg`, the command
must run with permission to access the user vcpkg registry cache.

To build the authoritative native client slice, configure a separate build
directory with the server-backed feature enabled. Keep the default build
unchanged because that feature requires Protobuf:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug-authoritative -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON -DDEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON -DCMAKE_TOOLCHAIN_FILE="C:\Program Files\Microsoft Visual Studio\18\Community\VC\vcpkg\scripts\buildsystems\vcpkg.cmake"'
```

The server-backed option activates the `authoritative-client` vcpkg manifest
feature. A fresh configure can therefore take several minutes while vcpkg
builds Protobuf and its dependencies. Keep the toolchain argument present;
without manifest mode CMake cannot resolve the required Protobuf package on the
verified Windows setup.

The authoritative client expects the server's content and ruleset hashes.
The production launcher selects protocol `0.2.0` and passes the printed hashes
independently.
On Windows, `run-authoritative-client.ps1` starts a local server on a
free port, reads those hashes, persists a resume token, and launches the
client with matching arguments. A handshake identity mismatch writes a
timestamped diagnostic report to `authoritative-diagnostics` (or the
directory supplied with `--authoritative-diagnostics`). The report includes
the endpoint, working directory, expected identities, and server identities.
The root launcher takes no arguments:

```powershell
run-authoritative-client.ps1
```

The root launcher selects only a CMake directory configured with
`DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON`, initializes the Visual Studio
developer environment, and incrementally rebuilds both native client and server
on every run. It also verifies the server's reported single-player mode before
opening the client, so stale binaries and mode mismatches fail before native
level setup.

The C# xUnit project is an executable Microsoft Testing Platform runner. Run
it directly when `dotnet test` selects a machine-wide VSTest host whose
`testhost` package is unavailable:

```bat
dotnet build server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj --no-restore
server\tests\Devilution.Server.Tests\bin\Debug\net10.0\Devilution.Server.Tests.exe --no-ansi
```

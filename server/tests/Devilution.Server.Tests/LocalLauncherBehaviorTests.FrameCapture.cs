using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{
    [Fact]
    public void NormalFrameCapturePassesItsSettingsAndStagedAssemblyPathWithoutChangingPerformanceFlags()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Start-SceneClient"], """
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-FrameCaptureLauncher-' + [guid]::NewGuid().ToString('N'))
            $SessionDirectory = Join-Path $work 'session'
            $captureRoot = Join-Path $work 'logs'
            $clientProject = Join-Path $work 'project'
            $PersistenceMode = 'cohort'; $Headless = $true; $Multiplayer = $false; $AcceptancePhase = ''
            $clientCount = 1
            $clientRunPolicy = [pscustomobject]@{ SceneQuitAfterSeconds = $null; SuppressCrashDialog = $true }
            function New-GodotProcess($Role, $Arguments, $Environment, $IsHeadless, $SuppressCrashDialog) {
                $script:childEnvironment = $Environment
                $script:childArguments = $Arguments
                return [pscustomobject]@{ Process = $null }
            }
            try {
                foreach ($capture in @($false, $true)) {
                    foreach ($performance in @($false, $true)) {
                        $savedEnvironment = @{
                            GODEVILUTION_CLIENT_FRAME_CAPTURE = $(if ($capture) { Join-Path $work 'frames' } else { $null })
                            GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS = '10'
                            GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS = '30'
                            GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES = '128'
                        }
                        Start-SceneClient 1 | Out-Null
                        if ($childEnvironment.ContainsKey('GODEVILUTION_CLIENT_FRAME_CAPTURE') -ne $capture) { throw 'Capture was not opt-in.' }
                        if ($capture -or $performance) {
                            if ($childEnvironment.GODEVILUTION_CLIENT_ASSEMBLY_IMAGE_DIRECTORY -cne (Join-Path $clientProject '.godot/mono/temp/bin/Debug')) { throw 'Recorder cannot find its staged assembly images.' }
                        } elseif ($childEnvironment.ContainsKey('GODEVILUTION_CLIENT_ASSEMBLY_IMAGE_DIRECTORY')) { throw 'Normal launch acquired capture settings.' }
                        if ($performance) {
                            if ($childArguments -notmatch '--max-fps 60' -or $childEnvironment.GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS -cne '0' -or
                                $childEnvironment.GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS -cne '60') { throw 'Performance flags changed.' }
                        } elseif ($capture) {
                            if ($childEnvironment.GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS -cne '10' -or
                                $childEnvironment.GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS -cne '30' -or
                                $childEnvironment.GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES -cne '128') { throw 'Normal capture settings were lost.' }
                        } elseif ($childEnvironment.ContainsKey('GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS')) { throw 'Normal launch acquired a measurement window.' }
                    }
                }
                'PASS'
            } finally {
                if (Test-Path -LiteralPath $work) {
                    $resolvedWork = [IO.Path]::GetFullPath($work)
                    if ([IO.Path]::GetDirectoryName($resolvedWork) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
                        [IO.Path]::GetFileName($resolvedWork) -notmatch '^Godevilution-FrameCaptureLauncher-[a-f0-9]{32}$') { throw 'Unexpected test cleanup path.' }
                    [IO.Directory]::Delete($resolvedWork, $true)
                }
            }
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}

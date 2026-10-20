using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises the native-object evidence validator against valid evidence and scoped mutations. */
public sealed class NativeObjectLauncherEvidenceTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Uses the Windows PowerShell launcher runtime.")]
    public void NativeObjectArtEvidenceRequiresActualBoundDrawsAndHonestColdScope()
    {
        var root = FindRepositoryRoot();
        var wrapperPath = Path.Combine(root, "tools/test-godot-native-classes.ps1");
        var script = string.Join("\r\n", [
            "$ErrorActionPreference = 'Stop'",
            $"$wrapperPath = {QuotePowerShell(wrapperPath)}",
            "$wrapperText = [IO.File]::ReadAllText($wrapperPath)",
            "$tokens = $null; $parseErrors = $null",
            "$wrapperAst = [System.Management.Automation.Language.Parser]::ParseInput($wrapperText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
            "$functionAst = $wrapperAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-NativeObjectArtEvidence' }, $true) | Select-Object -First 1",
            "if ($null -eq $functionAst) { throw 'Could not extract the native object-art evidence validator.' }",
            ". ([scriptblock]::Create($functionAst.Extent.Text))",
            "$originalObjectPack = $env:GODEVILUTION_NATIVE_OBJECT_PACK",
            "try {",
            "    $env:GODEVILUTION_NATIVE_OBJECT_PACK = 'res://assets/native-objects/native-object-catalog.json'",
            "    $packId = 'native-object-' + ('c' * 24)",
            "    function New-Draw([uint32]$Frame, [bool]$Activated, [uint64]$Tick) {",
            "        [pscustomobject]@{ NativeArt = $true; GenericFallback = $false; AnchorInsideViewport = $true; EntityId = 23; NativeObjectId = 6; LevelId = 1; SnapshotTick = $Tick; NativeWidth = 96; PackId = $packId; Destroyed = $false; TrapArmed = $false; PositionX = 19; PositionY = 25; AtlasPath = 'res://assets/native-objects/sprites/1-chest1.png'; Width = 80; Height = 48; AnchorX = 240; AnchorY = 180; DrawX = 224; DrawY = 133; NativeFrame = $Frame; Activated = $Activated }",
            "    }",
            "    function New-Evidence([bool]$Resumed) {",
            "        $interaction = [pscustomobject]@{ NativeObjectId = 6; EntityId = 23; BeforeFrame = 4; AfterFrame = 6; Sequence = 19; AppliedTick = 880; BeforeDraw = (New-Draw 4 $false 879); AfterDraw = (New-Draw 6 $true 880) }",
            "        $checkpoint = [pscustomobject]@{ ContentHash = ('A' * 64); GeometryHash = ('B' * 64); Objects = @('AQID'); Interaction = $interaction }",
            "        $pack = [pscustomobject]@{ IsLoaded = $true; PackId = $packId; ContentManifestHash = ('A' * 64); AtlasesReady = $true; LoadedAtlasPaths = @('0','1','2','3','4','5','6','7'); MissingAtlasPaths = @() }",
            "        $nativeObjects = [pscustomobject]@{ Passed = $true; CurrentSnapshotFramesOnly = $true; NativeLifecycleTimingParity = $false; NativeLightingAndScenePhaseParity = $false; RenderedParity = $false; ColdL1RedrawVerified = $false; ColdObjectFrameStateCollisionRetained = $Resumed; Pack = $pack; Checkpoint = $checkpoint; Samples = @((New-Draw 6 $true 880)) }",
            "        [pscustomobject]@{ NativeObjects = $nativeObjects; ContentHash = ('A' * 64) }",
            "    }",
            "    function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {",
            "        try { & $Action; throw 'Expected native object-art evidence to be rejected.' }",
            "        catch { if ($_.Exception.Message -cnotlike ('*' + $ExpectedMessage + '*')) { throw ('Unexpected object-art evidence result: ' + $_.Exception.Message) } }",
            "    }",
            "    $journey = New-Evidence $false",
            "    Assert-NativeObjectArtEvidence $journey $false",
            "    $resume = New-Evidence $true",
            "    Assert-NativeObjectArtEvidence $resume $true",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Pack.LoadedAtlasPaths = @('0','1','2','3','4','5','6')",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'complete texture family'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.AfterDraw.NativeFrame = 5",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'authoritative chest state'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.AfterDraw.SnapshotTick = 879",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'authoritative chest state'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.BeforeDraw.PackId = 'native-object-' + ('d' * 24)",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'correctly placed chest draw'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.AfterDraw.PositionX++",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'authoritative chest state'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.BeforeDraw.DrawX++",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'correctly placed chest draw'",
            "    foreach ($field in @('Destroyed', 'TrapArmed')) {",
            "        $scene = New-Evidence $false",
            "        $scene.NativeObjects.Checkpoint.Interaction.AfterDraw.$field = $true",
            "        Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'correctly placed chest draw'",
            "    }",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.PSObject.Properties.Remove('ColdObjectFrameStateCollisionRetained') | Out-Null",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'exact cold state contract'",
            "    $scene = New-Evidence $true",
            "    $scene.NativeObjects.ColdObjectFrameStateCollisionRetained = $false",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $true } 'exact cold state contract'",
            "    foreach ($claim in @('NativeLifecycleTimingParity', 'NativeLightingAndScenePhaseParity', 'RenderedParity', 'ColdL1RedrawVerified')) {",
            "        $scene = New-Evidence $false",
            "        $scene.NativeObjects.$claim = $true",
            "        Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'exact cold state contract'",
            "    }",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Checkpoint.Interaction.BeforeDraw = $null",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'correctly placed chest draw'",
            "    $scene = New-Evidence $false",
            "    $scene.NativeObjects.Samples[0].GenericFallback = $true",
            "    Assert-Rejected { Assert-NativeObjectArtEvidence $scene $false } 'complete texture family or actual object samples'",
            "    Write-Output 'PASS'",
            "} finally {",
            "    if ($null -eq $originalObjectPack) { Remove-Item Env:GODEVILUTION_NATIVE_OBJECT_PACK -ErrorAction SilentlyContinue }",
            "    else { $env:GODEVILUTION_NATIVE_OBJECT_PACK = $originalObjectPack }",
            "}",
        ]) + "\r\n";

        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }

    private static string RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"godevilution-native-object-evidence-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try {
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "powershell.exe",
                    Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            Assert.True(process.Start(), "Could not start Windows PowerShell.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(10_000), "Timed out PowerShell process did not exit after termination.");
                Assert.Fail("PowerShell behavior harness timed out.");
            }
            Assert.True(stdout.Wait(10_000) && stderr.Wait(10_000), "PowerShell output did not drain after exit.");
            var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0, $"PowerShell behavior harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
            return output;
        } finally {
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
    }

    private static string QuotePowerShell(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}

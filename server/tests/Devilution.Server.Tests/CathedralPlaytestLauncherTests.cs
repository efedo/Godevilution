using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Exercises the real optional profile guards without starting Godot or a server. */
public sealed class CathedralPlaytestLauncherTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Requires Windows PowerShell and directory junctions.")]
    public async Task ProfileAndSessionGuardsRejectChangedContentOldSavesAndReparsePoints()
    {
        var root = FindRepositoryRoot();
        var script = " $repository = '" + root.Replace("'", "''", StringComparison.Ordinal) + "'\r\n" + """
            $ErrorActionPreference = 'Stop'
            $tokens = $null
            $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repository 'godot-client/run-local-client.ps1'), [ref]$tokens, [ref]$errors)
            if ($errors) { throw 'The launcher must parse.' }
            $parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
            foreach ($name in @('CathedralDoorProfileDirectory', 'AcceptancePhase')) {
                if ($name -notin $parameterNames) { throw "Missing launcher argument: $name" }
            }
            if ($ast.Extent.Text -notmatch 'CathedralDoorJourney' -or $ast.Extent.Text -notmatch 'CathedralDoorResume' -or
                $ast.Extent.Text -notmatch 'GODEVILUTION_CATHEDRAL_DOOR_PROFILE') {
                throw 'The launcher omitted the Cathedral door phase or profile environment contract.'
            }
            foreach ($name in @('Get-FileSha256','Assert-NoCathedralReparsePoint','Get-CathedralContentTreeHash','Get-CathedralPlaytestProfile','Initialize-CathedralPlaytestSession','Get-CathedralDoorPlaytestProfile','Initialize-CathedralDoorPlaytestSession')) {
                $functions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true))
                if ($functions.Count -ne 1) { throw "Missing actual launcher helper: $name" }
                Invoke-Expression $functions[0].Extent.Text
            }
            function Reject([scriptblock]$Action, [string]$Expected) {
                $failure = $null
                try { & $Action } catch { $failure = $_.Exception.Message }
                if (-not $failure -or -not $failure.Contains($Expected)) { throw "Expected rejection '$Expected'; got '$failure'." }
            }
            $work = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-CathedralLauncherTest-' + [guid]::NewGuid().ToString('N'))
            $content = Join-Path $work 'content'
            $session = Join-Path $work 'sessions/one'
            $junction = Join-Path $session 'save'
            $lock = $null
            try {
                [IO.Directory]::CreateDirectory($content) | Out-Null
                [IO.File]::WriteAllText((Join-Path $content 'content-pack.json'), '{"formatVersion":1}')
                # Ordinal Windows full-path sorting places a0 before a\b;
                # the compiler's normalized relative-path contract places a/b first.
                [IO.Directory]::CreateDirectory((Join-Path $content 'a')) | Out-Null
                [IO.File]::WriteAllText((Join-Path $content 'a/b.dun'), 'nested')
                [IO.File]::WriteAllText((Join-Path $content 'a0.tsv'), 'sibling')
                $canonical = [Text.StringBuilder]::new()
                foreach ($relative in @('a/b.dun', 'a0.tsv', 'content-pack.json')) {
                    [void]$canonical.Append($relative.Length).Append(':').Append($relative).Append(':')
                    [void]$canonical.Append((Get-FileSha256 (Join-Path $content $relative)).ToUpperInvariant()).Append("`n")
                }
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $expected = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical.ToString()))).Replace('-', '') }
                finally { $sha.Dispose() }
                if ((Get-CathedralContentTreeHash $content) -cne $expected) { throw 'Pack hashing changed normalized relative-path order.' }
                $metadata = [ordered]@{ FormatVersion=1; AdapterId='devilution.diablo.cathedral-l1.v1'; ContentDirectory='content';
                    ContentId='godevilution.cathedral-playtest'; ContentVersion='0.1.0'; Entry='main'; ContentManifestHash=('1' * 64);
                    PackFileHash=(Get-CathedralContentTreeHash $content) }
                $metadataPath = Join-Path $work 'cathedral-playtest.json'
                [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json))
                Reject { Get-CathedralPlaytestProfile $work (Join-Path $work 'ordinary-save') } 'separate child directory'
                $profile = Get-CathedralPlaytestProfile $work $session
                if (Test-Path -LiteralPath $session) { throw 'Read-only validation created the session.' }
                [IO.Directory]::CreateDirectory($session) | Out-Null
                $lock = [IO.File]::Open((Join-Path $session 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
                Initialize-CathedralPlaytestSession $profile $session
                $markerPath = Join-Path $session 'cathedral-session.json'
                $marker = [IO.File]::ReadAllText($markerPath)
                Initialize-CathedralPlaytestSession (Get-CathedralPlaytestProfile $work $session) $session
                if ([IO.File]::ReadAllText($markerPath) -cne $marker) { throw 'Resume rewrote profile identity.' }
                [IO.File]::WriteAllText((Join-Path $content 'changed.tsv'), "x`r`n1`r`n")
                Reject { Initialize-CathedralPlaytestSession (Get-CathedralPlaytestProfile $work $session) $session } 'pack hash'
                [IO.File]::Delete((Join-Path $content 'changed.tsv'))
                $metadata.Entry = 'previous'
                [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json))
                Reject { Get-CathedralPlaytestProfile $work $session } 'Unsupported'
                $metadata.Entry = 'main'
                $metadata.DungeonSeed = 99
                [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json))
                Reject { Initialize-CathedralPlaytestSession (Get-CathedralPlaytestProfile $work $session) $session } 'profile changed'
                if ([IO.File]::ReadAllText($markerPath) -cne $marker) { throw 'Rejected profile changes rewrote identity.' }
                $doorCatalogSha = [Security.Cryptography.SHA256]::Create()
                try { $doorCatalogFingerprint = [BitConverter]::ToString($doorCatalogSha.ComputeHash([Text.Encoding]::UTF8.GetBytes("1:1:2`n2:1:2`n3:1:2`n4:1:2"))).Replace('-', '') }
                finally { $doorCatalogSha.Dispose() }
                $doorMetadata = [ordered]@{ FormatVersion=1; AdapterId='devilution.diablo.cathedral-doors.v1'; ContentDirectory='content';
                    ContentId='godevilution.cathedral-doors-playtest'; ContentVersion='0.1.0'; Entry='main';
                    ContentManifestHash=('1' * 64); PackFileHash=(Get-CathedralContentTreeHash $content);
                    DepthSeeds=@(0,1,2,4294967295); PlacementPolicy='none'; NativePopulationClaim=$false;
                    Levels=@(1..4 | ForEach-Object { [ordered]@{ Depth=$_; LevelId=$_; LevelSymbol=if ($_ -eq 1) { 'cathedral' } else { "cathedral_depth_$_" }; Entry='main'; DungeonSeed=@(0,1,2,4294967295)[$_ - 1] } });
                    Routes=@(1001,1002,1003,1004,1099,1100,1101,1102 | ForEach-Object { [ordered]@{ RouteId=$_; RouteSymbol="route$_"; Direction=if ($_ -le 1004) { 'down' } else { 'up' }; SourceLevelId=0; SourceLevelSymbol='town'; DestinationLevelId=1; DestinationLevelSymbol='cathedral' } }); DoorProfileId='original-diablo.cathedral-doors';
                    DoorProfileRevision=1; DoorCatalogFingerprint=$doorCatalogFingerprint;
                    DoorMappings=@(1..4 | ForEach-Object { [ordered]@{ Depth=$_; LevelId=$_; LevelSymbol=if ($_ -eq 1) { 'cathedral' } else { "cathedral_depth_$_" }; LeftObjectId=1; LeftObjectSymbol='l1_left_door'; LeftNativeObjectId=1; RightObjectId=2; RightObjectSymbol='l1_right_door'; RightNativeObjectId=2 } }) }
                $doorMetadataPath = Join-Path $work 'cathedral-doors-playtest.json'
                [IO.File]::WriteAllText($doorMetadataPath, ($doorMetadata | ConvertTo-Json -Depth 8))
                $doorMetadata.DoorProfileRevision = $true
                [IO.File]::WriteAllText($doorMetadataPath, ($doorMetadata | ConvertTo-Json -Depth 8))
                Reject { Get-CathedralDoorPlaytestProfile $work (Join-Path $work 'sessions/doors') } 'complete door catalog identity'
                $doorMetadata.DoorProfileRevision = 1
                $doorMetadata.DoorMappings[0].LeftObjectId = '1'
                [IO.File]::WriteAllText($doorMetadataPath, ($doorMetadata | ConvertTo-Json -Depth 8))
                Reject { Get-CathedralDoorPlaytestProfile $work (Join-Path $work 'sessions/doors') } 'native left/right object IDs 1/2'
                $doorMetadata.DoorMappings[0].LeftObjectId = 1
                [IO.File]::WriteAllText($doorMetadataPath, ($doorMetadata | ConvertTo-Json -Depth 8))
                $doorSession = Join-Path $work 'sessions/doors'
                $doorProfile = Get-CathedralDoorPlaytestProfile $work $doorSession
                if (Test-Path -LiteralPath $doorSession) { throw 'Read-only door validation created the session.' }
                [IO.Directory]::CreateDirectory($doorSession) | Out-Null
                $doorLock = [IO.File]::Open((Join-Path $doorSession 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
                try {
                    Initialize-CathedralDoorPlaytestSession $doorProfile $doorSession
                    $doorMarkerPath = Join-Path $doorSession 'cathedral-door-session.json'
                    $doorMarker = [IO.File]::ReadAllText($doorMarkerPath)
                    $doorMarkerDocument = $doorMarker | ConvertFrom-Json
                    if ($doorMarkerDocument.ProfileKind -cne 'cathedral-doors-playtest' -or
                        $doorMarkerDocument.AdapterId -cne 'devilution.diablo.cathedral-doors.v1') { throw 'Door session marker did not preserve its distinct profile identity.' }
                    Initialize-CathedralDoorPlaytestSession (Get-CathedralDoorPlaytestProfile $work $doorSession) $doorSession
                    if ([IO.File]::ReadAllText($doorMarkerPath) -cne $doorMarker) { throw 'Door resume rewrote profile identity.' }
                    Reject { Initialize-CathedralPlaytestSession $profile $doorSession } 'unmarked existing'
                    $doorMetadata.DoorMappings[0].LeftNativeObjectId = 2
                    [IO.File]::WriteAllText($doorMetadataPath, ($doorMetadata | ConvertTo-Json -Depth 8))
                    Reject { Get-CathedralDoorPlaytestProfile $work $doorSession } 'native left/right object IDs 1/2'
                    if ([IO.File]::ReadAllText($doorMarkerPath) -cne $doorMarker) { throw 'Rejected door metadata rewrote identity.' }
                } finally { $doorLock.Dispose() }
                $old = Join-Path $work 'sessions/old'
                [IO.Directory]::CreateDirectory($old) | Out-Null
                [IO.File]::WriteAllText((Join-Path $old 'resume-token.txt'), 'existing-user-bookmark')
                Reject { Initialize-CathedralPlaytestSession (Get-CathedralPlaytestProfile $work $old) $old } 'unmarked existing'
                Reject { Initialize-CathedralDoorPlaytestSession $doorProfile $old } 'unmarked existing'
                if ([IO.File]::ReadAllText((Join-Path $old 'resume-token.txt')) -cne 'existing-user-bookmark') { throw 'Old bookmark changed.' }
                $depthMetadata = [ordered]@{ FormatVersion=1; AdapterId='devilution.diablo.cathedral-depths.v1'; ContentDirectory='content';
                    ContentId='godevilution.cathedral-depths-playtest'; ContentVersion='0.1.0'; Entry='main';
                    ContentManifestHash=('1' * 64); PackFileHash=(Get-CathedralContentTreeHash $content);
                    DepthSeeds=@(0,1,2,4294967295); PlacementPolicy='none'; NativePopulationClaim=$false;
                    Levels=@(1..4 | ForEach-Object { [ordered]@{ Depth=$_; LevelId=$_; Entry='main';
                        DungeonSeed=@(0,1,2,4294967295)[$_ - 1] } }) }
                $depthPath = Join-Path $work 'cathedral-depths-playtest.json'
                [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                $depthSession = Join-Path $work 'sessions/depths'
                $depthProfile = Get-CathedralPlaytestProfile $work $depthSession $true
                if (Test-Path -LiteralPath $depthSession) { throw 'Read-only depth validation created a session.' }
                [IO.Directory]::CreateDirectory($depthSession) | Out-Null
                Initialize-CathedralPlaytestSession $depthProfile $depthSession
                $depthMarker = [IO.File]::ReadAllText((Join-Path $depthSession 'cathedral-session.json'))
                Reject { Initialize-CathedralPlaytestSession (Get-CathedralPlaytestProfile $work $depthSession) $depthSession } 'profile changed'
                if ([IO.File]::ReadAllText((Join-Path $depthSession 'cathedral-session.json')) -cne $depthMarker) { throw 'L1 mixing rewrote depth identity.' }
                $depthMetadata.Levels[1].Depth = 3
                [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                Reject { Get-CathedralPlaytestProfile $work $depthSession $true } 'ordered depths'
                $depthMetadata.Levels[1].Depth = 2
                $depthMetadata.NativePopulationClaim = $true
                [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                Reject { Get-CathedralPlaytestProfile $work $depthSession $true } 'no native population claim'
                $depthMetadata.NativePopulationClaim = $false
                $depthMetadata.DepthSeeds[1] = 9
                [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                Reject { Get-CathedralPlaytestProfile $work $depthSession $true } 'ordered depths'
                foreach ($invalidSeed in @($null, -1, 4294967296, 0.5, $true, '1')) {
                    $depthMetadata.DepthSeeds = @($invalidSeed, $invalidSeed, $invalidSeed, $invalidSeed)
                    foreach ($level in $depthMetadata.Levels) { $level.DungeonSeed = $invalidSeed }
                    [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                    Reject { Get-CathedralPlaytestProfile $work $depthSession $true } 'unsigned 32-bit integer'
                }
                $depthMetadata.DepthSeeds = @(0, 1, 2, 4294967295)
                foreach ($index in 0..3) { $depthMetadata.Levels[$index].DungeonSeed = $depthMetadata.DepthSeeds[$index] }
                $depthMetadata.Levels[0].Remove('DungeonSeed')
                [IO.File]::WriteAllText($depthPath, ($depthMetadata | ConvertTo-Json -Depth 5))
                Reject { Get-CathedralPlaytestProfile $work $depthSession $true } 'unsigned 32-bit integer'
                if ([IO.File]::ReadAllText((Join-Path $depthSession 'cathedral-session.json')) -cne $depthMarker) { throw 'Invalid seeds rewrote depth identity.' }
                $outside = Join-Path $work 'outside'
                [IO.Directory]::CreateDirectory($outside) | Out-Null
                [IO.File]::WriteAllText((Join-Path $outside 'must-preserve.txt'), 'unrelated')
                New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
                Reject { Get-CathedralPlaytestProfile $work $session } 'reparse point'
                if ([IO.File]::ReadAllText((Join-Path $outside 'must-preserve.txt')) -cne 'unrelated') { throw 'Junction target changed.' }
            } finally {
                if ($null -ne $lock) { $lock.Dispose() }
                if (Test-Path -LiteralPath $junction) { [IO.Directory]::Delete($junction, $false) }
                $absolute = [IO.Path]::GetFullPath($work)
                $temp = [IO.Path]::GetTempPath().TrimEnd('\')
                if (-not $absolute.StartsWith($temp + '\', [StringComparison]::OrdinalIgnoreCase) -or
                    [IO.Path]::GetFileName($absolute) -notmatch '^Godevilution-CathedralLauncherTest-[a-f0-9]{32}$') { throw 'Unsafe test cleanup target.' }
                if ([IO.Directory]::Exists($absolute)) { [IO.Directory]::Delete($absolute, $true) }
            }
            """;
        var scriptPath = Path.Combine(Path.GetTempPath(),
            "Godevilution-CathedralLauncherTestScript-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
        using var child = new Process {
            StartInfo = new ProcessStartInfo("powershell.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        child.StartInfo.ArgumentList.Add("-NoProfile");
        child.StartInfo.ArgumentList.Add("-OutputFormat");
        child.StartInfo.ArgumentList.Add("Text");
        child.StartInfo.ArgumentList.Add("-File");
        child.StartInfo.ArgumentList.Add(scriptPath);
        var started = false;
        try {
            started = child.Start();
            Assert.True(started);
            var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await child.WaitForExitAsync(timeout.Token);
            Assert.True(child.ExitCode == 0, await stdout + "\r\n" + await stderr);
        } finally {
            if (started && !child.HasExited) {
                child.Kill();
                Assert.True(child.WaitForExit(5000), "Owned regression child did not terminate.");
            }
            var absoluteScriptPath = Path.GetFullPath(scriptPath);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(tempRoot, absoluteScriptPath, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("^Godevilution-CathedralLauncherTestScript-[a-f0-9]{32}\\.ps1$",
                Path.GetFileName(absoluteScriptPath));
            if (File.Exists(absoluteScriptPath))
                File.Delete(absoluteScriptPath);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Requires Windows PowerShell.")]
    public async Task DoorProfileArgumentGuardRefusesMixingWithLegacyProfile()
    {
        var root = FindRepositoryRoot();
        var launcher = Path.Combine(root, "godot-client", "run-local-client.ps1");
        using var child = new Process {
            StartInfo = new ProcessStartInfo("powershell.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        child.StartInfo.ArgumentList.Add("-NoProfile");
        child.StartInfo.ArgumentList.Add("-File");
        child.StartInfo.ArgumentList.Add(launcher);
        child.StartInfo.ArgumentList.Add("-CathedralProfileDirectory");
        child.StartInfo.ArgumentList.Add(Path.Combine(root, ".local", "cathedral-playtest"));
        child.StartInfo.ArgumentList.Add("-CathedralDoorProfileDirectory");
        child.StartInfo.ArgumentList.Add(Path.Combine(root, ".local", "cathedral-doors-playtest"));
        Assert.True(child.Start());
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await child.WaitForExitAsync(timeout.Token);
            Assert.NotEqual(0, child.ExitCode);
            var output = await stdout + "\r\n" + await stderr;
            Assert.Contains("Choose exactly one Cathedral profile mode", output, StringComparison.Ordinal);
        } finally {
            if (!child.HasExited) {
                child.Kill();
                Assert.True(child.WaitForExit(5000), "Owned argument-regression child did not terminate.");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

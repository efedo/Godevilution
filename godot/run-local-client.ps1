[CmdletBinding()]
param(
    [string]$GodotExecutable,
    [ValidateRange(0, 65535)] [int]$Port = 0,
    [ValidateRange(-1, 4294967295)] [long]$CharacterClassId = -1,
    [string]$CharacterName,
    [switch]$Headless,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 120,
    [string]$SessionDirectory,
    [ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy',
    [switch]$PreviewContent,
    [switch]$PortalPreviewContent,
    [switch]$LifecyclePreviewContent,
    [switch]$Multiplayer,
    [string]$CathedralProfileDirectory,
    [string]$CathedralDepthProfileDirectory,
    [string]$CathedralDoorProfileDirectory,
    [string]$CathedralNativeProfileDirectory,
    [ValidateSet(1, 4)] [int]$PerformanceParticipants = 1,
    [ValidateSet('Create', 'Resume', 'Recovery', 'Journey', 'JourneyResume', 'TownLoop', 'TownLoopResume', 'Lifecycle', 'LifecycleResume', 'MultiplayerCreate', 'MultiplayerResume', 'MultiplayerRecovery', 'MultiplayerPendingRecovery', 'CooperativeJourney', 'CooperativeResume', 'Performance', 'PerformanceCreate', 'PerformanceResume', 'CathedralJourney', 'CathedralResume', 'CathedralDepthJourney', 'CathedralDepthResume', 'CathedralDoorJourney', 'CathedralDoorResume', 'CathedralNativeJourney', 'CathedralNativeResume', 'CathedralNativeChestJourney', 'CathedralNativeChestResume', 'CathedralNativeSarcophagusJourney', 'CathedralNativeSarcophagusResume', 'CathedralNativeBarrelJourney', 'CathedralNativeBarrelResume', 'CathedralNativeTrapJourney', 'CathedralNativeTrapResume', 'CathedralNativeProgressionJourney', 'CathedralNativeProgressionResume', 'CathedralNativeDepthProgressionJourney', 'CathedralNativeDepthProgressionResume')] [string]$AcceptancePhase,
    [switch]$CommandTimingTrace,
    [switch]$Automated,
    [string]$PerformanceResumeBaselinePath,
    [string]$CathedralNativeProgressionProfileDirectory,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug',
    [string]$CathedralNativeDepthProgressionProfileDirectory,
    [string]$NativeDepthEntryBaselinePath
)

$ErrorActionPreference = 'Stop'
if ($Automated -and [string]::IsNullOrWhiteSpace($SessionDirectory)) {
    throw 'Automated launches require an explicit isolated -SessionDirectory.'
}
if ($CommandTimingTrace -and [string]::IsNullOrWhiteSpace($AcceptancePhase)) {
    throw 'Command timing traces require an explicit isolated acceptance phase.'
}
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
$cathedralProgression = -not [string]::IsNullOrWhiteSpace($CathedralNativeProgressionProfileDirectory)
$cathedralDepthProgression = -not [string]::IsNullOrWhiteSpace($CathedralNativeDepthProgressionProfileDirectory)
if (($cathedralProgression -and $cathedralDepthProgression) -or
    (($cathedralProgression -or $cathedralDepthProgression) -and -not [string]::IsNullOrWhiteSpace($CathedralNativeProfileDirectory))) {
    throw 'Choose one explicit native Cathedral profile.'
}
$cathedralNative = $cathedralProgression -or $cathedralDepthProgression -or
    -not [string]::IsNullOrWhiteSpace($CathedralNativeProfileDirectory)
$nativeDepthEntryContinuation = -not [string]::IsNullOrWhiteSpace($NativeDepthEntryBaselinePath)
if ($nativeDepthEntryContinuation) {
    if ($AcceptancePhase -cne 'CathedralNativeDepthProgressionResume' -or -not $cathedralDepthProgression -or
        $PersistenceMode -cne 'cohort' -or -not $Automated -or -not $Headless -or
        -not [IO.Path]::IsPathFullyQualified($NativeDepthEntryBaselinePath)) {
        throw 'A native depth entry baseline requires an automated headless cohort Resume on the explicit four-floor profile.'
    }
    if (-not (Test-Path -LiteralPath $NativeDepthEntryBaselinePath -PathType Leaf)) {
        throw 'The native depth entry baseline file does not exist.'
    }
    $NativeDepthEntryBaselinePath = (Resolve-Path -LiteralPath $NativeDepthEntryBaselinePath -ErrorAction Stop).ProviderPath
}
$performanceCreate = $AcceptancePhase -eq 'PerformanceCreate'
$performanceResume = $AcceptancePhase -eq 'PerformanceResume'
$performance = $AcceptancePhase -in @('Performance', 'PerformanceCreate')
if ($AcceptancePhase -like 'Performance*' -and $ServerBuildConfiguration -ne 'Debug') {
    throw 'Performance acceptance requires -ServerBuildConfiguration Debug.'
}
if ($performanceCreate -or $performanceResume) {
    if ($PerformanceParticipants -ne 1 -or $Multiplayer -or $CommandTimingTrace -or $PersistenceMode -cne 'legacy' -or
        $PreviewContent -or $PortalPreviewContent -or $LifecyclePreviewContent -or
        $CharacterClassId -ge 0 -or -not [string]::IsNullOrWhiteSpace($CharacterName)) {
        throw 'PerformanceCreate and PerformanceResume require one Legacy single-player participant in slot one, base content, and no command timing trace.'
    }
}
if ($performanceResume) {
    if ([string]::IsNullOrWhiteSpace($PerformanceResumeBaselinePath) -or
        -not [IO.Path]::IsPathFullyQualified($PerformanceResumeBaselinePath)) {
        throw 'PerformanceResume requires an explicit absolute PerformanceCreate baseline path.'
    }
    if (-not (Test-Path -LiteralPath $PerformanceResumeBaselinePath -PathType Leaf)) {
        throw 'PerformanceResume requires an existing PerformanceCreate baseline file.'
    }
    $PerformanceResumeBaselinePath = (Resolve-Path -LiteralPath $PerformanceResumeBaselinePath -ErrorAction Stop).ProviderPath
} elseif (-not [string]::IsNullOrWhiteSpace($PerformanceResumeBaselinePath)) {
    throw 'PerformanceResumeBaselinePath is only valid for PerformanceResume.'
}
if ($CommandTimingTrace -and $performance) {
    throw 'Command timing diagnostics cannot be combined with performance measurement.'
}
$multiplayerRecovery = $AcceptancePhase -in @('MultiplayerRecovery', 'MultiplayerPendingRecovery')
$cooperative = $AcceptancePhase -in @('CooperativeJourney', 'CooperativeResume')
$pendingRecovery = $AcceptancePhase -eq 'MultiplayerPendingRecovery'
$lifecyclePhases = @('Lifecycle', 'LifecycleResume')
$lifecycle = [bool]$LifecyclePreviewContent
$lifecyclePhase = $AcceptancePhase -in $lifecyclePhases
if ($lifecyclePhase -and -not $lifecycle) {
    throw 'Lifecycle acceptance requires -LifecyclePreviewContent.'
}
if ($lifecycle -and $AcceptancePhase -and -not $lifecyclePhase) {
    throw 'The lifecycle preview only supports Lifecycle and LifecycleResume acceptance phases.'
}
$cathedralDepths = -not [string]::IsNullOrWhiteSpace($CathedralDepthProfileDirectory)
$cathedralDoors = -not [string]::IsNullOrWhiteSpace($CathedralDoorProfileDirectory)
$cathedralProfileModes = @(
    $cathedralDepths,
    -not [string]::IsNullOrWhiteSpace($CathedralProfileDirectory),
    $cathedralDoors,
    $cathedralNative) | Where-Object { $_ }
if ($cathedralProfileModes.Count -gt 1) {
    throw 'Choose exactly one Cathedral profile mode: L1, depths 1-4, mutable doors, or one pinned native population profile.'
}
$cathedral = $cathedralDepths -or -not [string]::IsNullOrWhiteSpace($CathedralProfileDirectory)
if (($cathedral -or $cathedralDoors -or $cathedralNative) -and ($PreviewContent -or $PortalPreviewContent -or $LifecyclePreviewContent -or $Multiplayer -or $performance)) {
    throw 'Cathedral playtests require their own single-player content and save profile.'
}
if (($AcceptancePhase -in @('CathedralJourney', 'CathedralResume')) -and (-not $cathedral -or $cathedralDepths)) {
    throw 'Cathedral acceptance requires an explicit compiled -CathedralProfileDirectory.'
}
if (($AcceptancePhase -in @('CathedralDepthJourney', 'CathedralDepthResume')) -and -not $cathedralDepths) {
    throw 'Cathedral depth acceptance requires an explicit compiled -CathedralDepthProfileDirectory.'
}
if (($AcceptancePhase -in @('CathedralDoorJourney', 'CathedralDoorResume')) -and -not $cathedralDoors) {
    throw 'Cathedral door acceptance requires an explicit compiled -CathedralDoorProfileDirectory.'
}
$cathedralNativePhases = @('CathedralNativeJourney', 'CathedralNativeResume', 'CathedralNativeChestJourney', 'CathedralNativeChestResume', 'CathedralNativeSarcophagusJourney', 'CathedralNativeSarcophagusResume', 'CathedralNativeBarrelJourney', 'CathedralNativeBarrelResume', 'CathedralNativeTrapJourney', 'CathedralNativeTrapResume')
if ($AcceptancePhase -in @('CathedralNativeProgressionJourney', 'CathedralNativeProgressionResume') -and -not $cathedralProgression) {
    throw 'Native progression acceptance requires its explicit -CathedralNativeProgressionProfileDirectory.'
}
if ($AcceptancePhase -in @('CathedralNativeDepthProgressionJourney', 'CathedralNativeDepthProgressionResume') -and -not $cathedralDepthProgression) {
    throw 'Native depth progression acceptance requires its explicit -CathedralNativeDepthProgressionProfileDirectory.'
}
if ($cathedralProgression) {
    $cathedralNativePhases = @('CathedralNativeProgressionJourney', 'CathedralNativeProgressionResume')
}
if ($cathedralDepthProgression) {
    $cathedralNativePhases = @('CathedralNativeDepthProgressionJourney', 'CathedralNativeDepthProgressionResume')
}
$cathedralNativePhase = $AcceptancePhase -in $cathedralNativePhases
if ($cathedralNative -and (($AcceptancePhase -and -not $cathedralNativePhase) -or ($CommandTimingTrace -and -not ($cathedralProgression -or $cathedralDepthProgression)) -or
    $PersistenceMode -cne 'cohort' -or ($CharacterClassId -ge 0 -and $CharacterClassId -ne 1))) {
    throw 'The native Cathedral profile requires its own single-player Rogue cohort session.'
}
if ($cathedralNativePhase -and -not $cathedralNative) {
    throw 'Native Cathedral acceptance requires the pinned -CathedralNativeProfileDirectory.'
}
if ($cathedralNativePhase -and (-not $Headless -or $PersistenceMode -cne 'cohort' -or
    [string]::IsNullOrWhiteSpace($SessionDirectory) -or ($CommandTimingTrace -and -not ($cathedralProgression -or $cathedralDepthProgression)) -or $Multiplayer -or
    ($CharacterClassId -ge 0 -and $CharacterClassId -ne 1) -or
    -not [string]::IsNullOrWhiteSpace($CharacterName))) {
    throw 'Native Cathedral journey/resume requires a headless, isolated single-player Rogue cohort session.'
}
$cathedralPhases = if ($cathedralDepths) { @('CathedralDepthJourney', 'CathedralDepthResume') } else { @('CathedralJourney', 'CathedralResume') }
if ($cathedralDoors) { $cathedralPhases = @('CathedralDoorJourney', 'CathedralDoorResume') }
if ($cathedralNative -and $AcceptancePhase -and -not $cathedralNativePhase) {
    throw 'The pinned native Cathedral profile only supports its population and chest journey/resume phases.'
}
if (($cathedral -or $cathedralDoors) -and $AcceptancePhase -and $AcceptancePhase -notin $cathedralPhases) {
    throw 'Compiled Cathedral content only supports its own acceptance phases.'
}
if ($performance) {
    if ($PreviewContent -or $PortalPreviewContent -or $LifecyclePreviewContent -or ($Multiplayer -ne ($PerformanceParticipants -eq 4))) {
        throw 'Performance uses base content and requires -Multiplayer only for four participants.'
    }
} elseif ($PerformanceParticipants -ne 1) {
    throw 'PerformanceParticipants is only valid for Performance acceptance.'
}
$clientCount = if ($performance) { $PerformanceParticipants } elseif ($Multiplayer) { 2 } else { 1 }
if (($PreviewContent -and $PortalPreviewContent) -or (($PreviewContent -or $PortalPreviewContent) -and $LifecyclePreviewContent)) {
    throw 'Choose one preview profile.'
}
if ($LifecyclePreviewContent -and ($Multiplayer -or $performance)) {
    throw 'The lifecycle preview requires its own single-player content and save profile.'
}
if ($AcceptancePhase -in @('TownLoop', 'TownLoopResume') -and -not ($PreviewContent -or $PortalPreviewContent)) {
    throw 'Town-loop acceptance requires -PreviewContent or -PortalPreviewContent and its isolated content identity.'
}
if ($cooperative -and -not $PreviewContent) {
    throw 'Cooperative acceptance requires the town-loop-preview content overlay.'
}
if ($cooperative -and $PersistenceMode -cne 'cohort') {
    throw 'Cooperative acceptance requires the explicit complete-cohort persistence mode.'
}
if ($cooperative -and -not $Multiplayer) {
    throw 'Cooperative acceptance requires -Multiplayer so two independent clients are launched.'
}
if ($Multiplayer -and ($PreviewContent -or $PortalPreviewContent) -and -not $cooperative) { throw 'The local multiplayer profile uses base content, not the single-player preview.' }
if ($AcceptancePhase -and -not $performance -and ($Multiplayer -ne ($AcceptancePhase -in @('MultiplayerCreate', 'MultiplayerResume', 'MultiplayerRecovery', 'MultiplayerPendingRecovery', 'CooperativeJourney', 'CooperativeResume')))) {
    throw 'Multiplayer acceptance phases require -Multiplayer; single-player phases cannot use it.'
}
if ($multiplayerRecovery -and $PersistenceMode -cne 'cohort') {
    throw 'MultiplayerRecovery requires the explicit complete-cohort persistence mode.'
}
$expectedGameMode = if ($Multiplayer) { 'Multiplayer' } else { 'SinglePlayer' }
$serverGameMode = if ($Multiplayer) { 'multiplayer' } else { 'single-player' }
if ($PersistenceMode -eq 'cohort' -and [string]::IsNullOrWhiteSpace($SessionDirectory)) {
    throw 'Cohort persistence requires an explicit nonempty -SessionDirectory; it never adopts a legacy default profile.'
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot "server/src/Devilution.Server/bin/$ServerBuildConfiguration/net10.0/Devilution.Server.exe"
$clientSourceProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
$clientProject = $clientSourceProject
if ([string]::IsNullOrWhiteSpace($SessionDirectory)) {
    $SessionDirectory = if ($cathedral -or $cathedralDoors -or $cathedralNative) {
        $profileDirectory = if ($cathedralDepths) { $CathedralDepthProfileDirectory } elseif ($cathedralDoors) { $CathedralDoorProfileDirectory } elseif ($cathedralDepthProgression) { $CathedralNativeDepthProgressionProfileDirectory } elseif ($cathedralProgression) { $CathedralNativeProgressionProfileDirectory } elseif ($cathedralNative) { $CathedralNativeProfileDirectory } else { $CathedralProfileDirectory }
        Join-Path ([IO.Path]::GetFullPath($profileDirectory)) 'sessions/local'
    } else {
        Join-Path $repositoryRoot $(if ($Multiplayer) { '.local/godot-multiplayer' } elseif ($lifecycle) { '.local/godot-lifecycle-preview' } elseif ($PortalPreviewContent) { '.local/godot-portal-preview' } elseif ($PreviewContent) { '.local/godot-preview' } else { '.local/godot' })
    }
}
$SessionDirectory = [System.IO.Path]::GetFullPath($SessionDirectory)
if ($SessionDirectory.Contains('"')) { throw 'The session directory must not contain a quote.' }
$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Godot-' + [guid]::NewGuid().ToString('N'))
$godotLogPath = Join-Path $captureRoot 'godot.log'
New-Item -ItemType Directory -Path $captureRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe'
    $GodotExecutable = @(
        (Get-Command 'Godot_v*-stable_mono_win64.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application -ErrorAction SilentlyContinue).Source
        if (Test-Path -LiteralPath $wingetRoot) {
            (Get-ChildItem -LiteralPath $wingetRoot -Filter 'Godot_v*-stable_mono_win64*.exe' -File -Recurse -ErrorAction SilentlyContinue).FullName
        }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($GodotExecutable) -or -not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw 'A Godot Mono executable was not found on PATH or in WinGet. Pass -GodotExecutable to this inner harness.'
}
function Resolve-DirectGodotExecutable([string]$Path) {
    # Normalize again after selecting the GUI sibling so an explicit relative
    # console path cannot leak a relative executable into ProcessStartInfo.
    $resolvedPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
    $enginePath = $resolvedPath -replace '_console\.exe$', '.exe'
    if (Test-Path -LiteralPath $enginePath -PathType Leaf) {
        $resolvedPath = (Resolve-Path -LiteralPath $enginePath -ErrorAction Stop).ProviderPath
    }
    if ([IO.Path]::GetFileName($resolvedPath) -match '_console\.exe$') {
        throw 'A direct Godot Mono executable is required; the console executable has no concrete GUI sibling.'
    }
    return $resolvedPath
}
$GodotExecutable = Resolve-DirectGodotExecutable $GodotExecutable

$serverProcess = $null
$godotProcess = $null
$startedClients = @()
$importProcess = $null
$importTracked = $null
$versionTracked = $null
$staging = $null
$engineVersion = $null
$projectStageEvidencePath = Join-Path $captureRoot 'project-stage.json'
$launcherError = $null
$sessionLock = $null
$processEvidenceRoot = Join-Path $captureRoot 'processes'
New-Item -ItemType Directory -Path $processEvidenceRoot -Force | Out-Null
$savedEnvironment = @{}
$environmentNames = @('DEVILUTION_SERVER_HOST', 'DEVILUTION_SERVER_PORT', 'DEVILUTION_PROTOCOL_VERSION',
    'DEVILUTION_CONTENT_HASH', 'DEVILUTION_RULESET_HASH', 'DEVILUTION_CHARACTER_CLASS_ID',
    'DEVILUTION_CHARACTER_NAME', 'DEVILUTION_RESUME_TOKEN', 'DEVILUTION_RESUME_TOKEN_PATH',
    'GODEVILUTION_ACCEPTANCE_PHASE', 'GODEVILUTION_ACCEPTANCE_DIRECTORY', 'GODEVILUTION_QUIT_AFTER_SECONDS',
    'GODEVILUTION_PERSISTENCE_MODE', 'GODEVILUTION_COMMAND_TIMING_TRACE',
    'GODEVILUTION_LOCAL_PLAYER', 'GODEVILUTION_PORTAL_PREVIEW', 'GODEVILUTION_LIFECYCLE_PREVIEW', 'GODEVILUTION_CATHEDRAL_PROFILE', 'GODEVILUTION_CATHEDRAL_DEPTH_PROFILE', 'GODEVILUTION_CATHEDRAL_DOOR_PROFILE', 'GODEVILUTION_CATHEDRAL_NATIVE_PROFILE',
    'GODEVILUTION_PERFORMANCE_PARTICIPANTS', 'GODEVILUTION_PERFORMANCE_SHARED_DIRECTORY', 'GODEVILUTION_PERFORMANCE_RESUME_BASELINE_PATH',
    'GODEVILUTION_NATIVE_DEPTH_ENTRY_BASELINE', 'GODEVILUTION_CLIENT_ASSEMBLY_IMAGE_DIRECTORY',
    'GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT', 'GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS',
    'GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS', 'GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES')
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Assert-WritableDirectory([string]$Path, [string]$Purpose) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $probePath = Join-Path $Path ('.godevilution-write-probe-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($probePath, $Purpose)
    } catch {
        throw "Godot $Purpose storage is not writable: $Path. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $probePath) {
            Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Assert-NoCathedralReparsePoint([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while ($candidate) {
        if ((Test-Path -LiteralPath $candidate) -and
            ((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Cathedral profile and session paths must not traverse reparse points.'
        }
        $parent = [IO.Directory]::GetParent($candidate)
        $candidate = if ($null -eq $parent) { $null } else { $parent.FullName }
    }
}

function Get-CathedralContentTreeHash([string]$ContentRoot) {
    $files = [string[]]@(Get-ChildItem -LiteralPath $ContentRoot -Force -Recurse -File -ErrorAction Stop | ForEach-Object {
        $_.FullName.Substring($ContentRoot.Length).TrimStart('\', '/').Replace('\', '/')
    })
    if ($files.Count -eq 0) { throw 'The compiled Cathedral content directory is empty.' }
    [Array]::Sort($files, [StringComparer]::Ordinal)
    $hashInput = [Text.StringBuilder]::new()
    foreach ($relative in $files) {
        $file = Join-Path $ContentRoot $relative
        [void]$hashInput.Append($relative.Length).Append(':').Append($relative).Append(':')
        [void]$hashInput.Append((Get-FileSha256 $file).ToUpperInvariant()).Append("`n")
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($hashInput.ToString()))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-CathedralPlaytestProfile([string]$Directory, [string]$Session, [bool]$Depths = $false) {
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    if ($root.Contains('"') -or $sessionRoot.Contains('"')) { throw 'Cathedral paths must not contain quotes.' }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot
    $allowedSessions = Join-Path $root 'sessions'
    if (-not $sessionRoot.StartsWith($allowedSessions.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cathedral saves must be in a separate child directory under the compiled profile sessions directory.'
    }
    $metadataPath = Join-Path $root $(if ($Depths) { 'cathedral-depths-playtest.json' } else { 'cathedral-playtest.json' })
    Assert-NoCathedralReparsePoint $metadataPath
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    $expectedAdapter = if ($Depths) { 'devilution.diablo.cathedral-depths.v1' } else { 'devilution.diablo.cathedral-l1.v1' }
    $expectedContent = if ($Depths) { 'godevilution.cathedral-depths-playtest' } else { 'godevilution.cathedral-playtest' }
    if ($metadata.FormatVersion -ne 1 -or $metadata.AdapterId -cne $expectedAdapter -or
        $metadata.ContentDirectory -cne 'content' -or $metadata.ContentId -cne $expectedContent -or
        $metadata.ContentVersion -cne '0.1.0' -or (-not $Depths -and $metadata.Entry -cne 'main') -or
        $metadata.ContentManifestHash -notmatch '^[0-9a-fA-F]{64}$' -or $metadata.PackFileHash -notmatch '^[0-9A-F]{64}$') {
        throw 'Unsupported or incomplete Cathedral playtest profile.'
    }
    if ($Depths) {
        if (@($metadata.Levels).Count -ne 4 -or @($metadata.DepthSeeds).Count -ne 4 -or
            $metadata.Entry -cne 'main' -or $metadata.NativePopulationClaim -isnot [bool] -or $metadata.NativePopulationClaim -or
            $metadata.PlacementPolicy -cnotin @('none', 'authored-diagnostic')) {
            throw 'Cathedral depth profiles require four explicit seeds and no native population claim.'
        }
        for ($index = 0; $index -lt 4; $index++) {
            $level = $metadata.Levels[$index]
            $seed = $metadata.DepthSeeds[$index]
            # JSON integers deserialize as Int32/Int64; coercion would accept null, strings or booleans.
            if (($seed -isnot [int] -and $seed -isnot [long]) -or $seed -lt 0 -or $seed -gt [uint32]::MaxValue -or
                ($level.DungeonSeed -isnot [int] -and $level.DungeonSeed -isnot [long]) -or
                $level.DungeonSeed -lt 0 -or $level.DungeonSeed -gt [uint32]::MaxValue) {
                throw 'Cathedral depth seeds must be unsigned 32-bit integer JSON numbers.'
            }
            if ($level.Depth -ne ($index + 1) -or $level.LevelId -ne ($index + 1) -or $level.Entry -cne 'main' -or
                $level.DungeonSeed -ne $seed) {
                throw 'Cathedral depth profiles require ordered depths 1-4 and MAIN entries.'
            }
        }
    }
    $contentRoot = Join-Path $root 'content'
    Assert-NoCathedralReparsePoint $contentRoot
    if (Test-Path -LiteralPath $sessionRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $sessionRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cathedral session storage contains a reparse point.' }
        }
    }
    foreach ($entry in Get-ChildItem -LiteralPath $contentRoot -Force -Recurse -ErrorAction Stop) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Compiled Cathedral content contains a reparse point.' }
    }
    $treeHash = Get-CathedralContentTreeHash $contentRoot
    if ($treeHash -cne $metadata.PackFileHash) { throw 'The compiled Cathedral pack hash does not match its published profile.' }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ContentRoot = $contentRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = $treeHash
    }
}

function Get-CathedralDoorPlaytestProfile([string]$Directory, [string]$Session) {
    # Door profiles deliberately use a separate metadata identity and session
    # marker so ordinary L1/depth saves can never adopt mutable-door content.
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    if ($root.Contains('"') -or $sessionRoot.Contains('"')) { throw 'Cathedral door paths must not contain quotes.' }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot
    $allowedSessions = Join-Path $root 'sessions'
    if (-not $sessionRoot.StartsWith($allowedSessions.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cathedral door saves must be in a separate child directory under the compiled profile sessions directory.'
    }
    $metadataPath = Join-Path $root 'cathedral-doors-playtest.json'
    Assert-NoCathedralReparsePoint $metadataPath
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    if (($metadata.FormatVersion -isnot [int] -and $metadata.FormatVersion -isnot [long]) -or $metadata.FormatVersion -ne 1 -or $metadata.AdapterId -cne 'devilution.diablo.cathedral-doors.v1' -or
        $metadata.ContentDirectory -cne 'content' -or $metadata.ContentId -cne 'godevilution.cathedral-doors-playtest' -or
        $metadata.ContentVersion -cne '0.1.0' -or $metadata.Entry -cne 'main' -or
        $metadata.ContentManifestHash -notmatch '^[0-9a-fA-F]{64}$' -or $metadata.PackFileHash -notmatch '^[0-9A-F]{64}$') {
        throw 'Unsupported or incomplete Cathedral door playtest profile.'
    }
    if (@($metadata.DepthSeeds).Count -ne 4 -or @($metadata.Levels).Count -ne 4 -or
        $metadata.PlacementPolicy -cnotin @('none', 'authored-diagnostic') -or
        $metadata.NativePopulationClaim -isnot [bool] -or $metadata.NativePopulationClaim -or
        $metadata.DoorProfileId -cne 'original-diablo.cathedral-doors' -or
        ($metadata.DoorProfileRevision -isnot [int] -and $metadata.DoorProfileRevision -isnot [long]) -or $metadata.DoorProfileRevision -ne 1 -or
        $metadata.DoorCatalogFingerprint -notmatch '^[0-9a-fA-F]{64}$' -or
        @($metadata.DoorMappings).Count -ne 4 -or @($metadata.Routes).Count -ne 8) {
        throw 'Cathedral door profiles require four ordered depths, the no-population policy, and a complete door catalog identity.'
    }
    for ($index = 0; $index -lt 4; $index++) {
        $seed = $metadata.DepthSeeds[$index]
        $level = $metadata.Levels[$index]
        $levelSymbol = @('cathedral', 'cathedral_depth_2', 'cathedral_depth_3', 'cathedral_depth_4')[$index]
        if (($seed -isnot [int] -and $seed -isnot [long]) -or $seed -lt 0 -or $seed -gt [uint32]::MaxValue -or
            ($level.Depth -isnot [int] -and $level.Depth -isnot [long]) -or ($level.LevelId -isnot [int] -and $level.LevelId -isnot [long]) -or
            $level.Depth -ne ($index + 1) -or $level.LevelId -ne ($index + 1) -or $level.LevelSymbol -cne $levelSymbol -or
            $level.Entry -cne 'main' -or ($level.DungeonSeed -isnot [int] -and $level.DungeonSeed -isnot [long]) -or
            $level.DungeonSeed -lt 0 -or $level.DungeonSeed -gt [uint32]::MaxValue -or
            $level.DungeonSeed -ne $seed) {
            throw 'Cathedral door profile depths and seeds must be ordered unsigned 32-bit integer JSON values.'
        }
        $mapping = $metadata.DoorMappings[$index]
        if (($mapping.Depth -isnot [int] -and $mapping.Depth -isnot [long]) -or ($mapping.LevelId -isnot [int] -and $mapping.LevelId -isnot [long]) -or
            ($mapping.LeftObjectId -isnot [int] -and $mapping.LeftObjectId -isnot [long]) -or ($mapping.RightObjectId -isnot [int] -and $mapping.RightObjectId -isnot [long]) -or
            ($mapping.LeftNativeObjectId -isnot [int] -and $mapping.LeftNativeObjectId -isnot [long]) -or ($mapping.RightNativeObjectId -isnot [int] -and $mapping.RightNativeObjectId -isnot [long]) -or
            $mapping.Depth -ne ($index + 1) -or $mapping.LevelId -ne ($index + 1) -or
            $mapping.LevelSymbol -cne $levelSymbol -or $mapping.LeftObjectId -ne 1 -or $mapping.RightObjectId -ne 2 -or
            $mapping.LeftNativeObjectId -ne 1 -or $mapping.RightNativeObjectId -ne 2 -or
            $mapping.LeftObjectSymbol -cne 'l1_left_door' -or $mapping.RightObjectSymbol -cne 'l1_right_door') {
            throw 'Cathedral door profile mappings must contain ordered depths and native left/right object IDs 1/2.'
        }
    }
    $catalogCanonical = [string]::Join("`n", @($metadata.DoorMappings | ForEach-Object {
        '{0}:{1}:{2}' -f $_.LevelId, $_.LeftObjectId, $_.RightObjectId
    }))
    $catalogSha = [Security.Cryptography.SHA256]::Create()
    try { $expectedCatalogFingerprint = [BitConverter]::ToString($catalogSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($catalogCanonical))).Replace('-', '') }
    finally { $catalogSha.Dispose() }
    if ($metadata.DoorCatalogFingerprint -cne $expectedCatalogFingerprint) {
        throw 'Cathedral door profile catalog fingerprint does not match its ordered mappings.'
    }
    $routeIds = [Collections.Generic.HashSet[long]]::new()
    foreach ($route in @($metadata.Routes)) {
        if (($route.RouteId -isnot [int] -and $route.RouteId -isnot [long]) -or $route.RouteId -lt 1 -or
            ($route.SourceLevelId -isnot [int] -and $route.SourceLevelId -isnot [long]) -or
            ($route.DestinationLevelId -isnot [int] -and $route.DestinationLevelId -isnot [long]) -or
            $route.SourceLevelId -lt 0 -or $route.DestinationLevelId -lt 0 -or
            [string]::IsNullOrWhiteSpace($route.RouteSymbol) -or
            $route.Direction -cnotin @('down', 'up') -or
            [string]::IsNullOrWhiteSpace($route.SourceLevelSymbol) -or
            [string]::IsNullOrWhiteSpace($route.DestinationLevelSymbol) -or
            -not $routeIds.Add([long]$route.RouteId)) {
            throw 'Cathedral door profile routes must contain eight unique typed route identities.'
        }
    }
    $contentRoot = Join-Path $root 'content'
    Assert-NoCathedralReparsePoint $contentRoot
    if (Test-Path -LiteralPath $sessionRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $sessionRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cathedral door session storage contains a reparse point.' }
        }
    }
    foreach ($entry in Get-ChildItem -LiteralPath $contentRoot -Force -Recurse -ErrorAction Stop) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Compiled Cathedral door content contains a reparse point.' }
    }
    $treeHash = Get-CathedralContentTreeHash $contentRoot
    if ($treeHash -cne $metadata.PackFileHash) { throw 'The compiled Cathedral door pack hash does not match its published profile.' }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ContentRoot = $contentRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = $treeHash
    }
}

function Initialize-CathedralDoorPlaytestSession($Profile, [string]$Session) {
    # Called only while the ordinary exclusive launcher lock is held.
    $markerPath = Join-Path $Session 'cathedral-door-session.json'
    if (Test-Path -LiteralPath $markerPath) {
        $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        if (($marker.FormatVersion -isnot [int] -and $marker.FormatVersion -isnot [long]) -or $marker.FormatVersion -ne 1 -or $marker.ProfileKind -cne 'cathedral-doors-playtest' -or
            $marker.AdapterId -cne 'devilution.diablo.cathedral-doors.v1' -or
            $marker.MetadataHash -cne $Profile.MetadataHash -or $marker.ContentTreeHash -cne $Profile.ContentTreeHash) {
            throw 'Cathedral door profile changed; use a fresh session instead of mixing existing saves.'
        }
    } else {
        if (Get-ChildItem -LiteralPath $Session -Force | Where-Object Name -ne 'launcher.lock') {
            throw 'An unmarked existing save profile must never become a Cathedral door session.'
        }
        $stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write(([ordered]@{ FormatVersion = 1; ProfileKind = 'cathedral-doors-playtest'; AdapterId = 'devilution.diablo.cathedral-doors.v1'; MetadataHash = $Profile.MetadataHash; ContentTreeHash = $Profile.ContentTreeHash } | ConvertTo-Json))
        } finally { $writer.Dispose() }
    }
}

function Get-CathedralNativePopulationProfile([string]$Directory, [string]$Session) {
    # This selector is intentionally pinned to the reviewed offline profile artifact.
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local/native-item-route-integrated-20261013/native-profile-r3')).TrimEnd('\', '/')
    if ($root.Contains('"') -or $sessionRoot.Contains('"') -or
        -not [string]::Equals($root, $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The native Cathedral selector accepts only the pinned native-profile-r3 artifact.'
    }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot
    $allowedSessions = Join-Path $root 'sessions'
    if (-not $sessionRoot.StartsWith($allowedSessions.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Native Cathedral saves must be in a separate child directory under the pinned profile sessions directory.'
    }
    $metadataPath = Join-Path $root 'cathedral-native-population-playtest.json'
    Assert-NoCathedralReparsePoint $metadataPath
    if ((Get-FileSha256 $metadataPath).ToUpperInvariant() -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1') {
        throw 'The native Cathedral profile metadata does not match the reviewed publication.'
    }
    $allowedRootEntries = @('cathedral-native-population-playtest.json', 'content', 'native_cathedral_population', 'native_cathedral_runtime', 'sessions')
    foreach ($entry in Get-ChildItem -LiteralPath $root -Force -ErrorAction Stop) {
        if ($entry.Name -notin $allowedRootEntries -or $entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The pinned native Cathedral profile root contains an unexpected entry.'
        }
    }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    $expectedFields = @(
        'formatVersion', 'adapterId', 'contentDirectory', 'contentId', 'contentVersion', 'contentManifestHash', 'packFileHash',
        'depthSeeds', 'entry', 'placementPolicy', 'nativePopulation', 'nativePopulationClaim', 'populationDirectory',
        'populationPackId', 'populationPackVersion', 'populationPackFileHash', 'runtimeDirectory', 'runtimePackId',
        'runtimePackVersion', 'runtimePackFileHash', 'populationMetadataFingerprint', 'populationMetadataManifestHash',
        'monsterRuntimeFingerprint', 'inventoryFootprintCanonicalSha256', 'itemSemanticsCanonicalSha256', 'semanticItemCount',
        'populationPolicy', 'nativeDepth', 'dungeonSeed', 'storySeed', 'heroClassSymbol', 'heroBaseLifeMaximum',
        'heroBaseManaMaximum', 'heroLightRadius', 'heroIdleFrames', 'preEntryFutureX', 'preEntryFutureY',
        'preEntryLevelChanging', 'preEntryLevelLoading', 'levels', 'routes', 'doorProfileId', 'doorProfileRevision',
        'doorCatalogFingerprint', 'doorMappings')
    $actualFields = @($metadata.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actualFields.Count -ne $expectedFields.Count -or
        [string]::Join("`n", $actualFields) -cne [string]::Join("`n", $expectedFields)) {
        throw 'The native Cathedral profile metadata fields do not match the closed reviewed schema.'
    }
    if (($metadata.formatVersion -isnot [int] -and $metadata.formatVersion -isnot [long]) -or $metadata.formatVersion -ne 1 -or
        $metadata.adapterId -cne 'devilution.diablo.cathedral-native-population.v1' -or
        $metadata.contentDirectory -cne 'content' -or $metadata.contentId -cne 'godevilution.cathedral-doors-playtest' -or
        $metadata.contentVersion -cne '0.1.0' -or $metadata.contentManifestHash -cne 'f669a75956485892092e4d0ad241903d21975629fff886b55881d8660caecffb' -or
        $metadata.packFileHash -cne '55E6931A6860EE0BBCA81E5F502EC6CE76A76E3758F2EDDF5EAC110F31147078' -or
        $metadata.nativePopulation -isnot [bool] -or -not $metadata.nativePopulation -or
        $metadata.nativePopulationClaim -isnot [bool] -or $metadata.nativePopulationClaim -or
        $metadata.placementPolicy -cne 'none' -or $metadata.entry -cne 'main' -or
        $metadata.populationDirectory -cne 'native_cathedral_population' -or
        $metadata.populationPackId -cne 'godevilution.cathedral-native-population-metadata' -or
        $metadata.populationPackVersion -cne '1.0.0' -or
        $metadata.populationPackFileHash -cne '64B4C2542DEC7952F5FEDF4B49D80C788C3E1B4891F064BCBF9F0384E435956D' -or
        $metadata.runtimeDirectory -cne 'native_cathedral_runtime' -or
        $metadata.runtimePackId -cne 'godevilution.cathedral-native-runtime' -or
        $metadata.runtimePackVersion -cne '1.0.0' -or
        $metadata.runtimePackFileHash -cne 'ECF1AA82AA9BF70A19230112E57C1C69D1A40BA0EAE4AD057EA4BF9C6E9066F2' -or
        $metadata.populationMetadataFingerprint -cne '53DBE0F0F771533DCBC42265CD878498680A3D502BD812A10B789628DF3219A2' -or
        $metadata.populationMetadataManifestHash -cne '27fa9d623c00cc7ed24f030b687fd58484d4a95aea1b387df99b522ee3891c08' -or
        $metadata.monsterRuntimeFingerprint -cne 'BBE0451DE5794803A73C32300157D345B75B516D0C3BAD3F61A53CC51B6E30B9' -or
        $metadata.inventoryFootprintCanonicalSha256 -cne '3B0F0D99908D39AA592DC9B21B3C68BB9D42520CD7530FD4AD3FD9BB92B3B1CD' -or
        $metadata.itemSemanticsCanonicalSha256 -cne '45A6194CA54BBB16D905B101DB495E08093D3746963B29535E1D8BC38098A2EB' -or
        ($metadata.semanticItemCount -isnot [int] -and $metadata.semanticItemCount -isnot [long]) -or $metadata.semanticItemCount -ne 110 -or
        $metadata.populationPolicy -cne 'original-diablo-single-player-normal-rogue-l1-main' -or
        $metadata.nativeDepth -ne 1 -or $metadata.dungeonSeed -ne 2588 -or $metadata.storySeed -ne 324508639 -or
        $metadata.heroClassSymbol -cne 'rogue' -or $metadata.heroBaseLifeMaximum -ne 2880 -or $metadata.heroBaseManaMaximum -ne 1440 -or
        $metadata.heroLightRadius -ne 8 -or $metadata.heroIdleFrames -ne 8 -or
        $metadata.preEntryFutureX -ne 57 -or $metadata.preEntryFutureY -ne 40 -or
        $metadata.preEntryLevelChanging -isnot [bool] -or -not $metadata.preEntryLevelChanging -or
        $metadata.preEntryLevelLoading -isnot [bool] -or -not $metadata.preEntryLevelLoading -or
        @($metadata.depthSeeds).Count -ne 4 -or @($metadata.levels).Count -ne 4 -or @($metadata.routes).Count -ne 8 -or
        $metadata.doorProfileId -cne 'original-diablo.cathedral-doors' -or
        $metadata.doorProfileRevision -ne 1 -or $metadata.doorCatalogFingerprint -cne '09C1F609164A55FCEFBA2722D459F790817B8F4056EF3E57905125F583301E69' -or
        @($metadata.doorMappings).Count -ne 4) {
        throw 'The native Cathedral profile does not match the pinned Rogue L1 population contract.'
    }
    $expectedSeeds = @(2588, 2588, 1, 4294967295)
    $expectedLevelSymbols = @('cathedral', 'cathedral_depth_2', 'cathedral_depth_3', 'cathedral_depth_4')
    for ($index = 0; $index -lt 4; $index++) {
        $level = $metadata.levels[$index]
        $seed = $metadata.depthSeeds[$index]
        if (($seed -isnot [int] -and $seed -isnot [long]) -or $seed -ne $expectedSeeds[$index] -or
            ($level.depth -isnot [int] -and $level.depth -isnot [long]) -or
            ($level.levelId -isnot [int] -and $level.levelId -isnot [long]) -or
            ($level.dungeonSeed -isnot [int] -and $level.dungeonSeed -isnot [long]) -or
            $level.depth -ne ($index + 1) -or $level.levelId -ne ($index + 1) -or
            $level.levelSymbol -cne $expectedLevelSymbols[$index] -or $level.entry -cne 'main' -or
            $level.dungeonSeed -ne $seed) {
            throw 'Native Cathedral depth rows must match the pinned ordered L1-L4 seed table.'
        }
    }
    $contentRoot = Join-Path $root $metadata.contentDirectory
    $populationRoot = Join-Path $root $metadata.populationDirectory
    $runtimeRoot = Join-Path $root $metadata.runtimeDirectory
    foreach ($packRoot in @($contentRoot, $populationRoot, $runtimeRoot)) {
        Assert-NoCathedralReparsePoint $packRoot
        if (-not (Test-Path -LiteralPath $packRoot -PathType Container)) { throw 'A pinned native Cathedral content pack is missing.' }
        foreach ($entry in Get-ChildItem -LiteralPath $packRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Native Cathedral content packs cannot contain reparse points.' }
        }
    }
    foreach ($pack in @(
        @{ Root=$contentRoot; Id=$metadata.contentId; Version=$metadata.contentVersion },
        @{ Root=$populationRoot; Id=$metadata.populationPackId; Version=$metadata.populationPackVersion },
        @{ Root=$runtimeRoot; Id=$metadata.runtimePackId; Version=$metadata.runtimePackVersion })) {
        $manifestPath = Join-Path $pack.Root 'content-pack.json'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
        if (($manifest.formatVersion -isnot [int] -and $manifest.formatVersion -isnot [long]) -or $manifest.formatVersion -ne 1 -or
            $manifest.id -cne $pack.Id -or $manifest.version -cne $pack.Version) {
            throw 'A native Cathedral pack manifest does not match the pinned profile.'
        }
    }
    if ((Get-CathedralContentTreeHash $contentRoot) -cne $metadata.packFileHash -or
        (Get-CathedralContentTreeHash $populationRoot) -cne $metadata.populationPackFileHash -or
        (Get-CathedralContentTreeHash $runtimeRoot) -cne $metadata.runtimePackFileHash) {
        throw 'A native Cathedral content, population, or runtime pack hash changed.'
    }
    if (Test-Path -LiteralPath $sessionRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $sessionRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Native Cathedral session storage cannot contain reparse points.' }
        }
    }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ContentRoot = $contentRoot
        PopulationRoot = $populationRoot; RuntimeRoot = $runtimeRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = (Get-CathedralContentTreeHash $contentRoot)
        PopulationTreeHash = (Get-CathedralContentTreeHash $populationRoot)
        RuntimeTreeHash = (Get-CathedralContentTreeHash $runtimeRoot)
    }
}

function Initialize-CathedralNativePopulationSession($Profile, [string]$Session, [bool]$Progression = $false, [bool]$DepthProgression = $false) {
    # A separate marker prevents native content from adopting any earlier Cathedral save.
    if ($Progression -and $DepthProgression) { throw 'A native session cannot select both progression profile generations.' }
    $markerFile = if ($DepthProgression) { 'cathedral-native-depth-progression-session.json' } elseif ($Progression) { 'cathedral-native-progression-session.json' } else { 'cathedral-native-population-session.json' }
    $profileKind = if ($DepthProgression) { 'cathedral-native-depth-progression-playtest' } elseif ($Progression) { 'cathedral-native-progression-playtest' } else { 'cathedral-native-population-playtest' }
    $adapterId = if ($DepthProgression) { 'devilution.diablo.cathedral-native-depth-progression.v1' } elseif ($Progression) { 'devilution.diablo.cathedral-native-progression.v1' } else { 'devilution.diablo.cathedral-native-population.v1' }
    $markerPath = Join-Path $Session $markerFile
    foreach ($otherMarker in (@('cathedral-session.json', 'cathedral-door-session.json', 'cathedral-native-population-session.json', 'cathedral-native-progression-session.json', 'cathedral-native-depth-progression-session.json') | Where-Object { $_ -ne $markerFile })) {
        if (Test-Path -LiteralPath (Join-Path $Session $otherMarker)) {
            throw 'A save marker from another Cathedral profile cannot be reused by the native population profile.'
        }
    }
    if (Test-Path -LiteralPath $markerPath) {
        $marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
        $markerFields = @($marker.PSObject.Properties | ForEach-Object { $_.Name })
        if ($markerFields.Count -ne 7 -or
            [string]::Join("`n", $markerFields) -cne "FormatVersion`nProfileKind`nAdapterId`nMetadataHash`nContentTreeHash`nPopulationTreeHash`nRuntimeTreeHash" -or
            ($marker.FormatVersion -isnot [int] -and $marker.FormatVersion -isnot [long]) -or $marker.FormatVersion -ne 1 -or
            $marker.ProfileKind -cne $profileKind -or
            $marker.AdapterId -cne $adapterId -or
            $marker.MetadataHash -cne $Profile.MetadataHash -or $marker.ContentTreeHash -cne $Profile.ContentTreeHash -or
            $marker.PopulationTreeHash -cne $Profile.PopulationTreeHash -or $marker.RuntimeTreeHash -cne $Profile.RuntimeTreeHash) {
            throw 'The pinned native Cathedral profile changed; use a fresh session instead of mixing saves.'
        }
    } else {
        if (Get-ChildItem -LiteralPath $Session -Force | Where-Object Name -ne 'launcher.lock') {
            throw 'An unmarked existing save profile must never become a native Cathedral session.'
        }
        $stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write(([ordered]@{
                FormatVersion = 1; ProfileKind = $profileKind
                AdapterId = $adapterId
                MetadataHash = $Profile.MetadataHash; ContentTreeHash = $Profile.ContentTreeHash
                PopulationTreeHash = $Profile.PopulationTreeHash; RuntimeTreeHash = $Profile.RuntimeTreeHash
            } | ConvertTo-Json))
        } finally { $writer.Dispose() }
    }
}

function Initialize-CathedralPlaytestSession($Profile, [string]$Session) {
    # Called only while the ordinary exclusive launcher lock is held.
    $markerPath = Join-Path $Session 'cathedral-session.json'
    if (Test-Path -LiteralPath $markerPath) {
        $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        if ($marker.FormatVersion -ne 1 -or $marker.MetadataHash -cne $Profile.MetadataHash -or
            $marker.ContentTreeHash -cne $Profile.ContentTreeHash) {
            throw 'Cathedral profile changed; use a fresh session instead of mixing existing saves.'
        }
    } else {
        if (Get-ChildItem -LiteralPath $Session -Force | Where-Object Name -ne 'launcher.lock') {
            throw 'An unmarked existing save profile must never become a Cathedral session.'
        }
        $stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write(([ordered]@{ FormatVersion = 1; MetadataHash = $Profile.MetadataHash; ContentTreeHash = $Profile.ContentTreeHash } | ConvertTo-Json))
        } finally { $writer.Dispose() }
    }
}

function Get-PerformanceMetricSummary([double[]]$Values) {
    if ($Values.Count -lt 1) { throw 'Performance metrics require nonempty samples.' }
    foreach ($value in $Values) {
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0) {
            throw 'Performance metrics must be finite and nonnegative.'
        }
    }
    $sorted = @($Values | Sort-Object)
    return [pscustomobject]@{
        Count = $sorted.Count
        MinimumMilliseconds = $sorted[0]
        MeanMilliseconds = ($Values | Measure-Object -Average).Average
        P50Milliseconds = $sorted[[int][Math]::Ceiling($sorted.Count * 0.50) - 1]
        P95Milliseconds = $sorted[[int][Math]::Ceiling($sorted.Count * 0.95) - 1]
        P99Milliseconds = $sorted[[int][Math]::Ceiling($sorted.Count * 0.99) - 1]
        MaximumMilliseconds = $sorted[-1]
    }
}

function Select-PerformancePublicFields($Value, [string[]]$Names) {
    if ($null -eq $Value) { throw 'Performance metadata is missing.' }
    $members = if ($Value -is [Collections.IDictionary]) { @($Value.Keys) } else { @($Value.PSObject.Properties.Name) }
    foreach ($member in $members) {
        if ($member -notin $Names) { throw 'Performance metadata contains an unexpected field.' }
    }
    $projected = [ordered]@{}
    foreach ($name in $Names) { $projected[$name] = $Value.$name }
    return [pscustomobject]$projected
}

function Assert-GodotPerformanceEvidence($Evidence, [int]$Participants, [string]$Mode, [string]$ContentHash, [string]$RulesetHash, [bool]$Headless) {
    if ($null -eq $Evidence -or $Evidence.SchemaVersion -ne '1' -or $Evidence.CaptureState -ne 'stopped' -or
        -not $Evidence.Outcome.Succeeded -or $Evidence.Outcome.Kind -ne 'workload-complete' -or
        -not $Evidence.Quality.IsValid -or -not $Evidence.Quality.OriginalIdentityUnchanged -or
        -not $Evidence.Quality.ParticipantCountMatches -or $Evidence.DroppedSampleCount -ne 0 -or
        $Evidence.NonMonotonicTimestampCount -ne 0 -or $Evidence.InvalidTimestampCount -ne 0 -or
        $Evidence.SampleCount -lt 2 -or $Evidence.SampleCount -gt 4096 -or
        @($Evidence.Samples).Count -ne $Evidence.SampleCount -or
        $Evidence.MeasuredDurationMilliseconds -lt 15000 -or $Evidence.MeasuredDurationMilliseconds -gt 60000 -or
        [double]::IsNaN([double]$Evidence.MeasuredDurationMilliseconds) -or
        $Evidence.WarmupMilliseconds -ne 0 -or $Evidence.MeasurementWindowMilliseconds -ne 60000) {
        throw 'Incomplete, invalid or truncated Godot performance evidence is not a baseline.'
    }
    $identity = $Evidence.ServerIdentity
    $configuration = $Evidence.Configuration
    if ($identity.GameMode -ne $Mode -or $identity.TickRateHz -ne 20 -or
        $identity.ProtocolSchemaVersion -ne '0.2.0' -or $identity.ContentManifestHash -cne $ContentHash -or
        $identity.RulesetIdentityHash -cne $RulesetHash -or $configuration.ParticipantCount -ne $Participants -or
        $configuration.ContentManifestHash -cne $ContentHash -or $configuration.RulesetIdentityHash -cne $RulesetHash -or
        $configuration.ProtocolSchemaVersion -ne '0.2.0' -or $configuration.BuildConfiguration -ne 'Debug' -or
        $configuration.FrameCap -ne 60 -or $configuration.Headless -ne $Headless -or
        $configuration.EngineVersion -notmatch '^4\.7\.' -or $configuration.Viewport -notmatch '^\d+x\d+$' -or
        [string]::IsNullOrWhiteSpace($configuration.Runtime) -or [string]::IsNullOrWhiteSpace($configuration.OperatingSystem) -or
        [string]::IsNullOrWhiteSpace($configuration.Architecture) -or [string]::IsNullOrWhiteSpace($configuration.DisplayServer) -or
        ($Headless -ne ($configuration.DisplayServer -eq 'headless'))) {
        throw 'Godot performance identity/configuration did not match the actual workload.'
    }
    if (-not $Headless -and ([string]::IsNullOrWhiteSpace($configuration.Renderer) -or
        [string]::IsNullOrWhiteSpace($configuration.Device) -or [string]::IsNullOrWhiteSpace($configuration.VsyncMode))) {
        throw 'Rendered Godot performance evidence requires actual renderer/device/pacing metadata.'
    }
    $publicConfigurationFields = @(
        'ClientBuildId', 'ProtocolSchemaVersion', 'ContentManifestHash', 'RulesetIdentityHash',
        'Renderer', 'Device', 'Viewport', 'VsyncEnabled', 'FrameCap', 'Headless', 'EngineVersion',
        'ParticipantCount', 'RenderingDriver', 'DisplayServer', 'OperatingSystem', 'OperatingSystemVersion',
        'OperatingSystemFeatures', 'VsyncMode', 'Runtime', 'RuntimeOperatingSystem', 'Architecture',
        'BuildConfiguration', 'WindowPosition', 'WindowMode', 'WindowCanDraw', 'WindowFocusedAtStart')
    if ($null -ne $configuration.PSObject.Properties['RuntimeAssemblies']) { $publicConfigurationFields += 'RuntimeAssemblies' }
    $publicConfiguration = Select-PerformancePublicFields $configuration $publicConfigurationFields
    $publicIdentity = Select-PerformancePublicFields $identity @(
        'BuildId', 'ProtocolSchemaVersion', 'ContentManifestHash', 'RulesetIdentityHash', 'TickRateHz', 'GameMode')
    $processFields = @('WorkingSetBytes', 'PeakWorkingSetBytes', 'LastGcHeapSizeBytes', 'TotalProcessorTimeMilliseconds')
    $startup = Select-PerformancePublicFields $Evidence.StartupProcess $processFields
    $final = Select-PerformancePublicFields $Evidence.FinalProcess $processFields
    foreach ($process in @($startup, $final)) {
        foreach ($field in $processFields) {
            if ($null -eq $process.$field -or [double]::IsNaN([double]$process.$field) -or
                [double]::IsInfinity([double]$process.$field) -or [double]$process.$field -lt 0) {
                throw 'Godot process observations must be available, finite and nonnegative.'
            }
        }
        if ($process.PeakWorkingSetBytes -lt $process.WorkingSetBytes) { throw 'Godot process working-set observations are inconsistent.' }
    }
    if ($final.TotalProcessorTimeMilliseconds -lt $startup.TotalProcessorTimeMilliseconds) {
        throw 'Godot process CPU counters moved backwards.'
    }
    $callbacks = @()
    $intervals = @()
    for ($index = 0; $index -lt $Evidence.Samples.Count; $index++) {
        $sample = $Evidence.Samples[$index]
        if ($null -eq $sample.ProcessCallbackMilliseconds -or
            (($index -eq 0) -ne ($null -eq $sample.CallbackIntervalMilliseconds))) {
            throw 'Godot callback boundaries are missing or include the pre-measurement gap.'
        }
        $callbacks += [double]$sample.ProcessCallbackMilliseconds
        if ($index -gt 0) {
            if ([double]$sample.CallbackIntervalMilliseconds -le 0) { throw 'Godot callback intervals must be positive.' }
            $intervals += [double]$sample.CallbackIntervalMilliseconds
        }
    }
    return [pscustomobject]@{
        MeasuredDurationMilliseconds = $Evidence.MeasuredDurationMilliseconds
        Configuration = $publicConfiguration
        ServerIdentity = $publicIdentity
        ProcessCallback = Get-PerformanceMetricSummary $callbacks
        CallbackInterval = Get-PerformanceMetricSummary $intervals
        StartupProcess = $startup
        FinalProcess = $final
        ProcessCpuDeltaMilliseconds = $final.TotalProcessorTimeMilliseconds - $startup.TotalProcessorTimeMilliseconds
    }
}

function Get-CanonicalPath([string]$Path) {
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath)
}

function Assert-PathWithinRepository([string]$Path, [string]$Purpose) {
    $root = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $candidate = [IO.Path]::GetFullPath($Path)
    if (-not $candidate.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Purpose must remain inside the repository: $candidate"
    }
    return $candidate
}

function Get-ProjectRelativePath([string]$Root, [string]$Path) {
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) -eq $rootPrefix.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) {
        return ''
    }
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "A project file escaped its source root: $fullPath"
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Assert-NoReparsePoint([IO.FileSystemInfo]$Item, [string]$Purpose) {
    if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Purpose contains a reparse point, which is not permitted: $($Item.FullName)"
    }
}

function Assert-NoReparseAncestry([string]$Path, [string]$Purpose) {
    $current = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    while ($null -ne $current) {
        Assert-NoReparsePoint $current $Purpose
        $current = if ($current -is [IO.FileInfo]) { $current.Directory } else { $current.Parent }
    }
}

function Get-IsolatedProjectFiles([string]$Root) {
    $rootInfo = Get-Item -LiteralPath $Root -Force -ErrorAction Stop
    Assert-NoReparsePoint $rootInfo 'The client source project'
    $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
    $pending.Push([IO.DirectoryInfo]$rootInfo)
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($entry in $directory.EnumerateFileSystemInfos('*', [IO.SearchOption]::TopDirectoryOnly)) {
            Assert-NoReparsePoint $entry 'The client source project'
            if ($entry -is [IO.DirectoryInfo]) {
                if ($entry.Name -in @('.godot', 'bin', 'obj')) { continue }
                $pending.Push([IO.DirectoryInfo]$entry)
                continue
            }
            if ($entry.Name -like '*.csproj.old') { continue }
            $files.Add([IO.FileInfo]$entry)
        }
    }
    return $files
}

function Get-ProjectTreeHash([string]$Root) {
    $manifest = [Text.StringBuilder]::new()
    foreach ($file in (Get-IsolatedProjectFiles $Root | Sort-Object FullName)) {
        $relative = Get-ProjectRelativePath $Root $file.FullName
        $stream = $null
        $fileSha = $null
        try {
            $stream = [IO.File]::OpenRead($file.FullName)
            $fileSha = [Security.Cryptography.SHA256]::Create()
            $hash = ([BitConverter]::ToString($fileSha.ComputeHash($stream)).Replace('-', '')).ToLowerInvariant()
        } finally {
            if ($null -ne $fileSha) { $fileSha.Dispose() }
            if ($null -ne $stream) { $stream.Dispose() }
        }
        [void]$manifest.Append($relative).Append("`0").Append($file.Length).Append("`0").Append($hash).Append("`n")
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($manifest.ToString())
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '')).ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-FileSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')).ToLowerInvariant()
    } finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-GodotCanonicalLocationSha256([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) {
        throw 'A staged assembly must have an absolute path.'
    }
    try { $canonicalPath = [IO.Path]::GetFullPath($Path) }
    catch { throw 'A staged assembly path could not be normalized.' }
    $pathRoot = [IO.Path]::GetPathRoot($canonicalPath)
    $directorySeparator = [IO.Path]::DirectorySeparatorChar.ToString()
    $alternateSeparator = [IO.Path]::AltDirectorySeparatorChar.ToString()
    while ($canonicalPath.Length -gt $pathRoot.Length -and
        ($canonicalPath.EndsWith($directorySeparator, [StringComparison]::Ordinal) -or
            $canonicalPath.EndsWith($alternateSeparator, [StringComparison]::Ordinal))) {
        $canonicalPath = $canonicalPath.Substring(0, $canonicalPath.Length - 1)
    }
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $canonicalPath = $canonicalPath.ToUpperInvariant()
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($canonicalPath)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '')).ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-GodotRuntimeAssemblyModuleVersionId([byte[]]$ImageBytes) {
    if ($null -eq $ImageBytes -or $ImageBytes.Length -eq 0) {
        throw 'A staged managed assembly image is empty.'
    }
    try {
        if ($PSVersionTable.PSEdition -eq 'Desktop') {
            $assembly = [Reflection.Assembly]::ReflectionOnlyLoad($ImageBytes)
            $moduleVersionId = $assembly.ManifestModule.ModuleVersionId
        } elseif ($PSVersionTable.PSEdition -eq 'Core') {
            $imageStream = [IO.MemoryStream]::new($ImageBytes, $false)
            $peReader = $null
            try {
                $peReader = [System.Reflection.PortableExecutable.PEReader]::new($imageStream)
                $metadataReader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
                $moduleDefinition = $metadataReader.GetModuleDefinition()
                $moduleVersionId = $metadataReader.GetGuid($moduleDefinition.Mvid)
            } finally {
                if ($null -ne $peReader) { $peReader.Dispose() }
                $imageStream.Dispose()
            }
        } else {
            throw 'The current PowerShell edition cannot read managed assembly metadata.'
        }
    } catch {
        throw 'A staged assembly MVID could not be read from its image.'
    }
    if ($moduleVersionId -eq [guid]::Empty) { throw 'A staged assembly has an empty MVID.' }
    return $moduleVersionId.ToString('D')
}

function Get-GodotRuntimeAssemblyManifest([string]$ClientProjectDirectory) {
    if ([string]::IsNullOrWhiteSpace($ClientProjectDirectory)) {
        throw 'The staged Godot client project path is missing.'
    }
    try {
        $clientRoot = [IO.Path]::GetFullPath($ClientProjectDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        $runtimeDirectory = [IO.Path]::GetFullPath((Join-Path $clientRoot '.godot/mono/temp/bin/Debug'))
    } catch { throw 'The staged Godot Debug output directory could not be resolved.' }
    $rootPrefix = $clientRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $runtimeDirectory.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $runtimeDirectory -PathType Container)) {
        throw 'The staged Godot Debug output directory is missing or escaped its project.'
    }

    $manifest = @()
    # The independently qualified local Godot 4.7.2 runtime resolves both client roles from memory and binds their staged images by MVID.
    foreach ($assemblyInput in @(
        [pscustomobject]@{ Role = 'client'; FileName = 'Devilution.Client.dll' },
        [pscustomobject]@{ Role = 'client-protocol'; FileName = 'Devilution.Client.Protocol.dll' })) {
        $assemblyPath = Join-Path $runtimeDirectory $assemblyInput.FileName
        if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
            throw 'A required staged Debug client assembly is missing.'
        }
        $stream = $null
        $imageStream = $null
        try {
            $stream = [IO.File]::Open($assemblyPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            $imageStream = New-Object IO.MemoryStream
            $stream.CopyTo($imageStream)
            $imageBytes = $imageStream.ToArray()
            $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
            $moduleVersionId = Get-GodotRuntimeAssemblyModuleVersionId $imageBytes
            $fileHash = [Security.Cryptography.SHA256]::Create()
            try { $fileSha256 = ([BitConverter]::ToString($fileHash.ComputeHash($imageBytes)).Replace('-', '')).ToLowerInvariant() }
            finally { $fileHash.Dispose() }
            if ($null -eq $assemblyName -or [string]::IsNullOrWhiteSpace($assemblyName.Name) -or
                $null -eq $assemblyName.Version -or [string]::IsNullOrWhiteSpace($assemblyName.FullName) -or
                $fileSha256 -notmatch '^[0-9a-f]{64}$') {
                throw 'A staged Debug client assembly has incomplete identity metadata.'
            }
            $manifest += [pscustomobject]@{
                Role = $assemblyInput.Role
                AssemblyName = $assemblyName.Name
                AssemblyVersion = $assemblyName.Version.ToString()
                FullName = $assemblyName.FullName
                ModuleVersionId = $moduleVersionId
                Configuration = 'Debug'
                Sha256 = $fileSha256
                FileName = $assemblyInput.FileName
                CanonicalLocationSha256 = Get-GodotCanonicalLocationSha256 $assemblyPath
                ImageBindingKind = 'StagedImageWithMatchingMvid'
                Path = [IO.Path]::GetFullPath($assemblyPath)
            }
        } catch {
            throw 'A staged Debug client assembly manifest could not be derived.'
        } finally {
            if ($null -ne $imageStream) { $imageStream.Dispose() }
            if ($null -ne $stream) { $stream.Dispose() }
        }
    }
    return $manifest
}

function Assert-GodotRuntimeAssemblyEvidence($Observations, $ExpectedManifest) {
    $observed = @($Observations)
    $expected = @($ExpectedManifest)
    $expectedRoles = @('client', 'client-protocol')
    if ($observed.Count -ne 2 -or $expected.Count -ne 2) {
        throw 'Godot runtime assembly evidence must contain exactly the two expected roles.'
    }
    $observationFields = @(
        'Role', 'AssemblyName', 'AssemblyVersion', 'FullName', 'ModuleVersionId',
        'HasConfigurationAttribute', 'Configuration', 'Sha256', 'FileName',
        'CanonicalLocationSha256', 'HasDebuggableAttribute', 'DebuggableModes',
        'OptimizationsDisabled', 'ImageBindingKind')
    $expectedByRole = @{}
    foreach ($entry in $expected) {
        $expectedFileName = if ($null -ne $entry -and $entry.Role -ceq 'client') { 'Devilution.Client.dll' } else { 'Devilution.Client.Protocol.dll' }
        $expectedMvid = [guid]::Empty
        if ($null -eq $entry -or $entry.Role -cnotin $expectedRoles -or $expectedByRole.ContainsKey([string]$entry.Role) -or
            [string]::IsNullOrWhiteSpace([string]$entry.Path) -or
            -not (Test-Path -LiteralPath $entry.Path -PathType Leaf) -or
            [string]$entry.Configuration -cne 'Debug' -or
            [string]$entry.ImageBindingKind -cne 'StagedImageWithMatchingMvid' -or
            [string]$entry.FileName -cne $expectedFileName -or
            [IO.Path]::GetFileName([string]$entry.Path) -cne $expectedFileName -or
            [string]::IsNullOrWhiteSpace([string]$entry.AssemblyName) -or
            [string]::IsNullOrWhiteSpace([string]$entry.AssemblyVersion) -or
            [string]::IsNullOrWhiteSpace([string]$entry.FullName) -or
            [string]$entry.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or
            [string]$entry.CanonicalLocationSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
            -not [guid]::TryParseExact([string]$entry.ModuleVersionId, 'D', [ref]$expectedMvid) -or
            $expectedMvid -eq [guid]::Empty) {
            throw 'The expected Godot runtime assembly manifest is invalid.'
        }
        $expectedByRole[[string]$entry.Role] = $entry
    }
    foreach ($role in $expectedRoles) {
        if (-not $expectedByRole.ContainsKey($role)) { throw 'The expected Godot runtime assembly manifest is incomplete.' }
    }
    $observedRoles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $validated = @()
    $disableOptimizations = [int][Enum]::Parse([type]'System.Diagnostics.DebuggableAttribute+DebuggingModes', 'DisableOptimizations')
    foreach ($item in $observed) {
        if ($null -eq $item -or $item -isnot [pscustomobject]) {
            throw 'Godot runtime assembly evidence contains an invalid record.'
        }
        $actualFields = @($item.PSObject.Properties | ForEach-Object { $_.Name })
        if ($actualFields.Count -ne $observationFields.Count -or
            @($observationFields | Where-Object { $_ -cnotin $actualFields }).Count -ne 0 -or
            @($actualFields | Where-Object { $_ -cnotin $observationFields }).Count -ne 0) {
            throw 'Godot runtime assembly evidence contains missing or unexpected fields.'
        }
        if ($item.Role -isnot [string] -or $item.Role -cnotin $expectedRoles -or -not $observedRoles.Add($item.Role)) {
            throw 'Godot runtime assembly evidence contains a duplicate or unknown role.'
        }
        $entry = $expectedByRole[$item.Role]
        foreach ($field in @('AssemblyName', 'AssemblyVersion', 'FullName', 'ModuleVersionId', 'Configuration', 'Sha256', 'FileName', 'CanonicalLocationSha256', 'ImageBindingKind')) {
            if ($item.$field -isnot [string] -or [string]::IsNullOrWhiteSpace($item.$field)) {
                throw 'Godot runtime assembly evidence contains an invalid string field.'
            }
        }
        foreach ($field in @('HasConfigurationAttribute', 'HasDebuggableAttribute', 'OptimizationsDisabled')) {
            if ($item.$field -isnot [bool]) { throw 'Godot runtime assembly evidence contains an invalid boolean field.' }
        }
        if ($item.DebuggableModes -isnot [int] -and $item.DebuggableModes -isnot [long]) {
            throw 'Godot runtime assembly DebuggableAttribute flags are not an integer.'
        }
        $debuggableModes = [long]$item.DebuggableModes
        if ($debuggableModes -lt 0 -or $debuggableModes -gt [int]::MaxValue -or
            $item.OptimizationsDisabled -ne (($debuggableModes -band $disableOptimizations) -ne 0)) {
            throw 'Godot runtime assembly DebuggableAttribute flags are inconsistent.'
        }
        $moduleVersionId = [guid]::Empty
        if (-not [guid]::TryParseExact($item.ModuleVersionId, 'D', [ref]$moduleVersionId) -or
            $moduleVersionId -eq [guid]::Empty -or $moduleVersionId.ToString('D') -ine $entry.ModuleVersionId -or
            -not $item.HasConfigurationAttribute -or $item.Configuration -cne 'Debug' -or
            -not $item.HasDebuggableAttribute -or -not $item.OptimizationsDisabled -or
            $item.ImageBindingKind -cne $entry.ImageBindingKind -or
            $item.AssemblyName -cne $entry.AssemblyName -or $item.AssemblyVersion -cne $entry.AssemblyVersion -or
            $item.FullName -cne $entry.FullName -or $item.FileName -cne $entry.FileName -or
            $item.Sha256 -inotmatch '^[0-9a-fA-F]{64}$' -or $item.Sha256 -ine $entry.Sha256 -or
            $item.CanonicalLocationSha256 -inotmatch '^[0-9a-fA-F]{64}$' -or
            $item.CanonicalLocationSha256 -ine $entry.CanonicalLocationSha256) {
            throw 'A loaded Godot runtime assembly does not match its fresh staged Debug image.'
        }
        $validated += [pscustomobject]@{
            Role = $item.Role
            AssemblyName = $item.AssemblyName
            AssemblyVersion = $item.AssemblyVersion
            FullName = $item.FullName
            ModuleVersionId = $item.ModuleVersionId
            HasConfigurationAttribute = $item.HasConfigurationAttribute
            Configuration = $item.Configuration
            Sha256 = $item.Sha256
            FileName = $item.FileName
            CanonicalLocationSha256 = $item.CanonicalLocationSha256
            HasDebuggableAttribute = $item.HasDebuggableAttribute
            DebuggableModes = [int]$item.DebuggableModes
            OptimizationsDisabled = $item.OptimizationsDisabled
            ImageBindingKind = $item.ImageBindingKind
        }
    }
    if ($observedRoles.Count -ne $expectedRoles.Count) { throw 'Godot runtime assembly evidence is missing a required role.' }
    return $validated
}

function Read-ProjectXml([string]$Path) {
    $xml = [Xml.XmlDocument]::new()
    $xml.PreserveWhitespace = $true
    $xml.Load($Path)
    if ($null -eq $xml.DocumentElement -or $xml.DocumentElement.Name -ne 'Project') {
        throw "The staged client project is not an MSBuild Project: $Path"
    }
    if ([string]::IsNullOrWhiteSpace($xml.DocumentElement.GetAttribute('Sdk'))) {
        throw "The client project has no SDK declaration: $Path"
    }
    return $xml
}

function Resolve-ValidatedProjectReference([string]$BaseDirectory, [string]$Include, [string]$Purpose) {
    if ([string]::IsNullOrWhiteSpace($Include) -or $Include.Contains('"') -or $Include.Contains('*') -or $Include.Contains('?')) {
        throw "$Purpose has an invalid project reference: '$Include'"
    }
    try {
        $candidate = if ([IO.Path]::IsPathRooted($Include)) {
            [IO.Path]::GetFullPath($Include)
        } else {
            [IO.Path]::GetFullPath((Join-Path $BaseDirectory $Include))
        }
    } catch {
        throw "$Purpose has an invalid project reference '$Include': $($_.Exception.Message)"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "$Purpose references a missing project: $candidate"
    }
    Assert-NoReparseAncestry $candidate $Purpose
    $item = Get-Item -LiteralPath $candidate -Force
    if ([IO.Path]::GetExtension($candidate) -ine '.csproj') {
        throw "$Purpose references a non-project file: $candidate"
    }
    return [IO.Path]::GetFullPath($item.FullName)
}

function Get-ProjectSignature([Xml.XmlDocument]$Xml) {
    $clone = [Xml.XmlDocument]::new()
    $clone.PreserveWhitespace = $true
    $clone.LoadXml($Xml.OuterXml)
    $project = $clone.DocumentElement
    $project.SetAttribute('Sdk', '__SDK__')
    $referenceIndex = 0
    foreach ($reference in @($project.SelectNodes('.//ProjectReference'))) {
        $reference.SetAttribute('Include', "__REFERENCE_$referenceIndex__")
        $referenceIndex++
    }
    return $clone.OuterXml
}

function Get-ProjectMetadata([string]$ProjectPath, [string]$ReferenceBaseDirectory) {
    $xml = Read-ProjectXml $ProjectPath
    $references = [Collections.Generic.List[string]]::new()
    foreach ($reference in @($xml.DocumentElement.SelectNodes('.//ProjectReference'))) {
        $include = $reference.GetAttribute('Include')
        $references.Add((Resolve-ValidatedProjectReference $ReferenceBaseDirectory $include 'The client project'))
    }
    return [pscustomobject]@{
        Path = [IO.Path]::GetFullPath($ProjectPath)
        Sdk = $xml.DocumentElement.GetAttribute('Sdk')
        References = @($references)
        Signature = Get-ProjectSignature $xml
        FileHash = Get-FileSha256 $ProjectPath
    }
}

function Test-ExactStringSequence([string[]]$Left, [string[]]$Right) {
    if ($Left.Count -ne $Right.Count) { return $false }
    for ($index = 0; $index -lt $Left.Count; $index++) {
        if ($Left[$index] -cne $Right[$index]) { return $false }
    }
    return $true
}

function Set-StagedProjectReferences([string]$ProjectPath, [string[]]$References) {
    $xml = Read-ProjectXml $ProjectPath
    $nodes = @($xml.DocumentElement.SelectNodes('.//ProjectReference'))
    if ($nodes.Count -ne $References.Count) {
        throw "The staged client project changed its ProjectReference count during staging."
    }
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        $nodes[$index].SetAttribute('Include', $References[$index])
    }
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.OmitXmlDeclaration = $true
    $settings.Indent = $false
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [Xml.NewLineHandling]::None
    $writer = $null
    try {
        $writer = [Xml.XmlWriter]::Create($ProjectPath, $settings)
        $xml.Save($writer)
    } finally {
        if ($null -ne $writer) { $writer.Dispose() }
    }
}

function Copy-IsolatedProjectTree([string]$SourceDirectory, [string]$DestinationDirectory) {
    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
    $pending.Push([IO.DirectoryInfo](Get-Item -LiteralPath $SourceDirectory -Force))
    while ($pending.Count -gt 0) {
        $source = $pending.Pop()
        $relativeDirectory = Get-ProjectRelativePath $SourceDirectory $source.FullName
        $destination = if ([string]::IsNullOrEmpty($relativeDirectory)) { $DestinationDirectory } else { Join-Path $DestinationDirectory $relativeDirectory }
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        foreach ($entry in $source.EnumerateFileSystemInfos('*', [IO.SearchOption]::TopDirectoryOnly)) {
            Assert-NoReparsePoint $entry 'The client source project'
            if ($entry -is [IO.DirectoryInfo]) {
                if ($entry.Name -in @('.godot', 'bin', 'obj')) { continue }
                $pending.Push([IO.DirectoryInfo]$entry)
                continue
            }
            if ($entry.Name -like '*.csproj.old') { continue }
            Copy-Item -LiteralPath $entry.FullName -Destination (Join-Path $destination $entry.Name) -Force
        }
    }
}

function Rewrite-StagedSolutionReferences([string]$SourceDirectory, [string]$DestinationDirectory) {
    $sourceSolution = Join-Path $SourceDirectory 'Devilution.Client.sln'
    $destinationSolution = Join-Path $DestinationDirectory 'Devilution.Client.sln'
    if (-not (Test-Path -LiteralPath $sourceSolution -PathType Leaf)) { return @() }
    $sourceText = [IO.File]::ReadAllText($sourceSolution)
    $references = [Collections.Generic.List[string]]::new()
    $pattern = '(?m)^(Project\("[^"]+"\) = "[^"]+", )"([^"]+)"(, "[^"]+"\r?)$'
    $rewritten = [Text.RegularExpressions.Regex]::Replace($sourceText, $pattern, [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $original = Resolve-ValidatedProjectReference $SourceDirectory $match.Groups[2].Value 'The client solution'
        $replacement = if ($original -ieq (Join-Path $SourceDirectory 'Devilution.Client.csproj')) {
            Join-Path $DestinationDirectory 'Devilution.Client.csproj'
        } else {
            $original
        }
        [void]$references.Add($replacement)
        return $match.Groups[1].Value + '"' + $replacement + '"' + $match.Groups[3].Value
    })
    [IO.File]::WriteAllText($destinationSolution, $rewritten, [Text.UTF8Encoding]::new($false))
    return @($references)
}

function New-IsolatedGodotProject([string]$SourceDirectory, [string]$DestinationDirectory) {
    $source = Get-CanonicalPath $SourceDirectory
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "The client source project directory is missing: $source" }
    Assert-NoReparseAncestry $source 'The client source project'
    $sourceProject = Join-Path $source 'Devilution.Client.csproj'
    $sourceMetadata = Get-ProjectMetadata $sourceProject $source
    $sourceHash = Get-ProjectTreeHash $source
    $sourceConfig = Join-Path (Split-Path $source -Parent) 'NuGet.Config'
    if (-not (Test-Path -LiteralPath $sourceConfig -PathType Leaf)) {
        throw "The Godot NuGet.Config ancestor is missing: $sourceConfig"
    }
    Assert-NoReparseAncestry $sourceConfig 'The Godot NuGet.Config'
    $sourceConfigHash = Get-FileSha256 $sourceConfig
    $destination = [IO.Path]::GetFullPath($DestinationDirectory)
    $sourcePrefix = $source.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $destinationComparable = $destination.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $sourceComparable = $source.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($destinationComparable.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase) -or $destinationComparable -eq $sourceComparable) {
        throw "The isolated Godot destination must be outside the source project: $destination"
    }
    $destinationParent = Split-Path $destination -Parent
    $stageConfig = Join-Path $destinationParent 'NuGet.Config'
    if (Test-Path -LiteralPath $stageConfig) {
        throw "The isolated Godot destination already has a NuGet.Config: $stageConfig"
    }
    if (Test-Path -LiteralPath $destination) {
        if (@(Get-ChildItem -LiteralPath $destination -Force).Count -gt 0) {
            throw "The isolated Godot destination must be fresh: $destination"
        }
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-IsolatedProjectTree $source $destination
    $stagedProject = Join-Path $destination 'Devilution.Client.csproj'
    Set-StagedProjectReferences $stagedProject $sourceMetadata.References
    $stagedSolutionReferences = Rewrite-StagedSolutionReferences $source $destination
    Copy-Item -LiteralPath $sourceConfig -Destination $stageConfig -Force
    $stagedMetadata = Get-ProjectMetadata $stagedProject $source
    if ($stagedMetadata.Signature -ne $sourceMetadata.Signature) {
        throw 'The staged client project changed outside its intended absolute ProjectReference paths.'
    }
    if (-not (Test-ExactStringSequence -Left @($stagedMetadata.References) -Right @($sourceMetadata.References))) {
        throw 'The staged client project did not retain its validated absolute ProjectReference paths.'
    }
    return [pscustomobject]@{
        SourceDirectory = $source
        DestinationDirectory = $destination
        SourceProject = $sourceProject
        StagedProject = $stagedProject
        SourceSdk = $sourceMetadata.Sdk
        SourceReferences = @($sourceMetadata.References)
        SourceSignature = $sourceMetadata.Signature
        SourceProjectHash = $sourceMetadata.FileHash
        SourceHash = $sourceHash
        SourceNuGetConfig = $sourceConfig
        StagedNuGetConfig = $stageConfig
        SourceNuGetConfigHash = $sourceConfigHash
        SolutionReferences = @($stagedSolutionReferences)
        StageHash = Get-ProjectTreeHash $destination
    }
}

function Complete-IsolatedGodotProject($Staging, [string]$EngineVersion) {
    if ($null -eq $Staging) { throw 'The isolated Godot project metadata is missing.' }
    if ([string]::IsNullOrWhiteSpace($EngineVersion)) { throw 'The imported Godot engine version is missing.' }
    $sourceHash = Get-ProjectTreeHash $Staging.SourceDirectory
    if ($sourceHash -cne $Staging.SourceHash) {
        throw 'The checked-in Godot client source changed while the isolated project was importing.'
    }
    if ((Get-FileSha256 $Staging.SourceNuGetConfig) -cne $Staging.SourceNuGetConfigHash) {
        throw 'The checked-in Godot NuGet.Config changed while the isolated project was importing.'
    }
    if ((Get-FileSha256 $Staging.StagedNuGetConfig) -cne $Staging.SourceNuGetConfigHash) {
        throw 'The isolated Godot NuGet.Config changed or disappeared during import.'
    }
    $currentSource = Get-ProjectMetadata $Staging.SourceProject $Staging.SourceDirectory
    if (($currentSource.Sdk -cne $Staging.SourceSdk) -or ($currentSource.FileHash -cne $Staging.SourceProjectHash) -or ($currentSource.Signature -cne $Staging.SourceSignature) -or -not (Test-ExactStringSequence -Left @($currentSource.References) -Right @($Staging.SourceReferences))) {
        throw 'The checked-in Godot client project changed while the isolated project was importing.'
    }
    $stagedXml = Read-ProjectXml $Staging.StagedProject
    $stagedReferences = @($stagedXml.DocumentElement.SelectNodes('.//ProjectReference') | ForEach-Object { $_.GetAttribute('Include') })
    if (-not (Test-ExactStringSequence -Left @($stagedReferences) -Right @($Staging.SourceReferences))) {
        throw 'Godot import changed the staged ProjectReference targets.'
    }
    if ((Get-ProjectSignature $stagedXml) -cne $Staging.SourceSignature) {
        throw 'Godot import changed the staged client project outside the SDK declaration.'
    }
    $expectedImportedSdk = "Godot.NET.Sdk/$EngineVersion"
    if ($stagedXml.DocumentElement.GetAttribute('Sdk') -cne $expectedImportedSdk) {
        throw "Godot import produced staged SDK '$($stagedXml.DocumentElement.GetAttribute('Sdk'))'; expected '$expectedImportedSdk'."
    }
    $Staging | Add-Member -NotePropertyName ImportedSdk -NotePropertyValue $stagedXml.DocumentElement.GetAttribute('Sdk') -Force
    $Staging | Add-Member -NotePropertyName StageHash -NotePropertyValue (Get-ProjectTreeHash $Staging.DestinationDirectory) -Force
    return $Staging
}

function Write-ProjectStageEvidence($Staging, [string]$EngineVersion, [string]$EvidencePath, [string]$ErrorMessage) {
    $importedSdk = $Staging.ImportedSdk
    if ([string]::IsNullOrWhiteSpace($importedSdk) -and (Test-Path -LiteralPath $Staging.StagedProject -PathType Leaf)) {
        try { $importedSdk = (Read-ProjectXml $Staging.StagedProject).DocumentElement.GetAttribute('Sdk') }
        catch { $importedSdk = $null }
    }
    $stageHash = $Staging.StageHash
    try { $stageHash = Get-ProjectTreeHash $Staging.DestinationDirectory }
    catch { }
    $record = [ordered]@{
        SourceSdk = $Staging.SourceSdk
        ImportedSdk = $importedSdk
        EngineVersion = $EngineVersion
        SourceHash = $Staging.SourceHash
        StageHash = $stageHash
        StagePath = $Staging.DestinationDirectory
        Error = $ErrorMessage
    }
    [IO.File]::WriteAllText($EvidencePath, ($record | ConvertTo-Json))
}

function Assert-CohortCheckpoint(
    [string]$SaveDirectory,
    [string]$ExpectedGameMode,
    [string]$ContentHash,
    [string]$RulesetHash,
    [string[]]$BookmarkPaths,
    [uint32[]]$ExpectedEntityIds = @()) {
    $checkpointPath = Join-Path $SaveDirectory 'authoritative-cohort.json'
    if (-not (Test-Path -LiteralPath $checkpointPath -PathType Leaf)) {
        throw "Complete-cohort acceptance did not produce $checkpointPath."
    }

    # Keep one read handle open with replacement sharing. The host may publish
    # the next generation while this verifier is reading the exact generation
    # that it opened; pathname metadata must not be used as its identity.
    $stream = $null
    try {
        $stream = [IO.File]::Open(
            $checkpointPath,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        if ($stream.Length -gt [int]::MaxValue) { throw 'The cohort checkpoint is too large to verify.' }
        $bytes = New-Object byte[] ([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -le 0) { throw 'The cohort checkpoint ended during verification.' }
            $offset += $read
        }
    } finally {
        if ($null -ne $stream) { $stream.Dispose() }
    }

    $strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
    try { $json = $strictUtf8.GetString($bytes) }
    catch { throw 'The cohort checkpoint is not strict UTF-8.' }
    $integrityMatch = [Text.RegularExpressions.Regex]::Match(
        $json,
        ',"IntegritySha256":"(?<hash>[0-9a-f]{64})"}\z',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $integrityMatch.Success) {
        throw 'The cohort checkpoint does not use the canonical compact integrity suffix.'
    }
    $payloadJson = $json.Substring(0, $integrityMatch.Index) + '}'
    $actualIntegrity = Get-Sha256Hex ($strictUtf8.GetBytes($payloadJson))
    if ($actualIntegrity -cne $integrityMatch.Groups['hash'].Value) {
        throw 'The cohort checkpoint integrity hash does not match its exact canonical payload bytes.'
    }

    try { $document = $json | ConvertFrom-Json -ErrorAction Stop }
    catch { throw 'The cohort checkpoint JSON is malformed.' }
    Assert-CohortJsonProperties $document @(
        'FormatVersion', 'Generation', 'Identity', 'Tick', 'NextEntityId',
        'NextServerReceiptSequence', 'Participants', 'SerializedModuleState', 'IntegritySha256') 'cohort root'
    Assert-CohortJsonProperties $document.Identity @(
        'ModuleId', 'ModuleFormatVersion', 'ProtocolSchemaVersion', 'ContentManifestHash',
        'RulesetIdentityHash', 'TickRateHz', 'GameMode') 'cohort identity'
    if ($null -eq $document -or $document.FormatVersion -ne 1 -or $null -eq $document.Identity) {
        throw 'The cohort checkpoint format or identity is invalid.'
    }
    if (-not ($document.Participants -is [Array]) -or
        -not (Test-CohortJsonInteger $document.FormatVersion 1 1) -or
        -not (Test-CohortJsonInteger $document.Generation 1 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.Tick 0 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.NextEntityId 0 ([uint32]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.NextServerReceiptSequence 0 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.Identity.ModuleFormatVersion 1 ([int]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.Identity.TickRateHz 1 ([uint32]::MaxValue)) -or
        -not (Test-CohortJsonInteger $document.Identity.GameMode 0 1)) {
        throw 'The cohort checkpoint contains a non-canonical numeric type or range.'
    }
    $expectedGameModeValue = if ($ExpectedGameMode -eq 'SinglePlayer') { 0 } elseif ($ExpectedGameMode -eq 'Multiplayer') { 1 } else { -1 }
    if ($expectedGameModeValue -lt 0 -or $document.Identity.ProtocolSchemaVersion -cne '0.2.0' -or
        $document.Identity.GameMode -ne $expectedGameModeValue -or
        $document.Identity.ContentManifestHash -cne $ContentHash -or
        $document.Identity.RulesetIdentityHash -cne $RulesetHash) {
        throw 'The cohort checkpoint identity does not match the active host.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$document.SerializedModuleState)) {
        throw 'The cohort checkpoint has no serialized module state.'
    }
    try {
        $moduleBytes = [Convert]::FromBase64String([string]$document.SerializedModuleState)
        if ($moduleBytes.Length -eq 0 -or
            [Convert]::ToBase64String($moduleBytes) -cne [string]$document.SerializedModuleState) {
            throw 'non-canonical module state'
        }
        [void]([Text.UTF8Encoding]::new($false, $true).GetString($moduleBytes))
    } catch { throw 'The cohort checkpoint module blob is empty or invalid Base64.' }

    $participants = @($document.Participants)
    $bookmarks = @($BookmarkPaths)
    if ($participants.Count -ne $bookmarks.Count) {
        throw 'The cohort participant count does not match the acceptance bookmarks.'
    }
    if ($ExpectedEntityIds.Count -ne 0 -and $ExpectedEntityIds.Count -ne $bookmarks.Count) {
        throw 'The cohort expected entity bindings do not match the acceptance bookmarks.'
    }
    $sessionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $bookmarkSessionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($bookmarkPath in $bookmarks) {
        if ([string]::IsNullOrWhiteSpace($bookmarkPath) -or -not (Test-Path -LiteralPath $bookmarkPath -PathType Leaf)) {
            throw "The cohort acceptance bookmark is missing: $bookmarkPath"
        }
        $bookmark = [IO.File]::ReadAllText($bookmarkPath).Trim()
        if ([string]::IsNullOrWhiteSpace($bookmark)) { throw "The cohort acceptance bookmark is empty: $bookmarkPath" }
        $bookmarkSessionId = Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($bookmark))
        if (-not $bookmarkSessionIds.Add($bookmarkSessionId)) {
            throw 'Cohort acceptance bookmarks must bind to distinct participants.'
        }
    }
    $entityIds = [Collections.Generic.HashSet[uint32]]::new()
    for ($index = 0; $index -lt $participants.Count; $index++) {
        $participant = $participants[$index]
        Assert-CohortJsonProperties $participant @('SessionId', 'EntityId', 'CommandReceipts') 'cohort participant'
        if (-not ($participant.CommandReceipts -is [Array])) { throw 'The cohort participant receipt ledger must be an array.' }
        if (($multiplayerRecovery -or $cooperative) -and @($participant.CommandReceipts).Count -eq 0) {
            throw 'The complete-cohort multiplayer acceptance requires a nonempty canonical receipt ledger for every participant.'
        }
        if ($null -eq $participant -or [string]$participant.SessionId -notmatch '^[0-9a-f]{64}$') {
            throw 'The cohort participant session identity is not a lowercase SHA-256 ID.'
        }
        try {
            if (-not (Test-CohortJsonInteger $participant.EntityId 1 ([uint32]::MaxValue))) {
                throw 'invalid entity ID'
            }
            $entityId = [uint32]$participant.EntityId
        } catch { throw 'The cohort participant entity identity is invalid.' }
        if (-not $sessionIds.Add([string]$participant.SessionId) -or -not $entityIds.Add($entityId)) {
            throw 'The cohort participant session or entity identities are not unique.'
        }
        foreach ($receipt in @($participant.CommandReceipts)) {
            Assert-CohortJsonProperties $receipt @('ClientSequence', 'ServerReceiptSequence', 'AppliedTick', 'Status', 'RejectReason') 'cohort receipt'
            if (-not (Test-CohortJsonInteger $receipt.ClientSequence 1 ([decimal][uint64]::MaxValue)) -or
                -not (Test-CohortJsonInteger $receipt.ServerReceiptSequence 1 ([decimal][uint64]::MaxValue)) -or
                -not (Test-CohortJsonInteger $receipt.AppliedTick 0 ([decimal][uint64]::MaxValue)) -or
                -not (Test-CohortJsonInteger $receipt.Status 0 ([uint32]::MaxValue)) -or
                -not (Test-CohortJsonInteger $receipt.RejectReason 0 ([uint32]::MaxValue))) {
                throw 'The cohort receipt contains a non-canonical numeric type or range.'
            }
        }
    }
    if ($bookmarkSessionIds.Count -ne $sessionIds.Count -or
        @($bookmarkSessionIds | Where-Object { -not $sessionIds.Contains($_) }).Count -ne 0) {
        throw 'The cohort acceptance bookmarks do not bind to the complete participant roster.'
    }
    if ($ExpectedEntityIds.Count -ne 0) {
        $expectedBySession = @{}
        for ($index = 0; $index -lt $bookmarks.Count; $index++) {
            $bookmark = [IO.File]::ReadAllText($bookmarks[$index]).Trim()
            $expectedBySession[(Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($bookmark)))] = $ExpectedEntityIds[$index]
        }
        foreach ($participant in $participants) {
            if ([uint32]$expectedBySession[[string]$participant.SessionId] -ne [uint32]$participant.EntityId) {
                throw 'The cohort participant entity binding does not match the acceptance scene.'
            }
        }
    }

    if ($multiplayerRecovery -or $cooperative) {
        foreach ($bookmarkPath in $bookmarks) {
            $bookmark = [IO.File]::ReadAllText($bookmarkPath).Trim()
            $sessionId = Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($bookmark))
            $participant = @($participants | Where-Object { $_.SessionId -ceq $sessionId })
            $evidencePath = Join-Path (Split-Path $bookmarkPath -Parent) ($AcceptancePhase.ToLowerInvariant() + '.json')
            if ($participant.Count -ne 1 -or -not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
                throw 'MultiplayerRecovery lacks the bookmark-bound final command evidence.'
            }
            $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
            Assert-CohortRecoveryReceipt $participant[0] $evidence
            if ($pendingRecovery) { Assert-CohortPendingReceipt $participant[0] $evidence }
            if ($cooperative) { Assert-CohortCooperativePickupReceipt $participants $evidence }
        }
    }

    $legacyCheckpoints = @(Get-ChildItem -LiteralPath $SaveDirectory -File -Force -Recurse -ErrorAction Stop |
        Where-Object { $_.Name -eq '.session.json' -or $_.Name -like '*.session.json' })
    if ($legacyCheckpoints.Count -ne 0) {
        throw 'Complete-cohort acceptance must not leave legacy per-session checkpoints.'
    }
    return $null
}

function Assert-CohortRecoveryReceipt($Participant, $Evidence) {
    if ($null -eq $Participant -or $null -eq $Evidence -or
        -not ($Evidence.Passed -is [bool]) -or -not $Evidence.Passed -or
        -not (Test-CohortJsonInteger $Evidence.EntityId 1 ([uint32]::MaxValue)) -or
        -not (Test-CohortJsonInteger $Evidence.Sequence 1 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $Evidence.ReceiptSequence 1 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $Evidence.AppliedTick 0 ([decimal][uint64]::MaxValue)) -or
        $Participant.EntityId -ne $Evidence.EntityId) {
        throw 'MultiplayerRecovery final evidence is invalid for its canonical owner.'
    }
    $matches = @($Participant.CommandReceipts | Where-Object {
        $_.ClientSequence -eq $Evidence.Sequence -and
        $_.ServerReceiptSequence -eq $Evidence.ReceiptSequence -and
        $_.AppliedTick -eq $Evidence.AppliedTick
    })
    # A successful duplicate retains the original accepted canonical receipt.
    if ($matches.Count -ne 1 -or $matches[0].Status -ne 1 -or $matches[0].RejectReason -ne 0) {
        throw 'MultiplayerRecovery final command has no exact accepted canonical receipt.'
    }
}

function Assert-CohortCooperativePickupReceipt($Participants, $Evidence) {
    # The terminal pickup is distinct from any preceding approach movement.
    $owner = @($Participants | Where-Object { $_.EntityId -eq $Evidence.OwnerEntityId })
    if ($owner.Count -ne 1) { throw 'Cooperative pickup has no unique canonical owner.' }
    Assert-CohortRecoveryReceipt $owner[0] ([pscustomobject]@{
        Passed = $Evidence.Passed; EntityId = $Evidence.OwnerEntityId;
        Sequence = $Evidence.PickupSequence; ReceiptSequence = $Evidence.PickupReceiptSequence;
        AppliedTick = $Evidence.PickupAppliedTick
    })
}

function Assert-CohortPendingReceipt($Participant, $Evidence) {
    if ($null -eq $Evidence -or
        -not (Test-CohortJsonInteger $Evidence.PendingSequence 1 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $Evidence.PendingReceiptSequence 1 ([decimal][uint64]::MaxValue)) -or
        -not (Test-CohortJsonInteger $Evidence.PendingAppliedTick 0 ([decimal][uint64]::MaxValue))) {
        throw 'Pending recovery omitted an exact original committed receipt.'
    }
    Assert-CohortRecoveryReceipt $Participant ([pscustomobject]@{
        Passed = $true; EntityId = $Evidence.EntityId; Sequence = $Evidence.PendingSequence;
        ReceiptSequence = $Evidence.PendingReceiptSequence; AppliedTick = $Evidence.PendingAppliedTick
    })
}

function Assert-CohortPendingPlayer($Owner, $ModuleOwner, $Prior) {
    $player = $ModuleOwner.Player
    if ($null -eq $Owner -or $null -eq $ModuleOwner -or $null -eq $Prior -or
        -not ($Prior.Passed -is [bool]) -or -not $Prior.Passed -or
        -not ($player.FriendlyMode -is [bool]) -or -not ($Prior.Local.Hostile -is [bool]) -or
        $ModuleOwner.SessionId -cne $Owner.SessionId -or
        $Owner.EntityId -ne $ModuleOwner.EntityId -or $Owner.EntityId -ne $player.EntityId -or
        $Owner.EntityId -ne $Prior.EntityId -or $player.FriendlyMode -ne $Prior.Local.Hostile -or
        $player.PositionX -ne $Prior.Local.X -or $player.PositionY -ne $Prior.Local.Y -or
        $player.LevelId -ne $Prior.Local.LevelId -or $player.CharacterName -cne $Prior.Local.Name -or
        $player.CharacterClassId -ne $Prior.Local.ClassId -or $player.Gold -ne $Prior.Local.Gold) {
        throw 'Pending recovery canonical state does not contain the committed friendly-mode assignment.'
    }
}

function Assert-PendingRecoveryCanonicalBoundary([string]$SaveDirectory, $Ready, [string]$RootDirectory) {
    $stream = $null; $reader = $null
    try {
        $stream = [IO.File]::Open((Join-Path $SaveDirectory 'authoritative-cohort.json'),
            [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        $document = $reader.ReadToEnd() | ConvertFrom-Json
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
        elseif ($null -ne $stream) { $stream.Dispose() }
    }
    if (@($document.Participants).Count -ne 2) { throw 'Pending recovery canonical boundary needs two owners.' }
    $moduleBytes = [Convert]::FromBase64String($document.SerializedModuleState)
    if ([Convert]::ToBase64String($moduleBytes) -cne $document.SerializedModuleState) {
        throw 'Pending recovery requires canonical module Base64.'
    }
    $module = ([Text.UTF8Encoding]::new($false, $true).GetString($moduleBytes)) | ConvertFrom-Json
    if ($module.FormatVersion -ne 1 -or $module.Tick -ne $document.Tick -or @($module.Participants).Count -ne 2) {
        throw 'Pending recovery module and host do not describe the same complete tick.'
    }
    foreach ($evidence in $Ready) {
        $bookmark = [IO.File]::ReadAllText((Join-Path $RootDirectory "player-$($evidence.Slot)/resume-token.txt")).Trim()
        $sessionId = Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($bookmark))
        $participant = @($document.Participants | Where-Object { $_.SessionId -ceq $sessionId })
        if ($participant.Count -ne 1) { throw 'Pending recovery canonical boundary lost an owner binding.' }
        Assert-CohortPendingReceipt $participant[0] $evidence
        $moduleOwner = @($module.Participants | Where-Object { $_.SessionId -ceq $sessionId })
        $prior = Get-Content -LiteralPath (Join-Path $RootDirectory "player-$($evidence.Slot)/multiplayerresume.json") -Raw | ConvertFrom-Json
        if ($moduleOwner.Count -ne 1) { throw 'Pending recovery module lost an owner binding.' }
        Assert-CohortPendingPlayer $participant[0] $moduleOwner[0] $prior
    }
    # Safe boundary metadata only: never expose private bookmarks/module state.
    return [pscustomobject]@{ Verified = $true; Generation = $document.Generation; Tick = $document.Tick }
}

function Get-CohortSceneEntityId($Evidence) {
    if ($null -eq $Evidence -or $Evidence.PSObject.Properties.Name -notcontains 'EntityId' -or
        -not (Test-CohortJsonInteger $Evidence.EntityId 1 ([uint32]::MaxValue))) {
        throw 'Complete-cohort scene evidence must contain a nonzero uint32 entity binding.'
    }
    return [uint32]$Evidence.EntityId
}

function Assert-CohortJsonProperties($Value, [string[]]$Expected, [string]$Description) {
    if ($null -eq $Value) { throw "The $Description is missing." }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Expected.Count -or
        @($actual | Where-Object { $_ -notin $Expected }).Count -ne 0 -or
        @($Expected | Where-Object { $_ -notin $actual }).Count -ne 0) {
        throw "The $Description has unexpected or missing properties."
    }
}

function Test-CohortJsonInteger($Value, [decimal]$Minimum, [decimal]$Maximum) {
    if ($null -eq $Value -or $Value -is [bool] -or $Value -is [string] -or
        $Value -is [double] -or $Value -is [single] -or $Value -is [decimal]) { return $false }
    try {
        $number = [decimal]$Value
        return $number -ge $Minimum -and $number -le $Maximum -and $number -eq [decimal]::Truncate($number)
    } catch { return $false }
}

function Get-Sha256Hex([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Write-ProcessEvidence($Tracked) {
    [IO.File]::WriteAllText(
        $Tracked.EvidencePath,
        ($Tracked.Record | ConvertTo-Json -Depth 4))
}

if ($env:OS -eq 'Windows_NT' -and -not ('GodevilutionNativeErrorMode' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class GodevilutionNativeErrorMode
{
    [DllImport("kernel32.dll")]
    public static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    public static extern uint SetErrorMode(uint mode);
}
'@
}

function Start-GodotChildProcess($StartInfo) {
    return [Diagnostics.Process]::Start($StartInfo)
}

function Get-GodotClientRunPolicy([bool]$Headless, [bool]$Automated, [string]$AcceptancePhase, [int]$QuitAfterSeconds) {
    $bounded = $Headless -or $Automated -or -not [string]::IsNullOrWhiteSpace($AcceptancePhase)
    return [pscustomobject]@{
        SuppressCrashDialog = $bounded
        # Acceptance scenes own their completion; ordinary automated scenes use
        # real seconds, with a separate parent deadline if the scene cannot quit.
        SceneQuitAfterSeconds = $(if ($bounded -and [string]::IsNullOrWhiteSpace($AcceptancePhase)) { $QuitAfterSeconds } else { $null })
        TimeoutSeconds = $(if ($bounded) { $QuitAfterSeconds + 15 } else { $null })
    }
}

function Assert-LocalLauncherCleanup([bool]$LaunchFailed, [string[]]$CleanupErrors) {
    # Never accept a launch whose owned-process cleanup could not be confirmed.
    # An existing launch error remains primary; its finally block already warns
    # separately about each cleanup failure.
    if (-not $LaunchFailed -and $CleanupErrors.Count -gt 0) {
        throw ('Launcher cleanup failed; do not retry until owned processes are confirmed stopped. ' + ($CleanupErrors -join ' | '))
    }
}

function Stop-OwnedLocalProcess($Process, [ValidateRange(1, 60000)] [int]$TimeoutMilliseconds = 5000) {
    if ($null -eq $Process -or $Process.HasExited) { return }
    try {
        $Process.Kill()
    } catch {
        # A natural exit between HasExited and Kill does not require cleanup.
        if (-not $Process.HasExited) { throw }
    }
    if (-not $Process.HasExited -and -not $Process.WaitForExit($TimeoutMilliseconds) -and -not $Process.HasExited) {
        throw "Owned process $($Process.Id) did not terminate within $TimeoutMilliseconds ms after cleanup."
    }
}

function Get-OwnedTerminalExitCode($Process) {
    if ($null -eq $Process -or -not $Process.HasExited) {
        throw 'Native exit evidence requires a terminated owned process.'
    }
    if (-not $Process.WaitForExit(5000)) {
        throw 'The terminated owned process did not complete its bounded terminal wait.'
    }
    $code = $Process.ExitCode
    if ($null -eq $code) { throw 'The terminated owned process did not expose a native exit code.' }
    return $code
}

function New-GodotProcess(
    [string]$StageName,
    [string]$Arguments,
    [hashtable]$EnvironmentOverrides,
    [bool]$Headless,
    [bool]$SuppressCrashDialog,
    [string]$StandardOutputPath,
    [string]$StandardErrorPath) {
    $stageRoot = Join-Path $captureRoot ('engine-state/' + $StageName)
    $appDataPath = Join-Path $stageRoot 'AppData'
    $localAppDataPath = Join-Path $stageRoot 'LocalAppData'
    Assert-WritableDirectory $appDataPath 'AppData'
    Assert-WritableDirectory $localAppDataPath 'LocalAppData'

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $GodotExecutable
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $repositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $Headless
    $startInfo.WindowStyle = if ($Headless) { 'Hidden' } else { 'Normal' }
    $startInfo.EnvironmentVariables['APPDATA'] = $appDataPath
    $startInfo.EnvironmentVariables['LOCALAPPDATA'] = $localAppDataPath
    if (-not [string]::IsNullOrWhiteSpace($StandardOutputPath)) {
        New-Item -ItemType Directory -Path (Split-Path $StandardOutputPath -Parent) -Force | Out-Null
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
    }
    foreach ($entry in $EnvironmentOverrides.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value
    }

    $process = $null
    try {
        $previousErrorMode = $null
        try {
            if ($SuppressCrashDialog -and $env:OS -eq 'Windows_NT') {
                $previousErrorMode = [GodevilutionNativeErrorMode]::GetErrorMode()
                [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode -bor 3)
            }
            $process = Start-GodotChildProcess $startInfo
        } finally {
            if ($null -ne $previousErrorMode) {
                [void][GodevilutionNativeErrorMode]::SetErrorMode($previousErrorMode)
            }
        }
        if ($null -eq $process) { throw "Could not start Godot for stage $StageName." }
        $startedUtc = [DateTime]::UtcNow.ToString('O')
        try { $startedUtc = $process.StartTime.ToUniversalTime().ToString('O') }
        catch { }
        $tracked = [pscustomobject]@{
            Process = $process
            EvidencePath = Join-Path $processEvidenceRoot ($StageName + '.json')
            Record = [pscustomobject]@{
                Stage = $StageName
                Executable = $GodotExecutable
                PersistenceMode = $PersistenceMode
                ProcessId = $process.Id
                StartedUtc = $startedUtc
                EndedUtc = $null
                ExitCode = $null
                Error = $null
            }
            StandardOutputTask = $null
            StandardErrorTask = $null
            StandardOutputPath = $StandardOutputPath
            StandardErrorPath = $StandardErrorPath
        }
        if (-not [string]::IsNullOrWhiteSpace($StandardOutputPath)) {
            $tracked.StandardOutputTask = $process.StandardOutput.ReadToEndAsync()
            $tracked.StandardErrorTask = $process.StandardError.ReadToEndAsync()
        }
        Write-ProcessEvidence $tracked
        return $tracked
    } catch {
        $primaryError = $_
        if ($null -ne $process) {
            try {
                Stop-OwnedLocalProcess $process
            } catch { Write-Warning "Could not stop failed Godot process for stage ${StageName}: $($_.Exception.Message)" }
            try { $process.Dispose() }
            catch { Write-Warning "Could not dispose failed Godot process for stage ${StageName}: $($_.Exception.Message)" }
        }
        throw $primaryError
    }
}

function Parse-SupportedGodotEngineVersion([string]$VersionOutput) {
    if ([string]::IsNullOrWhiteSpace($VersionOutput)) {
        throw 'Godot version output was empty.'
    }
    $matches = [Text.RegularExpressions.Regex]::Matches($VersionOutput,
        '(?m)^\s*(?:Godot Engine v)?(?<version>[0-9]+\.[0-9]+\.[0-9]+)\.stable\.mono(?:\.[A-Za-z0-9_-]+)*[ \t]*\r?$')
    if ($matches.Count -ne 1) {
        throw "Godot output must identify exactly one stable Mono version: $VersionOutput"
    }
    $version = $matches[0].Groups['version'].Value
    if ($version -notmatch '^4\.7\.') {
        throw "Unsupported Godot Mono engine version $version; this launcher requires 4.7.x."
    }
    return $version
}

function Get-GodotEngineVersion {
    $stdoutPath = Join-Path $captureRoot 'engine-version.stdout.log'
    $stderrPath = Join-Path $captureRoot 'engine-version.stderr.log'
    $tracked = New-GodotProcess 'engine-version' '--version' @{} $true $true $stdoutPath $stderrPath
    $script:versionTracked = $tracked
    Complete-GodotProcess $tracked 30000 'Godot version probe'
    $versionText = [IO.File]::ReadAllText($stdoutPath) + [Environment]::NewLine + [IO.File]::ReadAllText($stderrPath)
    return Parse-SupportedGodotEngineVersion $versionText
}

function Write-GodotRedirectedOutput($Tracked) {
    if ($null -eq $Tracked.StandardOutputTask) { return }
    foreach ($entry in @(
        @{ Task = $Tracked.StandardOutputTask; Path = $Tracked.StandardOutputPath },
        @{ Task = $Tracked.StandardErrorTask; Path = $Tracked.StandardErrorPath })) {
        if (-not $entry.Task.IsCompleted -and -not $entry.Task.Wait(1000)) {
            throw "Godot redirected output did not close after process termination: $($entry.Path)"
        }
        [IO.File]::WriteAllText($entry.Path, $entry.Task.GetAwaiter().GetResult())
    }
}

function Complete-GodotProcess($Tracked, [int]$TimeoutMilliseconds, [string]$FailurePrefix) {
    if ($null -eq $Tracked.Record.EndedUtc) {
        # HasExited is authoritative when the caller already observed process
        # completion; a zero-time WaitForExit can transiently report false.
        if (-not $Tracked.Process.HasExited -and -not $Tracked.Process.WaitForExit($TimeoutMilliseconds)) {
            $Tracked.Record.Error = "$FailurePrefix timed out after $TimeoutMilliseconds ms."
            Write-ProcessEvidence $Tracked
            throw $Tracked.Record.Error
        }
        $Tracked.Record.EndedUtc = [DateTime]::UtcNow.ToString('O')
    }
    if ($null -eq $Tracked.Record.ExitCode) { $Tracked.Record.ExitCode = $Tracked.Process.ExitCode }
    if ($Tracked.Record.ExitCode -ne 0) {
        if ($null -eq $Tracked.Record.Error) { $Tracked.Record.Error = "$FailurePrefix exited with code $($Tracked.Record.ExitCode)." }
    }
    $terminalError = if ($Tracked.Record.ExitCode -ne 0) { "$($Tracked.Record.Error) See the stage log." } else { $null }
    # Persist terminal status before fallible redirected-output storage, and
    # retain the engine failure as primary even when its diagnostics cannot save.
    try {
        Write-ProcessEvidence $Tracked
        Write-GodotRedirectedOutput $Tracked
    } catch {
        if ($null -eq $terminalError) { throw }
        $Tracked.Record | Add-Member -NotePropertyName DiagnosticError -NotePropertyValue $_.Exception.Message -Force
        try { Write-ProcessEvidence $Tracked }
        catch { Write-Warning "Could not retain terminal Godot evidence: $($_.Exception.Message)" }
        Write-Warning "Could not retain Godot diagnostics: $($Tracked.Record.DiagnosticError)"
        throw $terminalError
    }
    if ($null -ne $terminalError) { throw $terminalError }
}

function Assert-GodotImportLog($Tracked, [string]$ImportLogPath) {
    if (-not (Test-Path -LiteralPath $ImportLogPath -PathType Leaf)) {
        $Tracked.Record.Error = "Godot asset import did not produce its log: $ImportLogPath."
        Write-ProcessEvidence $Tracked
        throw $Tracked.Record.Error
    }
    $importLogText = Get-Content -LiteralPath $ImportLogPath -Raw -ErrorAction Stop
    foreach ($fatalImportPattern in @(
        'Cannot create file',
        'Cannot save file',
        'Cannot save editor help cache',
        'Error saving editor settings')) {
        if ($importLogText -match [regex]::Escape($fatalImportPattern)) {
            $Tracked.Record.Error = "Godot asset import reported fatal storage error: $fatalImportPattern."
            Write-ProcessEvidence $Tracked
            throw "$($Tracked.Record.Error) See $ImportLogPath"
        }
    }
}

function Stop-GodotProcess($Tracked) {
    if ($null -eq $Tracked -or $null -eq $Tracked.Process) { return }
    if (-not $Tracked.Process.HasExited) {
        try {
            Stop-OwnedLocalProcess $Tracked.Process
        } catch {
            $Tracked.Record | Add-Member -NotePropertyName CleanupError -NotePropertyValue $_.Exception.Message -Force
            Write-ProcessEvidence $Tracked
            throw
        }
        if ($null -eq $Tracked.Record.Error) { $Tracked.Record.Error = 'Terminated by launcher cleanup.' }
    }
    if ($null -eq $Tracked.Record.EndedUtc) {
        $Tracked.Record.EndedUtc = [DateTime]::UtcNow.ToString('O')
    }
    if ($null -eq $Tracked.Record.ExitCode) { $Tracked.Record.ExitCode = $Tracked.Process.ExitCode }
    if ($null -eq $Tracked.Record.Error -and $Tracked.Record.ExitCode -ne 0) {
        $Tracked.Record.Error = "Godot $($Tracked.Record.Stage) exited with code $($Tracked.Record.ExitCode)."
    }
    Write-ProcessEvidence $Tracked
    # Drain redirected version-probe output even after timeout/owned-child cleanup.
    # Callers retain the primary launch error if evidence storage also fails.
    Write-GodotRedirectedOutput $Tracked
}

$clientRunPolicy = Get-GodotClientRunPolicy ([bool]$Headless) ([bool]$Automated) $AcceptancePhase $QuitAfterSeconds

try {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    $cathedralProfile = if ($cathedralDepths) {
        Get-CathedralPlaytestProfile $CathedralDepthProfileDirectory $SessionDirectory $true
    } elseif ($cathedral) { Get-CathedralPlaytestProfile $CathedralProfileDirectory $SessionDirectory } else { $null }
    $cathedralDoorProfile = if ($cathedralDoors) {
        Get-CathedralDoorPlaytestProfile $CathedralDoorProfileDirectory $SessionDirectory
    } else { $null }
    $cathedralNativeProfile = if ($cathedralDepthProgression) {
        . (Join-Path $PSScriptRoot 'native-depth-progression-profile.ps1')
        Get-CathedralNativeDepthProgressionProfile $CathedralNativeDepthProgressionProfileDirectory $SessionDirectory
    } elseif ($cathedralProgression) {
        . (Join-Path $PSScriptRoot 'native-progression-profile.ps1')
        Get-CathedralNativeProgressionProfile $CathedralNativeProgressionProfileDirectory $SessionDirectory
    } elseif ($cathedralNative) {
        Get-CathedralNativePopulationProfile $CathedralNativeProfileDirectory $SessionDirectory
    } else { $null }
    New-Item -ItemType Directory -Path $SessionDirectory -Force | Out-Null
    if ($performance -and (Get-ChildItem -LiteralPath $SessionDirectory -Force)) {
        throw 'Performance acceptance requires a fresh empty session directory.'
    }
    # One local writer per save directory; parallel tests use distinct directories.
    $sessionLock = [System.IO.File]::Open((Join-Path $SessionDirectory 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    if ($multiplayerRecovery) {
        $recoveryPrefix = $AcceptancePhase.ToLowerInvariant()
        foreach ($marker in @("$recoveryPrefix-server-stopped.json", "$recoveryPrefix-server-restarted.json",
                "$recoveryPrefix-processes.json")) {
            if (Test-Path -LiteralPath (Join-Path $SessionDirectory $marker)) {
                throw "MultiplayerRecovery requires a fresh dedicated root; stale marker exists: $marker"
            }
        }
    }
    if ($cathedral) { Initialize-CathedralPlaytestSession $cathedralProfile $SessionDirectory }
    if ($cathedralDoors) { Initialize-CathedralDoorPlaytestSession $cathedralDoorProfile $SessionDirectory }
    if ($cathedralNative) {
        Initialize-CathedralNativePopulationSession $cathedralNativeProfile $SessionDirectory $cathedralProgression $cathedralDepthProgression
    }
    $staging = New-IsolatedGodotProject $clientSourceProject (Join-Path $captureRoot 'godot/Devilution.Client')
    $clientProject = $staging.DestinationDirectory
    Write-ProjectStageEvidence $staging $null $projectStageEvidencePath $null
    $engineVersion = Get-GodotEngineVersion
    Write-ProjectStageEvidence $staging $engineVersion $projectStageEvidencePath $null
    & dotnet build $serverProject -c $ServerBuildConfiguration --nologo -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the authoritative server.' }
    # Import the isolated SVG assets before building the staged client project.
    $importLogPath = Join-Path $captureRoot 'import.log'
    $importArguments = '--headless --editor --import --path "{0}" --log-file "{1}"' -f $clientProject, $importLogPath
    $importTracked = New-GodotProcess 'import' $importArguments @{} $true $true
    $importProcess = $importTracked.Process
    Complete-GodotProcess $importTracked 60000 'Godot asset import'
    Assert-GodotImportLog $importTracked $importLogPath
    $staging = Complete-IsolatedGodotProject $staging $engineVersion
    Write-ProjectStageEvidence $staging $engineVersion $projectStageEvidencePath $null
    & dotnet build (Join-Path $clientProject 'Devilution.Client.csproj') --nologo -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the staged Godot client.' }
    $runtimeAssemblyManifest = if ($performance) { Get-GodotRuntimeAssemblyManifest $clientProject } else { $null }

    # Seed 5 keeps Butcher available under both production and native quest-pool
    # orderings. This diagnostic does not certify parity of the complete pools.
    # Existing acceptance and preview profiles retain seed 0.
    $acceptanceArguments = if ($LifecyclePreviewContent) { '--starting-level 0 --random-seed 5' } elseif ($AcceptancePhase -or $PreviewContent -or $PortalPreviewContent -or $Multiplayer -or $cathedral -or $cathedralDoors -or $cathedralNative) { '--starting-level 0 --random-seed 0' } else { '' }
    $overlayArguments = if ($PreviewContent -or $PortalPreviewContent) { '--content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-loop-preview') } elseif ($LifecyclePreviewContent) { '--content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-lifecycle-preview') } else { '' }
    if ($PortalPreviewContent) {
        $overlayArguments += ' --content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-portal-preview')
    }
    if ($cathedral) {
        $overlayArguments = '--content-root "{0}" --content-id {1} --content-version {2} --game-adapter {3}' -f $cathedralProfile.ContentRoot, $cathedralProfile.Metadata.ContentId, $cathedralProfile.Metadata.ContentVersion, $cathedralProfile.Metadata.AdapterId
    }
    if ($cathedralDoors) {
        $overlayArguments = '--content-root "{0}" --content-id {1} --content-version {2} --game-adapter {3}' -f $cathedralDoorProfile.ContentRoot, $cathedralDoorProfile.Metadata.ContentId, $cathedralDoorProfile.Metadata.ContentVersion, $cathedralDoorProfile.Metadata.AdapterId
    }
    if ($cathedralNative) {
        $overlayArguments = '--content-root "{0}" --content-id {1} --content-version {2} --game-adapter {3} --content-overlay "{4}" --content-overlay "{5}"' -f
            $cathedralNativeProfile.ContentRoot, $cathedralNativeProfile.Metadata.ContentId, $cathedralNativeProfile.Metadata.ContentVersion,
            $cathedralNativeProfile.Metadata.AdapterId, $cathedralNativeProfile.PopulationRoot, $cathedralNativeProfile.RuntimeRoot
    }    function Start-SessionServer([int]$ListenPort, [string]$LogPrefix) {
        $commandTimingArguments = if ($CommandTimingTrace) { '--command-timing-trace true' } else { '' }
        $serverArguments = '--port {0} --protocol-version 0.2.0 --game-mode {4} --command-scheduling clock-gated --save-root "{1}/save" {2} {3} --persistence-mode {5} {6}' -f $ListenPort, $SessionDirectory, $acceptanceArguments, $overlayArguments, $serverGameMode, $PersistenceMode, $commandTimingArguments
        $stdoutPath = Join-Path $captureRoot ($LogPrefix + '.stdout.log')
        $stderrPath = Join-Path $captureRoot ($LogPrefix + '.stderr.log')
        $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        try {
            # Retain the native handle before startup polling can observe an
            # exit. Terminal evidence must come from this owned handle, never
            # a PID lookup after termination or a synthesized exit code.
            $null = $serverProcess.Handle
            $actualPort = $null
            $contentHash = $null
            $rulesetHash = $null
            $gameMode = $null
            $commandScheduling = $null
            $persistence = $null
            for ($attempt = 0; $attempt -lt 120; $attempt++) {
                if ($serverProcess.HasExited) { throw "Authoritative server exited during startup. See $stderrPath" }
                $output = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
                if ($output -match 'listening on [^:]+:(\d+)') { $actualPort = [int]$Matches[1] }
                if ($output -match 'Content manifest: ([0-9a-fA-F]{64})') { $contentHash = $Matches[1] }
                if ($output -match 'Ruleset identity: ([0-9a-fA-F]{64})') { $rulesetHash = $Matches[1] }
                if ($output -match 'Game mode: (\w+)') { $gameMode = $Matches[1] }
                if ($output -match 'Command scheduling: ([a-z-]+)') { $commandScheduling = $Matches[1] }
                if ($output -match 'Persistence: (complete cohort|legacy per-session)') { $persistence = $Matches[1] }
                if ($actualPort -and $contentHash -and $rulesetHash -and $gameMode -and $commandScheduling -and $persistence) { break }
                Start-Sleep -Milliseconds 250
            }
            if (-not ($actualPort -and $contentHash -and $rulesetHash -and $gameMode -and $commandScheduling -and $persistence)) {
                throw "Timed out waiting for the authoritative server identity. See $stdoutPath and $stderrPath"
            }
            if ($gameMode -ne $expectedGameMode) { throw "Unexpected server mode: $gameMode" }
            if ($commandScheduling -ne 'clock-gated') { throw "Unexpected command scheduling: $commandScheduling" }
            $expectedPersistence = if ($PersistenceMode -eq 'cohort') { 'complete cohort' } else { 'legacy per-session' }
            if ($persistence -cne $expectedPersistence) { throw "Unexpected server persistence mode: $persistence" }
            if ($cathedral -and $contentHash -cne $cathedralProfile.Metadata.ContentManifestHash) { throw 'The Cathedral host loaded a different content identity than the compiled profile.' }
            if ($cathedralDoors -and $contentHash -cne $cathedralDoorProfile.Metadata.ContentManifestHash) { throw 'The Cathedral door host loaded a different content identity than the compiled profile.' }
            if ($cathedralNative -and $contentHash -cne $cathedralNativeProfile.Metadata.ContentManifestHash) { throw 'The native Cathedral host loaded a different content identity than the pinned profile.' }
            return [pscustomobject]@{ Process = $serverProcess; Port = $actualPort; ContentHash = $contentHash; RulesetHash = $rulesetHash; Mode = $gameMode; Persistence = $persistence }
        } catch {
            if ($null -ne $serverProcess) {
                try {
                    Stop-OwnedLocalProcess $serverProcess
                } catch { Write-Warning "Could not stop the failed server process: $($_.Exception.Message)" }
                try { $serverProcess.Dispose() }
                catch { Write-Warning "Could not dispose the failed server process: $($_.Exception.Message)" }
            }
            throw
        }
    }
    function Wait-SceneEvidence([string]$Name) {
        $path = Join-Path $SessionDirectory $Name
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $path)) {
            if ($godotProcess.HasExited) { throw "Godot exited before emitting $Name. See $godotLogPath" }
            if ($deadline.Elapsed.TotalSeconds -ge 30) { throw "Godot did not emit $Name. See $godotLogPath" }
            Start-Sleep -Milliseconds 100
        }
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    function Wait-SceneEvidencePath([string]$Path, [string]$Description) {
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
            if ($serverProcess.HasExited) { throw "The authoritative host exited before $Description." }
            foreach ($startedClient in $startedClients) {
                if ($startedClient.Process.HasExited) { throw "A Godot client exited before $Description. See $($startedClient.Log)" }
            }
            if ($deadline.Elapsed.TotalSeconds -ge 30) { throw "Timed out waiting for ${Description}: $Path" }
            Start-Sleep -Milliseconds 100
        }
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    $startedServer = Start-SessionServer $Port 'server'
    $serverProcess = $startedServer.Process
    $actualPort = $startedServer.Port
    $contentHash = $startedServer.ContentHash
    $rulesetHash = $startedServer.RulesetHash

    # Clear inherited session/test settings, then supply this launch's identity.
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    $env:DEVILUTION_SERVER_HOST = '127.0.0.1'
    $env:DEVILUTION_SERVER_PORT = [string]$actualPort
    $env:DEVILUTION_PROTOCOL_VERSION = '0.2.0'
    $env:DEVILUTION_CONTENT_HASH = $contentHash
    $env:DEVILUTION_RULESET_HASH = $rulesetHash
    $env:GODEVILUTION_PERSISTENCE_MODE = $PersistenceMode
    function Start-SceneClient([int]$Slot) {
        $clientDirectory = if ($Multiplayer) { Join-Path $SessionDirectory "player-$Slot" } else { $SessionDirectory }
        New-Item -ItemType Directory -Path $clientDirectory -Force | Out-Null
        $clientLog = Join-Path $captureRoot $(if ($Multiplayer) { "godot-player-$Slot.log" } else { 'godot.log' })
        # Each process gets isolated engine storage and its own role/bookmark environment.
        $clientEnvironment = @{
            DEVILUTION_RESUME_TOKEN_PATH = Join-Path $clientDirectory 'resume-token.txt'
            GODEVILUTION_PERSISTENCE_MODE = $PersistenceMode
        }
        if ($PortalPreviewContent) { $clientEnvironment['GODEVILUTION_PORTAL_PREVIEW'] = '1' }
        if ($CommandTimingTrace) { $clientEnvironment['GODEVILUTION_COMMAND_TIMING_TRACE'] = '1' }
        if ($LifecyclePreviewContent) { $clientEnvironment['GODEVILUTION_LIFECYCLE_PREVIEW'] = '1' }
        if ($cathedralDepths) { $clientEnvironment['GODEVILUTION_CATHEDRAL_DEPTH_PROFILE'] = $cathedralProfile.MetadataPath }
        elseif ($cathedral) { $clientEnvironment['GODEVILUTION_CATHEDRAL_PROFILE'] = $cathedralProfile.MetadataPath }
        elseif ($cathedralDoors) { $clientEnvironment['GODEVILUTION_CATHEDRAL_DOOR_PROFILE'] = $cathedralDoorProfile.MetadataPath }
        if ($cathedralNativePhase) { $clientEnvironment['GODEVILUTION_CATHEDRAL_NATIVE_PROFILE'] = $cathedralNativeProfile.MetadataPath }
        if ($cathedralNative) { $clientEnvironment['DEVILUTION_CHARACTER_CLASS_ID'] = '1' }
        if ($nativeDepthEntryContinuation) {
            $clientEnvironment['GODEVILUTION_NATIVE_DEPTH_ENTRY_BASELINE'] = $NativeDepthEntryBaselinePath
        }
        if ($CharacterClassId -ge 0) { $clientEnvironment['DEVILUTION_CHARACTER_CLASS_ID'] = [string]$CharacterClassId }
        if (-not [string]::IsNullOrEmpty($CharacterName)) { $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = $CharacterName }
        if ($Multiplayer) {
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = [string]$Slot
            $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = if ($CharacterName) { "$CharacterName $Slot" } else { "Hero $Slot" }
        }
        if ($performance) {
            $clientEnvironment['GODEVILUTION_CLIENT_ASSEMBLY_IMAGE_DIRECTORY'] = Join-Path $clientProject '.godot/mono/temp/bin/Debug'
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = [string]$Slot
            $clientEnvironment['GODEVILUTION_PERFORMANCE_PARTICIPANTS'] = [string]$clientCount
            $clientEnvironment['GODEVILUTION_PERFORMANCE_SHARED_DIRECTORY'] = $SessionDirectory
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT'] = Join-Path $clientDirectory 'frame-measurement'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS'] = '0'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS'] = '60'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES'] = '4096'
        }
        if ($performanceResume) {
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = '1'
            $clientEnvironment['GODEVILUTION_PERFORMANCE_PARTICIPANTS'] = '1'
            $clientEnvironment['GODEVILUTION_PERFORMANCE_RESUME_BASELINE_PATH'] = $PerformanceResumeBaselinePath
        }
        $godotArguments = '--path "{0}" --log-file "{1}"' -f $clientProject, $clientLog
        if ($Headless) { $godotArguments += ' --headless' }
        if ($performance) { $godotArguments += ' --max-fps 60' }
        $clientEvidence = $null
        if ($AcceptancePhase) {
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_PHASE'] = $AcceptancePhase
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_DIRECTORY'] = $clientDirectory
            $godotArguments += ' --scene res://Acceptance/SessionAcceptance.tscn'
            $evidenceFileName = switch ($AcceptancePhase) {
                'PerformanceCreate' { 'performance.json' }
                'PerformanceResume' { 'performance-resume.json' }
                default { $AcceptancePhase.ToLowerInvariant() + '.json' }
            }
            $clientEvidence = Join-Path $clientDirectory $evidenceFileName
            if (Test-Path -LiteralPath $clientEvidence) { throw "Use a fresh acceptance directory; evidence already exists at $clientEvidence" }
            if ($Multiplayer -and (Get-ChildItem -LiteralPath $clientDirectory -Filter ($AcceptancePhase.ToLowerInvariant() + '-*.json') -File)) {
                throw 'Multiplayer acceptance requires fresh phase markers, not a partially completed run.'
            }
            if ($AcceptancePhase -eq 'Recovery') {
                foreach ($marker in @('recovery-ready.json', 'recovery-server-stopped.json', 'recovery-disconnected.json')) {
                    if (Test-Path -LiteralPath (Join-Path $clientDirectory $marker)) { throw "Recovery evidence already exists: $marker" }
                }
            }
            if ($multiplayerRecovery) {
                $recoveryPrefix = $AcceptancePhase.ToLowerInvariant()
                foreach ($marker in @("$recoveryPrefix-connected.json", "$recoveryPrefix-ready.json",
                        "$recoveryPrefix-offline-ready.json", "$recoveryPrefix-rejoined.json",
                        "$recoveryPrefix-moved.json", "$recoveryPrefix-prepared.json",
                        "$recoveryPrefix.json")) {
                    if (Test-Path -LiteralPath (Join-Path $clientDirectory $marker)) { throw "MultiplayerRecovery evidence already exists: $marker" }
                }
            }
        } elseif ($null -ne $clientRunPolicy.SceneQuitAfterSeconds) {
            # Godot --quit-after counts frames, not seconds. The scene owns the timer.
            $clientEnvironment['GODEVILUTION_QUIT_AFTER_SECONDS'] = [string]$clientRunPolicy.SceneQuitAfterSeconds
        }
        $clientTracked = New-GodotProcess ("client-$Slot") $godotArguments $clientEnvironment ([bool]$Headless) $clientRunPolicy.SuppressCrashDialog
        return [pscustomobject]@{
            Process = $clientTracked.Process
            Tracked = $clientTracked
            Log = $clientLog
            Evidence = $clientEvidence
            Directory = $clientDirectory
        }
    }
    $startedClients += Start-SceneClient 1
    $godotProcess = $startedClients[0].Process
    if ($performance) {
        for ($slot = 1; $slot -le $clientCount; $slot++) {
            $readyPath = Join-Path $SessionDirectory "performance-ready-$slot.json"
            $readyWait = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath $readyPath)) {
                if ($serverProcess.HasExited) { throw 'The performance host exited before client readiness.' }
                foreach ($startedClient in $startedClients) {
                    if ($startedClient.Process.HasExited) { throw "Performance client exited before slot $slot readiness. See $($startedClient.Log)" }
                }
                if ($readyWait.Elapsed.TotalSeconds -gt 30) { throw "Performance slot $slot did not become ready." }
                Start-Sleep -Milliseconds 100
            }
            $ready = Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
            if (-not $ready.Ready -or $ready.Slot -ne $slot -or $ready.ParticipantCount -ne $clientCount) {
                throw 'Performance client emitted invalid readiness evidence.'
            }
            if ($slot -lt $clientCount) { $startedClients += Start-SceneClient ($slot + 1) }
        }
    } else {
        if ($Multiplayer) { $startedClients += Start-SceneClient 2 }
    }
    if ($AcceptancePhase) {
        $processEvidence = @{ HostProcessId = $serverProcess.Id; HostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O'); Port = $actualPort;
            ServerBuildConfiguration = $ServerBuildConfiguration;
            PersistenceMode = $PersistenceMode; HostPersistence = $startedServer.Persistence;
            Clients = @($startedClients | ForEach-Object { @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') } }) }
        if ($cooperative -or $cathedralNativePhase) {
            # These identities were validated against the actual started host.
            $processEvidence.ContentHash = $contentHash
            $processEvidence.RulesetHash = $rulesetHash
        }
        if ($cathedral -or $cathedralDoors -or $cathedralNative) {
            if ($cathedralNative) {
                $processEvidence.ProfileHash = $cathedralNativeProfile.MetadataHash
                $processEvidence.ContentTreeHash = $cathedralNativeProfile.ContentTreeHash
                $processEvidence.PopulationTreeHash = $cathedralNativeProfile.PopulationTreeHash
                $processEvidence.RuntimeTreeHash = $cathedralNativeProfile.RuntimeTreeHash
                $processEvidence.ProfileKind = if ($cathedralDepthProgression) { 'cathedral-native-depth-progression-playtest' } elseif ($cathedralProgression) { 'cathedral-native-progression-playtest' } else { 'cathedral-native-population-playtest' }
                $processEvidence.AdapterId = $cathedralNativeProfile.Metadata.adapterId
            }            elseif ($cathedralDoors) {
                $processEvidence.ProfileHash = $cathedralDoorProfile.MetadataHash
                $processEvidence.ContentTreeHash = $cathedralDoorProfile.ContentTreeHash
                $processEvidence.ProfileKind = 'cathedral-doors-playtest'
            } else {
                $processEvidence.ProfileHash = $cathedralProfile.MetadataHash
                $processEvidence.ContentTreeHash = $cathedralProfile.ContentTreeHash
            }
        }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory ($AcceptancePhase.ToLowerInvariant() + '-processes.json')), ($processEvidence | ConvertTo-Json -Depth 4))
    }
    if ($AcceptancePhase -eq 'Recovery') {
        $ready = Wait-SceneEvidence 'recovery-ready.json'
        if (-not $ready.Ready -or $ready.Sequence -lt 1) { throw 'Invalid recovery readiness evidence.' }
        if ($serverProcess.HasExited) { throw 'The server exited before the recovery fault injection.' }
        # Kill this owned host only. Keep the original Godot process running.
        $stoppedProcessId = $serverProcess.Id
        $stoppedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O')
        $existingClientEvidence = @($startedClients | ForEach-Object {
            @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') }
        })
        Stop-OwnedLocalProcess $serverProcess
        $stoppedHostExitCode = Get-OwnedTerminalExitCode $serverProcess
        $stoppedHostEndedUtc = [DateTime]::UtcNow.ToString('O')
        # Retain terminal evidence before disposal or any fallible reconnect wait.
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-server-stopped.json'),
            (@{ Stopped = $true; ProcessId = $stoppedProcessId; StartedUtc = $stoppedHostStartedUtc;
                EndedUtc = $stoppedHostEndedUtc; ExitCode = $stoppedHostExitCode; Port = $actualPort } | ConvertTo-Json -Compress))
        $serverProcess.Dispose()
        $serverProcess = $null
        $offline = Wait-SceneEvidence 'recovery-disconnected.json'
        if (-not $offline.Passed -or $offline.Sequence -ne $ready.Sequence -or $offline.OfflineSeconds -lt 1.5) {
            throw 'Godot did not verify frozen disconnected state and disabled gameplay input.'
        }
        $restartedServer = Start-SessionServer $actualPort 'server-restarted'
        $serverProcess = $restartedServer.Process
        if ($restartedServer.Port -ne $actualPort -or $restartedServer.ContentHash -ne $contentHash -or $restartedServer.RulesetHash -ne $rulesetHash -or
            $restartedServer.Persistence -cne $startedServer.Persistence) {
            throw 'The restarted host did not preserve the original endpoint and content/ruleset identity.'
        }
        $restartEvidence = @{ PersistenceMode = $PersistenceMode; HostPersistence = $restartedServer.Persistence;
            ServerBuildConfiguration = $ServerBuildConfiguration;
            HostProcessId = $stoppedProcessId; HostStartedUtc = $stoppedHostStartedUtc; Clients = $existingClientEvidence;
            InitialHostProcessId = $stoppedProcessId; InitialHostStartedUtc = $stoppedHostStartedUtc; InitialClients = $existingClientEvidence;
            StoppedProcessId = $stoppedProcessId; StoppedHostStartedUtc = $stoppedHostStartedUtc;
            StoppedHostEndedUtc = $stoppedHostEndedUtc; StoppedHostExitCode = $stoppedHostExitCode;
            RestartedProcessId = $serverProcess.Id; RestartedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O');
            GodotProcessId = $godotProcess.Id; GodotStartedUtc = $godotProcess.StartTime.ToUniversalTime().ToString('O'); ExistingClients = $existingClientEvidence; Port = $actualPort }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-processes.json'), ($restartEvidence | ConvertTo-Json -Depth 4))
    }
    if ($multiplayerRecovery) {
        if ($PersistenceMode -cne 'cohort' -or -not $Multiplayer -or @($startedClients).Count -ne 2) {
            throw 'MultiplayerRecovery requires exactly two clients in complete-cohort mode.'
        }
        $recoveryPrefix = $AcceptancePhase.ToLowerInvariant()
        $recoveryReady = @()
        for ($slot = 1; $slot -le 2; $slot++) {
            $readyPath = Join-Path $SessionDirectory "player-$slot/$recoveryPrefix-ready.json"
            $ready = Wait-SceneEvidencePath $readyPath "multiplayer recovery player $slot readiness"
            if (-not $ready.Ready -or $ready.Slot -ne $slot -or $ready.EntityId -lt 1 -or
                $ready.PeerEntityId -lt 1 -or [string]::IsNullOrWhiteSpace($ready.SnapshotFingerprint) -or
                -not $ready.ClockGated) {
                throw "Invalid multiplayer recovery readiness evidence for player $slot."
            }
            $recoveryReady += $ready
            if ($pendingRecovery -and ($ready.PendingStatus -cne 'Accepted' -or
                $ready.PendingRejectReason -cne 'Unspecified' -or $ready.PendingCount -ne 1)) {
                throw 'Pending recovery must stop with a committed Accepted receipt withheld from each client.'
            }
        }
        $pendingLiveBoundary = if ($pendingRecovery) {
            Assert-PendingRecoveryCanonicalBoundary (Join-Path $SessionDirectory 'save') $recoveryReady $SessionDirectory
        } else { $null }
        if ($serverProcess.HasExited) { throw 'The complete-cohort host exited before the multiplayer recovery fault injection.' }
        $stoppedProcessId = $serverProcess.Id
        $stoppedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O')
        $existingClientEvidence = @($startedClients | ForEach-Object {
            @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') }
        })
        if (@($existingClientEvidence).Count -ne 2 -or
            @($existingClientEvidence | ForEach-Object { $_.ProcessId } | Select-Object -Unique).Count -ne 2) {
            throw 'MultiplayerRecovery did not retain exactly two distinct Godot client processes.'
        }
        # Kill only this owned authoritative host; both original Godot clients
        # remain alive and must cross their own offline barrier before restart.
        Stop-OwnedLocalProcess $serverProcess
        if (-not $serverProcess.HasExited) { throw 'The multiplayer recovery host did not terminate before replacement.' }
        $stoppedHostExitCode = Get-OwnedTerminalExitCode $serverProcess
        $stoppedHostEndedUtc = [DateTime]::UtcNow.ToString('O')
        $pendingStoppedBoundary = if ($pendingRecovery) {
            Assert-PendingRecoveryCanonicalBoundary (Join-Path $SessionDirectory 'save') $recoveryReady $SessionDirectory
        } else { $null }
        $serverProcess.Dispose()
        $serverProcess = $null
        [IO.File]::WriteAllText((Join-Path $SessionDirectory "$recoveryPrefix-server-stopped.json"),
            (@{ Stopped = $true; ProcessId = $stoppedProcessId; StartedUtc = $stoppedHostStartedUtc;
                EndedUtc = $stoppedHostEndedUtc; ExitCode = $stoppedHostExitCode; Port = $actualPort } | ConvertTo-Json -Compress))
        $offlineEvidence = @()
        for ($slot = 1; $slot -le 2; $slot++) {
            $offlinePath = Join-Path $SessionDirectory "player-$slot/$recoveryPrefix-offline-ready.json"
            $offline = Wait-SceneEvidencePath $offlinePath "multiplayer recovery player $slot offline barrier"
            if (-not $offline.Passed -or $offline.Slot -ne $slot -or $offline.OfflineSeconds -lt 2 -or
                [string]::IsNullOrWhiteSpace($offline.SnapshotFingerprint)) {
                throw "Invalid multiplayer recovery offline evidence for player $slot."
            }
            $offlineEvidence += $offline
            if ($pendingRecovery -and ($offline.PendingCount -ne 1 -or
                $offline.PendingSequence -ne $recoveryReady[$slot - 1].PendingSequence)) {
                throw 'Pending recovery offline evidence lost or renumbered the original command.'
            }
        }
        $restartedServer = Start-SessionServer $actualPort 'server-multiplayer-recovery-restarted'
        $serverProcess = $restartedServer.Process
        if ($restartedServer.Port -ne $actualPort -or $restartedServer.ContentHash -ne $contentHash -or
            $restartedServer.RulesetHash -ne $rulesetHash -or $restartedServer.Persistence -cne 'complete cohort') {
            throw 'The multiplayer recovery host did not preserve the same port, root, persistence, or identity.'
        }
        $retainedClientEvidence = @($startedClients | ForEach-Object {
            if ($_.Process.HasExited) { throw "Godot client $($_.Process.Id) exited before multiplayer recovery reconnect evidence." }
            @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') }
        })
        if (@($retainedClientEvidence).Count -ne 2) {
            throw 'MultiplayerRecovery lost a client before host replacement evidence was captured.'
        }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory "$recoveryPrefix-server-restarted.json"),
            (@{ Restarted = $true; ProcessId = $serverProcess.Id;
                StartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O'); Port = $actualPort } | ConvertTo-Json -Compress))
        $recoveryProcessEvidence = @{ Phase = $AcceptancePhase; PersistenceMode = $PersistenceMode;
            ServerBuildConfiguration = $ServerBuildConfiguration;
            HostPersistence = $restartedServer.Persistence; Port = $actualPort;
            ContentHash = $contentHash; RulesetHash = $rulesetHash;
            HostProcessId = $processEvidence.HostProcessId; HostStartedUtc = $processEvidence.HostStartedUtc;
            InitialHostProcessId = $processEvidence.HostProcessId; InitialHostStartedUtc = $processEvidence.HostStartedUtc;
            StoppedProcessId = $stoppedProcessId; StoppedHostStartedUtc = $stoppedHostStartedUtc;
            StoppedHostEndedUtc = $stoppedHostEndedUtc; StoppedHostExitCode = $stoppedHostExitCode;
            RestartedProcessId = $serverProcess.Id; RestartedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O');
            Clients = $processEvidence.Clients; ExistingClients = $retainedClientEvidence;
            Ready = $recoveryReady; Offline = $offlineEvidence;
            PendingLiveBoundary = $pendingLiveBoundary; PendingStoppedBoundary = $pendingStoppedBoundary }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory "$recoveryPrefix-processes.json"),
            ($recoveryProcessEvidence | ConvertTo-Json -Depth 8))
    }
    $clientWait = [Diagnostics.Stopwatch]::StartNew()
    do {
        $clientsRunning = $false
        foreach ($startedClient in $startedClients) {
            if (-not $startedClient.Process.HasExited) { $clientsRunning = $true }
            else {
                Complete-GodotProcess $startedClient.Tracked 0 'Godot client'
            }
        }
        if ($clientsRunning -and $null -ne $clientRunPolicy.TimeoutSeconds -and $clientWait.Elapsed.TotalSeconds -gt $clientRunPolicy.TimeoutSeconds) {
            foreach ($startedClient in $startedClients) {
                if (-not $startedClient.Process.HasExited) {
                    $startedClient.Tracked.Record.Error = 'Godot did not finish before the launch timeout.'
                    Write-ProcessEvidence $startedClient.Tracked
                }
            }
            throw 'Godot did not finish before the launch timeout.'
        }
        if ($clientsRunning) { Start-Sleep -Milliseconds 100 }
    } while ($clientsRunning)
    if ($AcceptancePhase) {
        $acceptanceEntityIds = @()
        $acceptanceEntityIdsComplete = $true
        foreach ($startedClient in $startedClients) {
            if (-not (Test-Path -LiteralPath $startedClient.Evidence)) { throw "Godot did not emit acceptance evidence. See $($startedClient.Log)" }
            $evidence = Get-Content -LiteralPath $startedClient.Evidence -Raw | ConvertFrom-Json
            if (-not $evidence.Passed -or $evidence.Mode -ne $expectedGameMode -or $evidence.ContentHash -ne $contentHash -or $evidence.RulesetHash -ne $rulesetHash) {
                throw "Godot acceptance evidence failed identity/mode validation. See $($startedClient.Evidence)"
            }
            if ($PersistenceMode -eq 'cohort' -and -not $performance) {
                $acceptanceEntityIds += Get-CohortSceneEntityId $evidence
            } elseif ($performance -or $null -eq $evidence.EntityId) {
                $acceptanceEntityIdsComplete = $false
            } else {
                try { $acceptanceEntityIds += [uint32]$evidence.EntityId }
                catch { throw "Godot acceptance evidence has an invalid entity binding. See $($startedClient.Evidence)" }
            }
        }
        if ($performance) {
            $expectedPerformancePhase = if ($performanceCreate) { 'PerformanceCreate' } else { 'Performance' }
            $summaries = @()
            $slot = 0
            foreach ($startedClient in $startedClients) {
                $slot++
                $workload = Get-Content -LiteralPath $startedClient.Evidence -Raw | ConvertFrom-Json
                if ($workload.Phase -ne $expectedPerformancePhase -or $workload.ParticipantCount -ne $clientCount -or
                    $workload.Slot -ne $slot -or $workload.WarmupRounds -ne 5 -or $workload.MeasuredRounds -ne 15) {
                    throw 'The Godot performance workload did not complete all settled movement rounds.'
                }
                $framePath = Join-Path $startedClient.Directory 'frame-measurement/client-frame-evidence.json'
                $frames = Get-Content -LiteralPath $framePath -Raw | ConvertFrom-Json
                $frames.Configuration.RuntimeAssemblies = Assert-GodotRuntimeAssemblyEvidence $frames.Configuration.RuntimeAssemblies $runtimeAssemblyManifest
                $summary = Assert-GodotPerformanceEvidence $frames $clientCount $expectedGameMode $contentHash $rulesetHash ([bool]$Headless)
                $summaries += [pscustomobject]@{ Slot = $slot; Metrics = $summary }
            }
            $baseline = [pscustomobject]@{
                SchemaVersion = '1'; Passed = $true; ParticipantCount = $clientCount
                WarmupRounds = 5; MeasuredRounds = 15; FrameCap = 60; Headless = [bool]$Headless
                Statistic = 'Exact nearest-rank percentiles over bounded samples; not GPU/whole-frame timing'
                ProcessObservationScope = 'Own process at capture creation/stop after workload warm-up; CPU delta includes recorder/barrier overhead; heap size is from the last GC, not live allocations; peak working set is lifetime peak, not leak/soak evidence'
                EngineVersion = $engineVersion; SourceHash = $staging.SourceHash
                LaunchLogs = $captureRoot; Clients = $summaries
            }
            $baselinePath = Join-Path $SessionDirectory 'performance-baseline.json'
            if (Test-Path -LiteralPath $baselinePath) { throw 'A performance baseline must never overwrite evidence.' }
        }
        if ($PersistenceMode -eq 'cohort') {
            if ($null -ne $serverProcess -and $serverProcess.HasExited) {
                throw 'The complete-cohort host exited before its live checkpoint was verified.'
            }
            $bookmarkPaths = if ($Multiplayer) {
                @(for ($slot = 1; $slot -le $clientCount; $slot++) {
                    Join-Path $SessionDirectory "player-$slot/resume-token.txt"
                })
            } else {
                @(Join-Path $SessionDirectory 'resume-token.txt')
            }
            $entityBindings = if ($acceptanceEntityIdsComplete) { [uint32[]]$acceptanceEntityIds } else { @() }
            [void](Assert-CohortCheckpoint (Join-Path $SessionDirectory 'save') $expectedGameMode $contentHash $rulesetHash $bookmarkPaths $entityBindings)
            if ($null -ne $serverProcess -and $serverProcess.HasExited) {
                throw 'The complete-cohort host exited while its live checkpoint was being verified.'
            }
        } else {
            if (-not (Get-ChildItem -LiteralPath (Join-Path $SessionDirectory 'save') -Filter '*.session.json' -File)) {
                throw 'Godot acceptance did not produce an authoritative checkpoint.'
            }
            if ($Multiplayer) {
                if (@(Get-ChildItem -LiteralPath (Join-Path $SessionDirectory 'save') -Filter '*.session.json' -File).Count -ne $clientCount) {
                    throw 'Multiplayer acceptance must preserve one authoritative character checkpoint per client.'
                }
                $bookmarks = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                for ($slot = 1; $slot -le $clientCount; $slot++) {
                    $bookmark = [IO.File]::ReadAllText((Join-Path $SessionDirectory "player-$slot/resume-token.txt")).Trim()
                    if ([string]::IsNullOrWhiteSpace($bookmark) -or -not $bookmarks.Add($bookmark)) {
                        throw 'Multiplayer players must retain distinct private resume bookmarks.'
                    }
                }
            }
        }
        if ($performance) {
            if ($serverProcess.HasExited) { throw 'The performance host exited unexpectedly; no baseline is accepted.' }
            $baselineStream = [IO.File]::Open($baselinePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
            $baselineWriter = [IO.StreamWriter]::new($baselineStream, [Text.UTF8Encoding]::new($false))
            try { $baselineWriter.Write(($baseline | ConvertTo-Json -Depth 10)) }
            finally { $baselineWriter.Dispose() }
        }
    }
} catch {
    $launcherError = $_
    throw
} finally {
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    if ($CommandTimingTrace -and ($cathedralProgression -or $cathedralDepthProgression) -and $null -ne $serverProcess) {
        try {
            $observedHostExited = $serverProcess.HasExited
            [ordered]@{
                ProcessId = $serverProcess.Id
                StartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O')
                ObservedUtc = [DateTime]::UtcNow.ToString('O')
                HasExited = $observedHostExited
                ExitCode = $(if ($observedHostExited) { $serverProcess.ExitCode } else { $null })
                Boundary = 'before any launcher-owned process cleanup'
            } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $processEvidenceRoot 'server-before-cleanup.json') -Encoding utf8
        } catch {
            $cleanupErrors.Add("Could not retain pre-cleanup host observation: $($_.Exception.Message)")
        }
    }
    if ($null -ne $staging) {
        try {
            $errorMessage = if ($null -ne $launcherError) { $launcherError.Exception.Message } else { $null }
            Write-ProjectStageEvidence $staging $engineVersion $projectStageEvidencePath $errorMessage
        } catch {
            Write-Warning "Could not finalize project-stage evidence: $($_.Exception.Message)"
        }
    }
    foreach ($trackedProcess in @($startedClients | ForEach-Object { $_.Tracked }) + @($importTracked, $versionTracked)) {
        if ($null -ne $trackedProcess) {
            try { Stop-GodotProcess $trackedProcess }
            catch {
                $cleanupErrors.Add("Could not finalize Godot process evidence: $($_.Exception.Message)")
                Write-Warning $cleanupErrors[$cleanupErrors.Count - 1]
            }
        }
    }
    foreach ($ownedProcess in @($startedClients | ForEach-Object { $_.Process }) + @($serverProcess, $importProcess, $versionTracked.Process)) {
        if ($null -ne $ownedProcess) {
            try {
                Stop-OwnedLocalProcess $ownedProcess
            } catch {
                $cleanupErrors.Add("Could not stop owned process $($ownedProcess.Id): $($_.Exception.Message)")
                Write-Warning $cleanupErrors[$cleanupErrors.Count - 1]
            }
            try { $ownedProcess.Dispose() }
            catch {
                $cleanupErrors.Add("Could not dispose owned process: $($_.Exception.Message)")
                Write-Warning $cleanupErrors[$cleanupErrors.Count - 1]
            }
        }
    }
    if ($null -ne $sessionLock) {
        try { $sessionLock.Dispose() }
        catch { Write-Warning "Could not dispose the session lock: $($_.Exception.Message)" }
    }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Write-Host "Session files: $SessionDirectory"
    Write-Host "Launch logs: $captureRoot"
    Assert-LocalLauncherCleanup ($null -ne $launcherError) $cleanupErrors.ToArray()
}

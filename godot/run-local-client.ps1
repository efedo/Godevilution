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
    [ValidateSet(1, 4)] [int]$PerformanceParticipants = 1,
    [ValidateSet('Create', 'Resume', 'Recovery', 'Journey', 'JourneyResume', 'TownLoop', 'TownLoopResume', 'Lifecycle', 'LifecycleResume', 'MultiplayerCreate', 'MultiplayerResume', 'MultiplayerRecovery', 'Performance', 'CathedralJourney', 'CathedralResume', 'CathedralDepthJourney', 'CathedralDepthResume')] [string]$AcceptancePhase
)

$ErrorActionPreference = 'Stop'
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
$performance = $AcceptancePhase -eq 'Performance'
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
if ($cathedralDepths -and -not [string]::IsNullOrWhiteSpace($CathedralProfileDirectory)) {
    throw 'Choose either the L1 or depths 1-4 Cathedral profile, never both.'
}
$cathedral = $cathedralDepths -or -not [string]::IsNullOrWhiteSpace($CathedralProfileDirectory)
if ($cathedral -and ($PreviewContent -or $PortalPreviewContent -or $LifecyclePreviewContent -or $Multiplayer -or $performance)) {
    throw 'Cathedral playtests require their own single-player content and save profile.'
}
if (($AcceptancePhase -in @('CathedralJourney', 'CathedralResume')) -and (-not $cathedral -or $cathedralDepths)) {
    throw 'Cathedral acceptance requires an explicit compiled -CathedralProfileDirectory.'
}
if (($AcceptancePhase -in @('CathedralDepthJourney', 'CathedralDepthResume')) -and -not $cathedralDepths) {
    throw 'Cathedral depth acceptance requires an explicit compiled -CathedralDepthProfileDirectory.'
}
$cathedralPhases = if ($cathedralDepths) { @('CathedralDepthJourney', 'CathedralDepthResume') } else { @('CathedralJourney', 'CathedralResume') }
if ($cathedral -and $AcceptancePhase -and $AcceptancePhase -notin $cathedralPhases) {
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
if ($Multiplayer -and ($PreviewContent -or $PortalPreviewContent)) { throw 'The local multiplayer profile uses base content, not the single-player preview.' }
if ($AcceptancePhase -and -not $performance -and ($Multiplayer -ne ($AcceptancePhase -in @('MultiplayerCreate', 'MultiplayerResume', 'MultiplayerRecovery')))) {
    throw 'Multiplayer acceptance phases require -Multiplayer; single-player phases cannot use it.'
}
if ($AcceptancePhase -eq 'MultiplayerRecovery' -and $PersistenceMode -cne 'cohort') {
    throw 'MultiplayerRecovery requires the explicit complete-cohort persistence mode.'
}
$expectedGameMode = if ($Multiplayer) { 'Multiplayer' } else { 'SinglePlayer' }
$serverGameMode = if ($Multiplayer) { 'multiplayer' } else { 'single-player' }
if ($PersistenceMode -eq 'cohort' -and [string]::IsNullOrWhiteSpace($SessionDirectory)) {
    throw 'Cohort persistence requires an explicit nonempty -SessionDirectory; it never adopts a legacy default profile.'
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientSourceProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
$clientProject = $clientSourceProject
if ([string]::IsNullOrWhiteSpace($SessionDirectory)) {
    $SessionDirectory = if ($cathedral) {
        $profileDirectory = if ($cathedralDepths) { $CathedralDepthProfileDirectory } else { $CathedralProfileDirectory }
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
    'GODEVILUTION_PERSISTENCE_MODE',
    'GODEVILUTION_LOCAL_PLAYER', 'GODEVILUTION_PORTAL_PREVIEW', 'GODEVILUTION_LIFECYCLE_PREVIEW', 'GODEVILUTION_CATHEDRAL_PROFILE', 'GODEVILUTION_CATHEDRAL_DEPTH_PROFILE',
    'GODEVILUTION_PERFORMANCE_PARTICIPANTS', 'GODEVILUTION_PERFORMANCE_SHARED_DIRECTORY',
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
    $publicConfiguration = Select-PerformancePublicFields $configuration @(
        'ClientBuildId', 'ProtocolSchemaVersion', 'ContentManifestHash', 'RulesetIdentityHash',
        'Renderer', 'Device', 'Viewport', 'VsyncEnabled', 'FrameCap', 'Headless', 'EngineVersion',
        'ParticipantCount', 'RenderingDriver', 'DisplayServer', 'OperatingSystem', 'OperatingSystemVersion',
        'OperatingSystemFeatures', 'VsyncMode', 'Runtime', 'RuntimeOperatingSystem', 'Architecture',
        'BuildConfiguration', 'WindowPosition', 'WindowMode', 'WindowCanDraw', 'WindowFocusedAtStart')
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
        if ($AcceptancePhase -eq 'MultiplayerRecovery' -and @($participant.CommandReceipts).Count -eq 0) {
            throw 'MultiplayerRecovery requires a nonempty canonical receipt ledger for every participant.'
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

    if ($AcceptancePhase -eq 'MultiplayerRecovery') {
        foreach ($bookmarkPath in $bookmarks) {
            $bookmark = [IO.File]::ReadAllText($bookmarkPath).Trim()
            $sessionId = Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($bookmark))
            $participant = @($participants | Where-Object { $_.SessionId -ceq $sessionId })
            $evidencePath = Join-Path (Split-Path $bookmarkPath -Parent) 'multiplayerrecovery.json'
            if ($participant.Count -ne 1 -or -not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
                throw 'MultiplayerRecovery lacks the bookmark-bound final command evidence.'
            }
            $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
            Assert-CohortRecoveryReceipt $participant[0] $evidence
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

try {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    $cathedralProfile = if ($cathedralDepths) {
        Get-CathedralPlaytestProfile $CathedralDepthProfileDirectory $SessionDirectory $true
    } elseif ($cathedral) { Get-CathedralPlaytestProfile $CathedralProfileDirectory $SessionDirectory } else { $null }
    New-Item -ItemType Directory -Path $SessionDirectory -Force | Out-Null
    if ($performance -and (Get-ChildItem -LiteralPath $SessionDirectory -Force)) {
        throw 'Performance acceptance requires a fresh empty session directory.'
    }
    # One local writer per save directory; parallel tests use distinct directories.
    $sessionLock = [System.IO.File]::Open((Join-Path $SessionDirectory 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    if ($AcceptancePhase -eq 'MultiplayerRecovery') {
        foreach ($marker in @('multiplayerrecovery-server-stopped.json', 'multiplayerrecovery-server-restarted.json',
                'multiplayerrecovery-processes.json')) {
            if (Test-Path -LiteralPath (Join-Path $SessionDirectory $marker)) {
                throw "MultiplayerRecovery requires a fresh dedicated root; stale marker exists: $marker"
            }
        }
    }
    if ($cathedral) { Initialize-CathedralPlaytestSession $cathedralProfile $SessionDirectory }
    $staging = New-IsolatedGodotProject $clientSourceProject (Join-Path $captureRoot 'godot/Devilution.Client')
    $clientProject = $staging.DestinationDirectory
    Write-ProjectStageEvidence $staging $null $projectStageEvidencePath $null
    $engineVersion = Get-GodotEngineVersion
    Write-ProjectStageEvidence $staging $engineVersion $projectStageEvidencePath $null
    & dotnet build $serverProject --nologo
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
    & dotnet build (Join-Path $clientProject 'Devilution.Client.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the staged Godot client.' }

    # Seed 5 keeps Butcher available under both production and native quest-pool
    # orderings. This diagnostic does not certify parity of the complete pools.
    # Existing acceptance and preview profiles retain seed 0.
    $acceptanceArguments = if ($LifecyclePreviewContent) { '--starting-level 0 --random-seed 5' } elseif ($AcceptancePhase -or $PreviewContent -or $PortalPreviewContent -or $Multiplayer -or $cathedral) { '--starting-level 0 --random-seed 0' } else { '' }
    $overlayArguments = if ($PreviewContent -or $PortalPreviewContent) { '--content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-loop-preview') } elseif ($LifecyclePreviewContent) { '--content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-lifecycle-preview') } else { '' }
    if ($PortalPreviewContent) {
        $overlayArguments += ' --content-overlay "{0}"' -f (Join-Path $repositoryRoot 'server/content/town-portal-preview')
    }
    if ($cathedral) {
        $overlayArguments = '--content-root "{0}" --content-id {1} --content-version {2} --game-adapter {3}' -f $cathedralProfile.ContentRoot, $cathedralProfile.Metadata.ContentId, $cathedralProfile.Metadata.ContentVersion, $cathedralProfile.Metadata.AdapterId
    }
    function Start-SessionServer([int]$ListenPort, [string]$LogPrefix) {
        $serverArguments = '--port {0} --protocol-version 0.2.0 --game-mode {4} --command-scheduling clock-gated --save-root "{1}/save" {2} {3} --persistence-mode {5}' -f $ListenPort, $SessionDirectory, $acceptanceArguments, $overlayArguments, $serverGameMode, $PersistenceMode
        $stdoutPath = Join-Path $captureRoot ($LogPrefix + '.stdout.log')
        $stderrPath = Join-Path $captureRoot ($LogPrefix + '.stderr.log')
        $serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
        try {
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
        if ($LifecyclePreviewContent) { $clientEnvironment['GODEVILUTION_LIFECYCLE_PREVIEW'] = '1' }
        if ($cathedralDepths) { $clientEnvironment['GODEVILUTION_CATHEDRAL_DEPTH_PROFILE'] = $cathedralProfile.MetadataPath }
        elseif ($cathedral) { $clientEnvironment['GODEVILUTION_CATHEDRAL_PROFILE'] = $cathedralProfile.MetadataPath }
        if ($CharacterClassId -ge 0) { $clientEnvironment['DEVILUTION_CHARACTER_CLASS_ID'] = [string]$CharacterClassId }
        if (-not [string]::IsNullOrEmpty($CharacterName)) { $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = $CharacterName }
        if ($Multiplayer) {
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = [string]$Slot
            $clientEnvironment['DEVILUTION_CHARACTER_NAME'] = if ($CharacterName) { "$CharacterName $Slot" } else { "Hero $Slot" }
        }
        if ($performance) {
            $clientEnvironment['GODEVILUTION_LOCAL_PLAYER'] = [string]$Slot
            $clientEnvironment['GODEVILUTION_PERFORMANCE_PARTICIPANTS'] = [string]$clientCount
            $clientEnvironment['GODEVILUTION_PERFORMANCE_SHARED_DIRECTORY'] = $SessionDirectory
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_OUTPUT'] = Join-Path $clientDirectory 'frame-measurement'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_WARMUP_SECONDS'] = '0'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_DURATION_SECONDS'] = '60'
            $clientEnvironment['GODEVILUTION_CLIENT_PERFORMANCE_MAX_SAMPLES'] = '4096'
        }
        $godotArguments = '--path "{0}" --log-file "{1}"' -f $clientProject, $clientLog
        if ($Headless) { $godotArguments += ' --headless' }
        if ($performance) { $godotArguments += ' --max-fps 60' }
        $clientEvidence = $null
        if ($AcceptancePhase) {
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_PHASE'] = $AcceptancePhase
            $clientEnvironment['GODEVILUTION_ACCEPTANCE_DIRECTORY'] = $clientDirectory
            $godotArguments += ' --scene res://Acceptance/SessionAcceptance.tscn'
            $clientEvidence = Join-Path $clientDirectory ($AcceptancePhase.ToLowerInvariant() + '.json')
            if (Test-Path -LiteralPath $clientEvidence) { throw "Use a fresh acceptance directory; evidence already exists at $clientEvidence" }
            if ($Multiplayer -and (Get-ChildItem -LiteralPath $clientDirectory -Filter ($AcceptancePhase.ToLowerInvariant() + '-*.json') -File)) {
                throw 'Multiplayer acceptance requires fresh phase markers, not a partially completed run.'
            }
            if ($AcceptancePhase -eq 'Recovery') {
                foreach ($marker in @('recovery-ready.json', 'recovery-server-stopped.json', 'recovery-disconnected.json')) {
                    if (Test-Path -LiteralPath (Join-Path $clientDirectory $marker)) { throw "Recovery evidence already exists: $marker" }
                }
            }
            if ($AcceptancePhase -eq 'MultiplayerRecovery') {
                foreach ($marker in @('multiplayerrecovery-connected.json', 'multiplayerrecovery-ready.json',
                        'multiplayerrecovery-offline-ready.json', 'multiplayerrecovery-rejoined.json',
                        'multiplayerrecovery-moved.json',
                        'multiplayerrecovery.json')) {
                    if (Test-Path -LiteralPath (Join-Path $clientDirectory $marker)) { throw "MultiplayerRecovery evidence already exists: $marker" }
                }
            }
        } elseif ($Headless) {
            # Godot --quit-after counts frames, not seconds. The scene owns the timer.
            $clientEnvironment['GODEVILUTION_QUIT_AFTER_SECONDS'] = [string]$QuitAfterSeconds
        }
        $clientTracked = New-GodotProcess ("client-$Slot") $godotArguments $clientEnvironment ([bool]$Headless) ([bool]($Headless -or $AcceptancePhase))
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
            PersistenceMode = $PersistenceMode; HostPersistence = $startedServer.Persistence;
            Clients = @($startedClients | ForEach-Object { @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') } }) }
        if ($cathedral) {
            $processEvidence.ProfileHash = $cathedralProfile.MetadataHash
            $processEvidence.ContentTreeHash = $cathedralProfile.ContentTreeHash
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
        $serverProcess.Dispose()
        $serverProcess = $null
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-server-stopped.json'), '{"Stopped":true}')
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
            HostProcessId = $stoppedProcessId; HostStartedUtc = $stoppedHostStartedUtc; Clients = $existingClientEvidence;
            InitialHostProcessId = $stoppedProcessId; InitialHostStartedUtc = $stoppedHostStartedUtc; InitialClients = $existingClientEvidence;
            StoppedProcessId = $stoppedProcessId; StoppedHostStartedUtc = $stoppedHostStartedUtc;
            RestartedProcessId = $serverProcess.Id; RestartedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O');
            GodotProcessId = $godotProcess.Id; GodotStartedUtc = $godotProcess.StartTime.ToUniversalTime().ToString('O'); ExistingClients = $existingClientEvidence; Port = $actualPort }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'recovery-processes.json'), ($restartEvidence | ConvertTo-Json -Depth 4))
    }
    if ($AcceptancePhase -eq 'MultiplayerRecovery') {
        if ($PersistenceMode -cne 'cohort' -or -not $Multiplayer -or @($startedClients).Count -ne 2) {
            throw 'MultiplayerRecovery requires exactly two clients in complete-cohort mode.'
        }
        $recoveryPrefix = 'multiplayerrecovery'
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
        }
        if ($serverProcess.HasExited) { throw 'The complete-cohort host exited before the multiplayer recovery fault injection.' }
        $stoppedProcessId = $serverProcess.Id
        $stoppedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O')
        $existingClientEvidence = @($startedClients | ForEach-Object {
            @{ ProcessId = $_.Process.Id; StartedUtc = $_.Process.StartTime.ToUniversalTime().ToString('O') }
        })
        if (@($existingClientEvidence).Count -ne 2 -or
            @($existingClientEvidence | Select-Object -ExpandProperty ProcessId -Unique).Count -ne 2) {
            throw 'MultiplayerRecovery did not retain exactly two distinct Godot client processes.'
        }
        # Kill only this owned authoritative host; both original Godot clients
        # remain alive and must cross their own offline barrier before restart.
        Stop-OwnedLocalProcess $serverProcess
        if (-not $serverProcess.HasExited) { throw 'The multiplayer recovery host did not terminate before replacement.' }
        $stoppedHostExitCode = $serverProcess.ExitCode
        $stoppedHostEndedUtc = [DateTime]::UtcNow.ToString('O')
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
            HostPersistence = $restartedServer.Persistence; Port = $actualPort;
            ContentHash = $contentHash; RulesetHash = $rulesetHash;
            HostProcessId = $processEvidence.HostProcessId; HostStartedUtc = $processEvidence.HostStartedUtc;
            InitialHostProcessId = $processEvidence.HostProcessId; InitialHostStartedUtc = $processEvidence.HostStartedUtc;
            StoppedProcessId = $stoppedProcessId; StoppedHostStartedUtc = $stoppedHostStartedUtc;
            StoppedHostEndedUtc = $stoppedHostEndedUtc; StoppedHostExitCode = $stoppedHostExitCode;
            RestartedProcessId = $serverProcess.Id; RestartedHostStartedUtc = $serverProcess.StartTime.ToUniversalTime().ToString('O');
            Clients = $processEvidence.Clients; ExistingClients = $retainedClientEvidence;
            Ready = $recoveryReady; Offline = $offlineEvidence }
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
        if ($clientsRunning -and ($Headless -or $AcceptancePhase) -and $clientWait.Elapsed.TotalSeconds -gt ($QuitAfterSeconds + 15)) {
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
            $summaries = @()
            $slot = 0
            foreach ($startedClient in $startedClients) {
                $slot++
                $workload = Get-Content -LiteralPath $startedClient.Evidence -Raw | ConvertFrom-Json
                if ($workload.Phase -ne 'Performance' -or $workload.ParticipantCount -ne $clientCount -or
                    $workload.Slot -ne $slot -or $workload.WarmupRounds -ne 5 -or $workload.MeasuredRounds -ne 15) {
                    throw 'The Godot performance workload did not complete all settled movement rounds.'
                }
                $framePath = Join-Path $startedClient.Directory 'frame-measurement/client-frame-evidence.json'
                $frames = Get-Content -LiteralPath $framePath -Raw | ConvertFrom-Json
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
            catch { Write-Warning "Could not finalize Godot process evidence: $($_.Exception.Message)" }
        }
    }
    foreach ($ownedProcess in @($startedClients | ForEach-Object { $_.Process }) + @($serverProcess, $importProcess, $versionTracked.Process)) {
        if ($null -ne $ownedProcess) {
            try {
                Stop-OwnedLocalProcess $ownedProcess
            } catch { Write-Warning "Could not stop owned process $($ownedProcess.Id): $($_.Exception.Message)" }
            try { $ownedProcess.Dispose() }
            catch { Write-Warning "Could not dispose owned process: $($_.Exception.Message)" }
        }
    }
    if ($null -ne $sessionLock) {
        try { $sessionLock.Dispose() }
        catch { Write-Warning "Could not dispose the session lock: $($_.Exception.Message)" }
    }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Write-Host "Session files: $SessionDirectory"
    Write-Host "Launch logs: $captureRoot"
}

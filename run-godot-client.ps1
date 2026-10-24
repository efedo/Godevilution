[CmdletBinding()]
param(
    # DIABDAT.MPQ to use; remembered for later launches. Otherwise it is found automatically.
    [string]$DiabloArchive,
    # The native Town profiles are class-bound until batch 1.2.
    [ValidateSet('warrior', 'sorcerer')] [string]$NativeClass = 'warrior',
    # Skip native setup and start the authored content.
    [switch]$AuthoredContent,
    # Build or reuse the native setup and print the launch configuration without starting the game.
    [switch]$SetupOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$godotLauncher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$setupRoot = Join-Path $repositoryRoot '.local/godot/native'
$packVariables = [ordered]@{
    playerPack = 'GODEVILUTION_NATIVE_PLAYER_PACK'
    scenePack = 'GODEVILUTION_NATIVE_SCENE_PACK'
    townerPack = 'GODEVILUTION_NATIVE_TOWNER_PACK'
    objectPack = 'GODEVILUTION_NATIVE_OBJECT_PACK'
}

if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
if ($AuthoredContent -and ($SetupOnly -or $DiabloArchive)) {
    throw '-AuthoredContent cannot be combined with native setup options.'
}

$launchArguments = @{}
$packs = @{}
if (-not $AuthoredContent) {
    # Native.Setup builds the profiles and art once per input fingerprint and reuses them afterwards.
    $setupArguments = @('run', '--project', (Join-Path $repositoryRoot 'tools/Native.Setup/Native.Setup.csproj'), '-c', 'Release', '--',
        'ensure', '--repository-root', $repositoryRoot, '--setup-root', $setupRoot)
    if ($DiabloArchive) { $setupArguments += @('--archive', [IO.Path]::GetFullPath($DiabloArchive)) }
    & dotnet @setupArguments
    $setupExit = $LASTEXITCODE
    if ($setupExit -eq 3) {
        if ($SetupOnly) { exit 3 }
        Write-Host 'Starting the authored content instead.'
    } elseif ($setupExit -ne 0) {
        throw "Native setup failed with exit $setupExit."
    } else {
        $state = Get-Content -LiteralPath (Join-Path $setupRoot 'current.json') -Raw | ConvertFrom-Json
        $nativeProfile = @($state.profiles | Where-Object { $_.classSymbol -ceq $NativeClass })
        if ($nativeProfile.Count -ne 1) { throw "Native setup has no $NativeClass profile." }
        foreach ($name in $packVariables.Keys) { $packs[$packVariables[$name]] = [string]$state.$name }
        # Saves follow the profile content: an unchanged profile keeps its save across rebuilds.
        $sessionDirectory = Join-Path $setupRoot ('sessions/' + $NativeClass + '-' + $nativeProfile[0].contentManifestHash.Substring(0, 16))
        $launchArguments = @{
            CathedralNativeTownProfileDirectory = $nativeProfile[0].directory
            PersistenceMode = 'cohort'
            SessionDirectory = $sessionDirectory
        }
        if ($SetupOnly) {
            [ordered]@{
                ProfileDirectory = $nativeProfile[0].directory
                SessionDirectory = $sessionDirectory
                Packs = $packs
            } | ConvertTo-Json -Depth 4
            exit 0
        }
    }
}

$previous = @{}
foreach ($variable in $packVariables.Values) { $previous[$variable] = [Environment]::GetEnvironmentVariable($variable) }
try {
    foreach ($variable in $packs.Keys) { [Environment]::SetEnvironmentVariable($variable, $packs[$variable]) }
    & $godotLauncher @launchArguments
    $exitCode = $LASTEXITCODE
} finally {
    foreach ($variable in $packVariables.Values) { [Environment]::SetEnvironmentVariable($variable, $previous[$variable]) }
}
exit $exitCode

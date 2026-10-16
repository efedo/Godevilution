# Explicit native Town/L1/L2 profile admission. Loaded only by run-local-client.ps1.
function Get-CathedralNativeProgressionProfile([string]$Directory, [string]$Session) {
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    if ($root.Contains('"') -or $sessionRoot.Contains('"') -or
        -not $sessionRoot.StartsWith((Join-Path $root 'sessions').TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Native progression requires a separate session child under its compiled profile sessions directory.'
    }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot
    $metadataPath = Join-Path $root 'cathedral-native-progression-playtest.json'
    Assert-NoCathedralReparsePoint $metadataPath
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($metadata.formatVersion -ne 2 -or
        $metadata.adapterId -cne 'devilution.diablo.cathedral-native-progression.v1' -or
        $metadata.populationPolicy -cne 'original-diablo-single-player-normal-rogue-town-l1-l2' -or
        $metadata.nativeDepth -ne 2 -or $metadata.nativePopulation -isnot [bool] -or -not $metadata.nativePopulation -or
        $metadata.nativePopulationClaim -isnot [bool] -or $metadata.nativePopulationClaim -or
        $metadata.contentDirectory -cne 'content' -or $metadata.contentId -cne 'godevilution.cathedral-doors-playtest' -or
        $metadata.contentVersion -cne '0.1.0' -or $metadata.populationDirectory -cne 'native_cathedral_population' -or
        $metadata.runtimeDirectory -cne 'native_cathedral_runtime' -or
        $metadata.heroClassSymbol -cne 'rogue' -or $metadata.heroBaseLifeMaximum -ne 2880 -or
        $metadata.heroBaseManaMaximum -ne 1440 -or $metadata.heroLightRadius -ne 8 -or $metadata.heroIdleFrames -ne 8 -or
        $metadata.entry -cne 'main' -or $metadata.placementPolicy -cne 'none' -or
        $metadata.dungeonSeed -ne 2588 -or $metadata.storySeed -ne 324508639 -or
        $metadata.preEntryFutureX -ne 57 -or $metadata.preEntryFutureY -ne 40 -or
        $metadata.preEntryLevelChanging -isnot [bool] -or -not $metadata.preEntryLevelChanging -or
        $metadata.preEntryLevelLoading -isnot [bool] -or -not $metadata.preEntryLevelLoading -or
        [string]::Join(',', @($metadata.depthSeeds)) -cne '2588,2588,1,4294967295' -or
        @($metadata.levels).Count -ne 4 -or @($metadata.routes).Count -ne 8 -or
        @($metadata.doorMappings).Count -ne 4 -or $metadata.doorProfileId -cne 'original-diablo.cathedral-doors' -or
        $metadata.doorProfileRevision -ne 1) {
        throw 'The profile does not select the explicit native Town/L1/L2 Rogue progression contract.'
    }
    foreach ($name in @('contentManifestHash', 'packFileHash', 'populationPackFileHash', 'runtimePackFileHash',
            'populationMetadataFingerprint', 'populationMetadataManifestHash', 'monsterRuntimeFingerprint',
            'inventoryFootprintCanonicalSha256', 'itemSemanticsCanonicalSha256', 'doorCatalogFingerprint')) {
        if ($metadata.$name -isnot [string] -or $metadata.$name -cnotmatch '^[0-9A-Fa-f]{64}$') {
            throw "Native progression metadata has an invalid $name identity."
        }
    }
    $allowed = @('cathedral-native-progression-playtest.json', 'content', 'native_cathedral_population', 'native_cathedral_runtime', 'sessions')
    foreach ($entry in Get-ChildItem -LiteralPath $root -Force -ErrorAction Stop) {
        if ($entry.Name -notin $allowed -or $entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The compiled native progression root contains an unexpected entry.'
        }
    }
    $contentRoot = Join-Path $root 'content'
    $populationRoot = Join-Path $root 'native_cathedral_population'
    $runtimeRoot = Join-Path $root 'native_cathedral_runtime'
    foreach ($path in @($contentRoot, $populationRoot, $runtimeRoot)) { Assert-NoCathedralReparsePoint $path }
    $contentHash = Get-CathedralContentTreeHash $contentRoot
    $populationHash = Get-CathedralContentTreeHash $populationRoot
    $runtimeHash = Get-CathedralContentTreeHash $runtimeRoot
    if ($contentHash -cne $metadata.packFileHash -or $populationHash -cne $metadata.populationPackFileHash -or
        $runtimeHash -cne $metadata.runtimePackFileHash) {
        throw 'A compiled native progression content pack changed after publication.'
    }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ContentRoot = $contentRoot
        PopulationRoot = $populationRoot; RuntimeRoot = $runtimeRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = $contentHash; PopulationTreeHash = $populationHash; RuntimeTreeHash = $runtimeHash
    }
}

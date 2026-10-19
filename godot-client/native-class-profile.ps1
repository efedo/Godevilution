# Explicit class-bound native Town/L1-L4 profile admission. Loaded only by run-local-client.ps1.
function Get-CathedralNativeClassProfile([string]$Directory, [string]$Session) {
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    $sessionsRoot = (Join-Path $root 'sessions').TrimEnd('\', '/')
    if ($root.Contains('"') -or $sessionRoot.Contains('"') -or
        -not $sessionRoot.StartsWith($sessionsRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Class-bound native progression requires a separate session child under its compiled profile sessions directory.'
    }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot
    $metadataPath = Join-Path $root 'cathedral-native-class-progression-playtest.json'
    Assert-NoCathedralReparsePoint $metadataPath
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
        throw 'Class-bound native metadata does not match the closed reviewed schema.'
    }

    $classFacts = @{
        warrior = @{ Id = 0; Life = 4480; Mana = 640; Light = 10; Idle = 10 }
        sorcerer = @{ Id = 2; Life = 1920; Mana = 4480; Light = 10; Idle = 8 }
    }
    $symbol = [string]$metadata.heroClassSymbol
    if (-not $classFacts.ContainsKey($symbol)) { throw 'Class-bound native profile supports only Warrior or Sorcerer.' }
    $facts = $classFacts[$symbol]
    $expectedPolicy = "original-diablo-single-player-normal-$symbol-town-l1-l4"
    foreach ($name in @('formatVersion', 'nativeDepth', 'dungeonSeed', 'storySeed', 'heroBaseLifeMaximum',
            'heroBaseManaMaximum', 'heroLightRadius', 'heroIdleFrames', 'preEntryFutureX', 'preEntryFutureY', 'doorProfileRevision')) {
        if ($metadata.$name -isnot [int] -and $metadata.$name -isnot [long]) {
            throw "Class-bound native metadata has an invalid $name value."
        }
    }
    if ($metadata.formatVersion -ne 4 -or
        $metadata.adapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1' -or
        $metadata.populationPolicy -cne $expectedPolicy -or $metadata.nativeDepth -ne 4 -or
        $metadata.nativePopulation -isnot [bool] -or -not $metadata.nativePopulation -or
        $metadata.nativePopulationClaim -isnot [bool] -or $metadata.nativePopulationClaim -or
        $metadata.contentDirectory -cne 'content' -or $metadata.contentId -cne 'godevilution.cathedral-doors-playtest' -or
        $metadata.contentVersion -cne '0.1.0' -or $metadata.populationDirectory -cne 'native_cathedral_population' -or
        $metadata.runtimeDirectory -cne 'native_cathedral_runtime' -or
        $metadata.heroBaseLifeMaximum -ne $facts.Life -or $metadata.heroBaseManaMaximum -ne $facts.Mana -or
        $metadata.heroLightRadius -ne $facts.Light -or $metadata.heroIdleFrames -ne $facts.Idle -or
        $metadata.entry -cne 'main' -or $metadata.placementPolicy -cne 'none' -or
        $metadata.dungeonSeed -ne 2588 -or $metadata.storySeed -ne 324508639 -or
        $metadata.preEntryFutureX -ne 57 -or $metadata.preEntryFutureY -ne 40 -or
        $metadata.preEntryLevelChanging -isnot [bool] -or -not $metadata.preEntryLevelChanging -or
        $metadata.preEntryLevelLoading -isnot [bool] -or -not $metadata.preEntryLevelLoading -or
        [string]::Join(',', @($metadata.depthSeeds)) -cne '2588,2588,1,4294967295' -or
        @($metadata.levels).Count -ne 4 -or @($metadata.routes).Count -ne 8 -or
        @($metadata.doorMappings).Count -ne 4 -or $metadata.doorProfileId -cne 'original-diablo.cathedral-doors' -or
        $metadata.doorProfileRevision -ne 1) {
        throw 'The profile does not select the explicit class-bound native Town/L1-L4 contract.'
    }
    foreach ($name in @('contentManifestHash', 'packFileHash', 'populationPackFileHash', 'runtimePackFileHash',
            'populationMetadataFingerprint', 'populationMetadataManifestHash', 'monsterRuntimeFingerprint',
            'inventoryFootprintCanonicalSha256', 'itemSemanticsCanonicalSha256', 'doorCatalogFingerprint')) {
        if ($metadata.$name -isnot [string] -or $metadata.$name -cnotmatch '^[0-9A-Fa-f]{64}$') {
            throw "Class-bound native metadata has an invalid $name identity."
        }
    }
    if (($metadata.semanticItemCount -isnot [int] -and $metadata.semanticItemCount -isnot [long]) -or
        $metadata.semanticItemCount -le 0) {
        throw 'Class-bound native metadata has an invalid semantic item count.'
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
            throw 'Class-bound native depth rows must match the pinned ordered L1-L4 seed table.'
        }
    }

    $rootEntries = @('cathedral-native-class-progression-playtest.json', 'content', 'native_cathedral_population', 'native_cathedral_runtime', 'sessions')
    foreach ($entry in Get-ChildItem -LiteralPath $root -Force -ErrorAction Stop) {
        if ($entry.Name -notin $rootEntries -or $entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The class-bound native profile root contains an unexpected entry.'
        }
    }
    $contentRoot = Join-Path $root 'content'
    $populationRoot = Join-Path $root 'native_cathedral_population'
    $runtimeRoot = Join-Path $root 'native_cathedral_runtime'
    foreach ($path in @($contentRoot, $populationRoot, $runtimeRoot)) {
        Assert-NoCathedralReparsePoint $path
        foreach ($entry in Get-ChildItem -LiteralPath $path -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Class-bound native packs cannot contain reparse points.' }
        }
    }
    $characterStartPath = Join-Path $runtimeRoot 'native_cathedral_runtime/native-character-start.tsv'
    Assert-NoCathedralReparsePoint $characterStartPath
    $bytes = [IO.File]::ReadAllBytes($characterStartPath)
    if ($bytes.Length -eq 0 -or ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) {
        throw 'The native character-start table must be nonempty UTF-8 without a BOM.'
    }
    $tableText = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    if (-not $tableText.EndsWith("`r`n", [StringComparison]::Ordinal)) { throw 'The native character-start table must use CRLF rows.' }
    for ($index = 0; $index -lt $tableText.Length; $index++) {
        if ($tableText[$index] -eq "`n" -and ($index -eq 0 -or $tableText[$index - 1] -ne "`r")) {
            throw 'The native character-start table contains a non-CRLF line ending.'
        }
    }
    $tableLines = $tableText.Split("`r`n", [StringSplitOptions]::None)
    $expectedHeader = "class_id`tclass_symbol`thero_base_life_maximum`thero_base_mana_maximum`thero_light_radius`thero_idle_frames"
    if ($tableLines.Length -ne 3 -or $tableLines[0] -cne $expectedHeader -or $tableLines[2] -cne '') {
        throw 'The native character-start table must have one row and the exact ordered schema.'
    }
    $tableFields = $tableLines[1].Split("`t")
    if ($tableFields.Length -ne 6 -or $tableFields[0] -cne [string]$facts.Id -or $tableFields[1] -cne $symbol -or
        $tableFields[2] -cne [string]$facts.Life -or $tableFields[3] -cne [string]$facts.Mana -or
        $tableFields[4] -cne [string]$facts.Light -or $tableFields[5] -cne [string]$facts.Idle) {
        throw 'The native character-start row does not match its canonical class and hero facts.'
    }

    $allowedPacks = @(
        @{ Root = $contentRoot; Id = $metadata.contentId; Version = $metadata.contentVersion },
        @{ Root = $populationRoot; Id = $metadata.populationPackId; Version = $metadata.populationPackVersion },
        @{ Root = $runtimeRoot; Id = $metadata.runtimePackId; Version = $metadata.runtimePackVersion })
    foreach ($pack in $allowedPacks) {
        if ([string]::IsNullOrWhiteSpace($pack.Id) -or [string]::IsNullOrWhiteSpace($pack.Version)) {
            throw 'Class-bound native pack identity is incomplete.'
        }
        $manifestPath = Join-Path $pack.Root 'content-pack.json'
        Assert-NoCathedralReparsePoint $manifestPath
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
        if ($manifest.formatVersion -isnot [int] -and $manifest.formatVersion -isnot [long] -or
            $manifest.formatVersion -ne 1 -or $manifest.id -cne $pack.Id -or $manifest.version -cne $pack.Version) {
            throw 'A class-bound native pack manifest does not match its profile.'
        }
    }
    $contentHash = Get-CathedralContentTreeHash $contentRoot
    $populationHash = Get-CathedralContentTreeHash $populationRoot
    $runtimeHash = Get-CathedralContentTreeHash $runtimeRoot
    if ($contentHash -cne $metadata.packFileHash -or $populationHash -cne $metadata.populationPackFileHash -or
        $runtimeHash -cne $metadata.runtimePackFileHash) {
        throw 'A class-bound native content, population, or runtime pack hash changed.'
    }
    if (Test-Path -LiteralPath $sessionRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $sessionRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Class-bound native session storage cannot contain reparse points.' }
        }
    }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ContentRoot = $contentRoot
        PopulationRoot = $populationRoot; RuntimeRoot = $runtimeRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = $contentHash; PopulationTreeHash = $populationHash; RuntimeTreeHash = $runtimeHash
        ClassId = $facts.Id; ClassSymbol = $symbol
    }
}

function Initialize-CathedralNativeClassSession($Profile, [string]$Session) {
    $markerFile = 'cathedral-native-class-progression-session.json'
    $profileKind = 'cathedral-native-class-progression-playtest'
    $adapterId = 'devilution.diablo.cathedral-native-class-progression.v1'
    $markerPath = Join-Path $Session $markerFile
    foreach ($otherMarker in @('cathedral-session.json', 'cathedral-door-session.json',
            'cathedral-native-population-session.json', 'cathedral-native-progression-session.json',
            'cathedral-native-depth-progression-session.json') ) {
        if (Test-Path -LiteralPath (Join-Path $Session $otherMarker)) {
            throw 'A save marker from another Cathedral profile cannot be reused by class-bound native progression.'
        }
    }
    if (Test-Path -LiteralPath $markerPath) {
        $marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
        $markerFields = @($marker.PSObject.Properties | ForEach-Object { $_.Name })
        if ($markerFields.Count -ne 9 -or
            [string]::Join("`n", $markerFields) -cne "FormatVersion`nProfileKind`nAdapterId`nMetadataHash`nContentTreeHash`nPopulationTreeHash`nRuntimeTreeHash`nClassId`nClassSymbol" -or
            ($marker.FormatVersion -isnot [int] -and $marker.FormatVersion -isnot [long]) -or $marker.FormatVersion -ne 1 -or
            $marker.ProfileKind -cne $profileKind -or $marker.AdapterId -cne $adapterId -or
            $marker.MetadataHash -cne $Profile.MetadataHash -or $marker.ContentTreeHash -cne $Profile.ContentTreeHash -or
            $marker.PopulationTreeHash -cne $Profile.PopulationTreeHash -or $marker.RuntimeTreeHash -cne $Profile.RuntimeTreeHash -or
            $marker.ClassId -ne $Profile.ClassId -or $marker.ClassSymbol -cne $Profile.ClassSymbol) {
            throw 'The class-bound native profile changed; use a fresh session instead of mixing saves.'
        }
    } else {
        if (Get-ChildItem -LiteralPath $Session -Force | Where-Object Name -ne 'launcher.lock') {
            throw 'An unmarked existing save profile must never become a class-bound native session.'
        }
        $stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write(([ordered]@{
                FormatVersion = 1; ProfileKind = $profileKind; AdapterId = $adapterId
                MetadataHash = $Profile.MetadataHash; ContentTreeHash = $Profile.ContentTreeHash
                PopulationTreeHash = $Profile.PopulationTreeHash; RuntimeTreeHash = $Profile.RuntimeTreeHash
                ClassId = $Profile.ClassId; ClassSymbol = $Profile.ClassSymbol
            } | ConvertTo-Json))
        } finally { $writer.Dispose() }
    }
}

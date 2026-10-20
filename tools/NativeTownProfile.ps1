# Explicit format-5 Town-enabled native progression profile admission.
function Get-CathedralNativeTownProfile([string]$Directory, [string]$Session) {
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $sessionRoot = [IO.Path]::GetFullPath($Session)
    $sessionsRoot = (Join-Path $root 'sessions').TrimEnd('\', '/')
    if ($root.Contains('"') -or $sessionRoot.Contains('"') -or
        -not $sessionRoot.StartsWith($sessionsRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Native Town progression requires a separate session child under its compiled profile sessions directory.'
    }
    Assert-NoCathedralReparsePoint $root
    Assert-NoCathedralReparsePoint $sessionRoot

    $metadataPath = Join-Path $root 'cathedral-native-town-progression-playtest.json'
    $classMetadataPath = Join-Path $root 'cathedral-native-class-progression-playtest.json'
    foreach ($path in @($metadataPath, $classMetadataPath)) { Assert-NoCathedralReparsePoint $path }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    $expectedFields = @(
        'formatVersion', 'adapterId', 'classProfile', 'townDirectory', 'townPackId', 'townPackVersion',
        'townPackTreeHash', 'townCatalogFingerprint', 'townInitialSeed', 'contentManifestHash')
    $actualFields = @($metadata.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actualFields.Count -ne $expectedFields.Count -or
        [string]::Join("`n", $actualFields) -cne [string]::Join("`n", $expectedFields)) {
        throw 'Native Town metadata does not match the closed format-5 schema.'
    }
    if (($metadata.formatVersion -isnot [int] -and $metadata.formatVersion -isnot [long]) -or
        $metadata.formatVersion -ne 5 -or
        $metadata.adapterId -cne 'devilution.diablo.cathedral-native-town-progression.v1' -or
        $metadata.townDirectory -cne 'native_town_geometry' -or
        $metadata.townPackId -cne 'godevilution.cathedral-native-town-geometry' -or
        $metadata.townPackVersion -cne '1.0.0' -or
        ($metadata.townInitialSeed -isnot [long] -and $metadata.townInitialSeed -isnot [int]) -or
        $metadata.townInitialSeed -lt 0 -or $metadata.townInitialSeed -gt [uint32]::MaxValue) {
        throw 'The profile does not select the supported format-5 native Town progression contract.'
    }
    foreach ($name in @('townPackTreeHash', 'townCatalogFingerprint', 'contentManifestHash')) {
        if ($metadata.$name -isnot [string] -or $metadata.$name -cnotmatch '^[0-9A-Fa-f]{64}$') {
            throw "Native Town metadata has an invalid $name identity."
        }
    }

    $classMetadata = Get-Content -LiteralPath $classMetadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($null -eq $metadata.classProfile -or $metadata.classProfile -isnot [System.Management.Automation.PSCustomObject]) {
        throw 'Native Town metadata must retain the nested format-4 class profile.'
    }
    $nestedCanonical = $metadata.classProfile | ConvertTo-Json -Depth 64 -Compress
    $retainedCanonical = $classMetadata | ConvertTo-Json -Depth 64 -Compress
    if ($nestedCanonical -cne $retainedCanonical) {
        throw 'The nested class profile does not exactly match the retained format-4 class metadata.'
    }
    $classProfile = Get-CathedralNativeTownClassProfile $classMetadata

    $rootEntries = @(
        'cathedral-native-town-progression-playtest.json', 'cathedral-native-class-progression-playtest.json',
        'content', 'native_cathedral_population', 'native_cathedral_runtime', 'native_town_geometry', 'sessions')
    foreach ($entry in Get-ChildItem -LiteralPath $root -Force -ErrorAction Stop) {
        if ($entry.Name -notin $rootEntries -or $entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The native Town profile root contains an unexpected entry.'
        }
    }
    $contentRoot = Join-Path $root 'content'
    $populationRoot = Join-Path $root 'native_cathedral_population'
    $runtimeRoot = Join-Path $root 'native_cathedral_runtime'
    $townRoot = Join-Path $root 'native_town_geometry'
    foreach ($path in @($contentRoot, $populationRoot, $runtimeRoot, $townRoot)) {
        Assert-NoCathedralReparsePoint $path
        foreach ($entry in Get-ChildItem -LiteralPath $path -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Native Town profile packs cannot contain reparse points.'
            }
        }
    }

    $classStartPath = Join-Path $runtimeRoot 'native_cathedral_runtime/native-character-start.tsv'
    Assert-CathedralNativeTownCharacterStart $classStartPath $classProfile
    $packs = @(
        @{ Root = $contentRoot; Id = $classMetadata.contentId; Version = $classMetadata.contentVersion; Hash = $classMetadata.packFileHash; Label = 'content' },
        @{ Root = $populationRoot; Id = $classMetadata.populationPackId; Version = $classMetadata.populationPackVersion; Hash = $classMetadata.populationPackFileHash; Label = 'population' },
        @{ Root = $runtimeRoot; Id = $classMetadata.runtimePackId; Version = $classMetadata.runtimePackVersion; Hash = $classMetadata.runtimePackFileHash; Label = 'runtime' },
        @{ Root = $townRoot; Id = $metadata.townPackId; Version = $metadata.townPackVersion; Hash = $metadata.townPackTreeHash; Label = 'Town' })
    foreach ($pack in $packs) {
        Assert-CathedralNativeTownPack $pack
    }

    $settingsPath = Join-Path $townRoot 'native_town_geometry/settings.tsv'
    Assert-CathedralNativeTownSettings $settingsPath ([uint32]$metadata.townInitialSeed)
    if (Test-Path -LiteralPath $sessionRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $sessionRoot -Force -Recurse -ErrorAction Stop) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Native Town session storage cannot contain reparse points.' }
        }
    }
    return [pscustomobject]@{
        Metadata = $metadata; MetadataPath = $metadataPath; ClassMetadataPath = $classMetadataPath
        ContentRoot = $contentRoot; PopulationRoot = $populationRoot; RuntimeRoot = $runtimeRoot; TownRoot = $townRoot
        MetadataHash = (Get-FileSha256 $metadataPath).ToUpperInvariant()
        ContentTreeHash = Get-CathedralContentTreeHash $contentRoot
        PopulationTreeHash = Get-CathedralContentTreeHash $populationRoot
        RuntimeTreeHash = Get-CathedralContentTreeHash $runtimeRoot
        TownTreeHash = Get-CathedralContentTreeHash $townRoot
        ClassId = $classProfile.Id; ClassSymbol = $classProfile.Symbol
    }
}

function Get-CathedralNativeTownClassProfile($Metadata) {
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
    $actualFields = @($Metadata.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actualFields.Count -ne $expectedFields.Count -or
        [string]::Join("`n", $actualFields) -cne [string]::Join("`n", $expectedFields)) {
        throw 'The retained class profile does not match the closed format-4 class schema.'
    }
    $facts = @{
        warrior = @{ Id = 0; Life = 4480; Mana = 640; Light = 10; Idle = 10 }
        sorcerer = @{ Id = 2; Life = 1920; Mana = 4480; Light = 10; Idle = 8 }
    }
    $symbol = [string]$Metadata.heroClassSymbol
    if (-not $facts.ContainsKey($symbol)) { throw 'Native Town progression supports only its bound Warrior or Sorcerer class.' }
    $class = $facts[$symbol]
    $policy = "original-diablo-single-player-normal-$symbol-town-l1-l4"
    if ($Metadata.formatVersion -ne 4 -or
        $Metadata.adapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1' -or
        $Metadata.contentDirectory -cne 'content' -or $Metadata.contentId -cne 'godevilution.cathedral-doors-playtest' -or
        $Metadata.contentVersion -cne '0.1.0' -or $Metadata.populationDirectory -cne 'native_cathedral_population' -or
        $Metadata.runtimeDirectory -cne 'native_cathedral_runtime' -or $Metadata.nativeDepth -ne 4 -or
        $Metadata.entry -cne 'main' -or $Metadata.heroBaseLifeMaximum -ne $class.Life -or
        $Metadata.heroBaseManaMaximum -ne $class.Mana -or $Metadata.heroLightRadius -ne $class.Light -or
        $Metadata.heroIdleFrames -ne $class.Idle -or $Metadata.populationPolicy -cne $policy -or
        $Metadata.nativePopulation -isnot [bool] -or -not $Metadata.nativePopulation -or
        $Metadata.nativePopulationClaim -isnot [bool] -or $Metadata.nativePopulationClaim -or
        @($Metadata.depthSeeds).Count -ne 4 -or @($Metadata.levels).Count -ne 4 -or
        @($Metadata.routes).Count -ne 8 -or @($Metadata.doorMappings).Count -ne 4) {
        throw 'The nested profile does not select the expected class-bound native Warrior/Sorcerer contract.'
    }
    foreach ($name in @('contentManifestHash', 'packFileHash', 'populationPackFileHash', 'runtimePackFileHash',
            'populationMetadataFingerprint', 'populationMetadataManifestHash', 'monsterRuntimeFingerprint',
            'inventoryFootprintCanonicalSha256', 'itemSemanticsCanonicalSha256', 'doorCatalogFingerprint')) {
        if ($Metadata.$name -isnot [string] -or $Metadata.$name -cnotmatch '^[0-9A-Fa-f]{64}$') {
            throw "The retained class profile has an invalid $name identity."
        }
    }
    return [pscustomobject]@{ Id = $class.Id; Symbol = $symbol }
}

function Assert-CathedralNativeTownCharacterStart([string]$Path, $ClassProfile) {
    Assert-NoCathedralReparsePoint $Path
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0 -or ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) {
        throw 'The native character-start table must be nonempty UTF-8 without a BOM.'
    }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    if (-not $text.EndsWith("`r`n", [StringComparison]::Ordinal)) { throw 'The native character-start table must use CRLF rows.' }
    for ($index = 0; $index -lt $text.Length; $index++) {
        if ($text[$index] -eq "`n" -and ($index -eq 0 -or $text[$index - 1] -ne "`r")) {
            throw 'The native character-start table contains a non-CRLF line ending.'
        }
    }
    $lines = $text.Split("`r`n", [StringSplitOptions]::None)
    $header = "class_id`tclass_symbol`thero_base_life_maximum`thero_base_mana_maximum`thero_light_radius`thero_idle_frames"
    $expected = if ($ClassProfile.Id -eq 0) { "0`twarrior`t4480`t640`t10`t10" } else { "2`tsorcerer`t1920`t4480`t10`t8" }
    if ($lines.Length -ne 3 -or $lines[0] -cne $header -or $lines[1] -cne $expected -or $lines[2] -cne '') {
        throw 'The native character-start row does not match the nested class profile.'
    }
}

function Assert-CathedralNativeTownPack($Pack) {
    if ([string]::IsNullOrWhiteSpace($Pack.Id) -or [string]::IsNullOrWhiteSpace($Pack.Version) -or
        $Pack.Hash -isnot [string] -or $Pack.Hash -cnotmatch '^[0-9A-Fa-f]{64}$') {
        throw "The native Town profile has an incomplete $($Pack.Label) pack identity."
    }
    $manifestPath = Join-Path $Pack.Root 'content-pack.json'
    Assert-NoCathedralReparsePoint $manifestPath
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($manifest.formatVersion -ne 1 -or $manifest.id -cne $Pack.Id -or $manifest.version -cne $Pack.Version) {
        throw "The native Town $($Pack.Label) pack manifest does not match its profile."
    }
    $treeHash = Get-CathedralContentTreeHash $Pack.Root
    if ($treeHash -cne $Pack.Hash) { throw "The native Town $($Pack.Label) pack hash changed." }
}

function Assert-CathedralNativeTownSettings([string]$Path, [uint32]$Seed) {
    Assert-NoCathedralReparsePoint $Path
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0 -or ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) {
        throw 'Native Town settings must be nonempty UTF-8 without a BOM.'
    }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $header = [string]::Join("`t", @('is_spawn', 'is_multiplayer', 'is_hellfire', 'character_level', 'warp_unlocks',
        'farmer_quest_state', 'grave_quest_state', 'entry', 'town_warp_from', 'water_quest_state', 'initial_random_state'))
    $row = [string]::Join("`t", @('false', 'false', 'false', '1', '0', 'other', 'other', 'main', '0', 'not_available',
        $Seed.ToString([Globalization.CultureInfo]::InvariantCulture)))
    if ($text -cne ($header + "`r`n" + $row + "`r`n")) {
        throw 'Native Town settings do not match the bound profile values or exact table schema.'
    }
}

function Initialize-CathedralNativeTownSession($Profile, [string]$Session) {
    $markerFile = 'cathedral-native-town-progression-session.json'
    $profileKind = 'cathedral-native-town-progression-playtest'
    $adapterId = 'devilution.diablo.cathedral-native-town-progression.v1'
    $markerPath = Join-Path $Session $markerFile
    foreach ($otherMarker in @('cathedral-session.json', 'cathedral-door-session.json',
            'cathedral-native-population-session.json', 'cathedral-native-progression-session.json',
            'cathedral-native-depth-progression-session.json', 'cathedral-native-class-progression-session.json')) {
        if (Test-Path -LiteralPath (Join-Path $Session $otherMarker)) {
            throw 'A save marker from another Cathedral profile cannot be reused by native Town progression.'
        }
    }
    if (Test-Path -LiteralPath $markerPath) {
        $marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
        $markerFields = @($marker.PSObject.Properties | ForEach-Object { $_.Name })
        $expectedMarkerFields = @('FormatVersion', 'ProfileKind', 'AdapterId', 'MetadataHash', 'ContentManifestHash',
            'ContentTreeHash', 'PopulationTreeHash', 'RuntimeTreeHash', 'TownTreeHash', 'TownCatalogFingerprint',
            'TownInitialSeed', 'ClassId', 'ClassSymbol')
        if ($markerFields.Count -ne $expectedMarkerFields.Count -or
            [string]::Join("`n", $markerFields) -cne [string]::Join("`n", $expectedMarkerFields) -or
            ($marker.FormatVersion -isnot [int] -and $marker.FormatVersion -isnot [long]) -or $marker.FormatVersion -ne 1 -or
            $marker.ProfileKind -cne $profileKind -or $marker.AdapterId -cne $adapterId -or
            $marker.MetadataHash -cne $Profile.MetadataHash -or
            $marker.ContentManifestHash -cne $Profile.Metadata.contentManifestHash -or
            $marker.ContentTreeHash -cne $Profile.ContentTreeHash -or $marker.PopulationTreeHash -cne $Profile.PopulationTreeHash -or
            $marker.RuntimeTreeHash -cne $Profile.RuntimeTreeHash -or $marker.TownTreeHash -cne $Profile.TownTreeHash -or
            $marker.TownCatalogFingerprint -cne $Profile.Metadata.townCatalogFingerprint -or
            $marker.TownInitialSeed -ne $Profile.Metadata.townInitialSeed -or
            $marker.ClassId -ne $Profile.ClassId -or $marker.ClassSymbol -cne $Profile.ClassSymbol) {
            throw 'The native Town profile changed; use a fresh session instead of mixing saves.'
        }
    } else {
        if (Get-ChildItem -LiteralPath $Session -Force | Where-Object Name -ne 'launcher.lock') {
            throw 'An unmarked existing save profile must never become a native Town session.'
        }
        $stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write(([ordered]@{
                FormatVersion = 1; ProfileKind = $profileKind; AdapterId = $adapterId
                MetadataHash = $Profile.MetadataHash; ContentManifestHash = $Profile.Metadata.contentManifestHash
                ContentTreeHash = $Profile.ContentTreeHash; PopulationTreeHash = $Profile.PopulationTreeHash
                RuntimeTreeHash = $Profile.RuntimeTreeHash; TownTreeHash = $Profile.TownTreeHash
                TownCatalogFingerprint = $Profile.Metadata.townCatalogFingerprint
                TownInitialSeed = $Profile.Metadata.townInitialSeed
                ClassId = $Profile.ClassId; ClassSymbol = $Profile.ClassSymbol
            } | ConvertTo-Json -Depth 4))
        } finally { $writer.Dispose() }
    }
}

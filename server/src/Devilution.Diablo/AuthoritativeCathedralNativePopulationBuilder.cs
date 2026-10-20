using Devilution.Data;

namespace Devilution.Diablo;

/** Detached construction of the single supported native-populated Cathedral level. */
internal sealed record AuthoritativeCathedralNativePopulationBuild(
    AuthoritativeCathedralFreshLevelResult Generated,
    string MonsterMetadataFingerprint,
    AuthoritativeCathedralMonsterRuntimeAdapterResult Monsters,
    IReadOnlyList<AuthoritativeWorldObject> Objects,
    AuthoritativeCathedralItemRuntimeProfile Items,
    AuthoritativeCathedralNativePopulationPolicy Policy,
    IReadOnlyDictionary<string, int> NativeAiCounts,
    int PlayerMinionCount,
    int ScavengerCount);

internal static class AuthoritativeCathedralNativePopulationBuilder
{
    private const int NativeFirstItemCursorOffset = 12;
    private const uint MonsterEntityIdBase = 0x09001000;
    private const uint ObjectEntityIdBase = 0x09002000;
    private const uint NativeObjectIdBase = 0x09000000;
    private const uint ItemEntityIdBase = 0x0A001000;
    private const int TilePopulated = 1 << 3;
    private const string UniqueIdentityPrefix = "native-cathedral-unique-";
    private static readonly string UnboundGenerationFingerprint = new('0', 64);

    public static AuthoritativeCathedralNativePopulationBuild Build(
        AuthoritativeCathedralPopulationMetadata metadata,
        AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
        AuthoritativeCathedralDepthGenerationCandidate depthCandidate,
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativePlayerAnimationCatalog playerAnimations,
        AuthoritativePlayerLoadoutCatalog playerLoadouts,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCathedralDoorDefinition doorDefinition,
        IReadOnlyCollection<AuthoritativeItemState> storeItems,
        AuthoritativeCathedralNativeItemSidecars itemSidecars,
        AuthoritativeCathedralFreshLevelRequest? entryRequest = null,
        IReadOnlyCollection<AuthoritativeItemState>? carriedItems = null,
        AuthoritativeCathedralNativeCharacterStart? characterStart = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        ArgumentNullException.ThrowIfNull(depthCandidate);
        ArgumentNullException.ThrowIfNull(playerClasses);
        ArgumentNullException.ThrowIfNull(playerAnimations);
        ArgumentNullException.ThrowIfNull(playerLoadouts);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        ArgumentNullException.ThrowIfNull(contentItems);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(doorDefinition);
        ArgumentNullException.ThrowIfNull(storeItems);
        ArgumentNullException.ThrowIfNull(itemSidecars);

        if (entryRequest is null) {
            if (depthCandidate.Level.LevelId != 1 || depthCandidate.Identity.Depth != 1
                || depthCandidate.Identity.DungeonSeed != 2588
                || depthCandidate.Identity.Entry != AuthoritativeCathedralEntry.Main
                || doorDefinition.LevelId != 1)
                throw new NotSupportedException("The native-populated route supports only its pinned Cathedral L1 main-entry generation.");
        } else if (entryRequest.Depth != depthCandidate.Identity.Depth
            || entryRequest.DungeonSeed != depthCandidate.Identity.DungeonSeed
            || entryRequest.Entry != depthCandidate.Identity.Entry
            || entryRequest.StableLevelSymbol != depthCandidate.Level.StableSymbol
            || entryRequest.Metadata.Fingerprint != metadata.Fingerprint
            || doorDefinition.LevelId != depthCandidate.Level.LevelId || carriedItems is null) {
            throw new InvalidDataException("A native first-entry build must bind the exact floor, hero context and carried items.");
        }
        var entityOffset = entryRequest is null ? 0U : checked((uint)(entryRequest.Depth - 1) * 0x10000U);

        var resolvedCharacterStart = characterStart
            ?? AuthoritativeCathedralNativeCharacterStart.Resolve(null, playerClasses, playerAnimations, playerLoadouts);
        var playerClass = playerClasses.GetRequired(resolvedCharacterStart.ClassId);
        if (!string.Equals(playerClass.Symbol, resolvedCharacterStart.ClassSymbol, StringComparison.Ordinal))
            throw new InvalidDataException("Native Cathedral character start no longer matches the active class catalog.");
        var template = playerLoadouts.CreateTemplate(
            "native-cathedral-population-profile",
            playerClass.ClassId,
            goldOverride: null,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: depthCandidate.Level.LevelId,
            startingPositionX: resolvedCharacterStart.Hero.PreEntryFuture.X,
            startingPositionY: resolvedCharacterStart.Hero.PreEntryFuture.Y);
        var hero = resolvedCharacterStart.Hero;
        var request = entryRequest ?? AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            metadata,
            depth: 1,
            dungeonSeed: 2588,
            storySeed: 0x13579BDF,
            AuthoritativeCathedralEntry.Main,
            hero,
            depthCandidate.Level.StableSymbol);
        var generated = AuthoritativeCathedralFreshLevelGenerator.Generate(request);
        var generatedFingerprint = AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(
            generated.Geometry.Level);
        if (!string.Equals(generatedFingerprint, depthCandidate.Identity.FineFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Full native population geometry does not match the activated Cathedral depth identity.");

        var state = generated.Population.State;
        if (entryRequest is null && state.ActiveMonsterIds.Take(state.ActiveMonsterCount)
            .Any(slot => state.MonsterSlots[slot].UniqueType != byte.MaxValue))
            throw new NotSupportedException("Active native unique monsters require stable unique item identities before this route can activate.");
        var monsterMetadataFingerprint = AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(
            metadata.Fingerprint, metadata.UniqueMonsters.Fingerprint, runtimeCatalog);
        var uniqueIdentities = metadata.UniqueMonsters.Definitions.ToDictionary(
            definition => definition.NativeUniqueId,
            definition => UniqueIdentityPrefix + definition.NativeUniqueId.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
        var monsterBindings = new List<AuthoritativeCathedralMonsterRuntimeBinding>(state.ActiveMonsterCount);
        foreach (var slot in state.ActiveMonsterIds.Take(state.ActiveMonsterCount)) {
            if ((uint)slot >= (uint)state.MonsterSlots.Count)
                throw new InvalidDataException("Native monster order references a slot outside its retained pool.");
            var nativeType = state.MonsterSlots[slot].Type;
            AuthoritativeMonsterTypeDefinition type = null!;
            var bound = nativeType >= 0 && monsterTypes.TryGetNative(checked((uint)nativeType), out type);
            if (!bound && entryRequest is not null && nativeType >= 0
                && monsterTypes.IsNativeMonsterIdAmbiguous(checked((uint)nativeType))) {
                // Authored quest aliases can share a raw type. First-entry native
                // provenance selects only a unique existing row with the exact AI.
                var nativeAi = AuthoritativeCathedralMonsterRuntimeAdapter.GetNativeAi(state.MonsterSlots[slot].Ai);
                var exact = monsterTypes.Definitions.Where(row => row.NativeMonsterId == (uint)nativeType
                    && row.NativeAi == nativeAi).ToArray();
                if (exact.Length == 1) { type = exact[0]; bound = true; }
            }
            if (!bound)
                throw new InvalidDataException($"Native monster slot {slot}, raw type {nativeType}, has no exact stable type binding.");
            monsterBindings.Add(new AuthoritativeCathedralMonsterRuntimeBinding(
                slot,
                checked(MonsterEntityIdBase + entityOffset + (uint)slot),
                type.MonsterId,
                depthCandidate.Level.LevelId));
        }
        var monsters = entryRequest is null
            ? AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
                state, runtimeCatalog, monsterMetadataFingerprint, monsterBindings,
                DiabloDifficulty.Normal, metadata.UniqueMonsters.Definitions, monsterTypes,
                uniqueIdentities, metadata.Population.Monsters)
            : AuthoritativeCathedralMonsterRuntimeAdapter.AdaptLevelEntryWithBindings(
                state, runtimeCatalog, monsterMetadataFingerprint, monsterBindings,
                metadata.Population.Monsters, DiabloDifficulty.Normal,
                metadata.UniqueMonsters.Definitions, monsterTypes, uniqueIdentities);

        var objects = BuildObjects(state, depthCandidate.Level.LevelId, doorDefinition,
            checked(ObjectEntityIdBase + entityOffset));

        if (entryRequest is not null && monsters.Records.Any(record => record.Target is not null
            && runtimeCatalog.HasUniqueTreasure(record.InitialState.Type)))
            throw new NotSupportedException("A native floor with special-treasure actors requires the exact reward compositor.");
        var hasUniqueLoot = entryRequest is not null && monsters.Records.Any(record => record.Target?.IsUnique == true);
        var supportedLootLevels = monsters.Records
            .Where(record => record.Target is { IsPlayerMinion: false } target
                && (entryRequest is not null || !target.IsUnique))
            .Select(record => unchecked((sbyte)record.Target!.MonsterLevel))
            .Distinct()
            .ToArray();
        var initialOwnedItems = carriedItems?.ToArray() ?? template.Inventory.Select(item => item.State)
            .Concat(template.Equipment.Select(item => item.State))
            .Concat(template.Belt.Select(item => item.State))
            .ToArray();
        var itemBindings = BuildItemBindings(metadata.Items.Catalog, contentItems, spells, state,
            supportedLootLevels, initialOwnedItems, storeItems, itemSidecars, hasUniqueLoot,
            includeCompleteAllowlist: entryRequest is not null);
        var spellBindings = BuildSpellBindings(spells);
        var itemCheckpoint = BuildItemCheckpoint(state, depthCandidate.Level.LevelId,
            generated.FullPopulationRequest.ItemPolicy.Depth, depthCandidate.Identity.FineFingerprint,
            metadata.Items.Fingerprint, generated.Population.FinalRandomState, itemBindings,
            checked(ItemEntityIdBase + entityOffset));
        var itemProfile = new AuthoritativeCathedralItemRuntimeProfile(
            metadata.Items.Fingerprint,
            UnboundGenerationFingerprint,
            itemCheckpoint,
            metadata.Items.Catalog,
            generated.FullPopulationRequest.ItemPolicy,
            runtimeCatalog,
            metadata.UniqueMonsters.Definitions,
            itemBindings,
            new AuthoritativeCathedralItemInventoryDimensions(),
            [BuildItemLevelProfile(state, depthCandidate.Level.LevelId, request.Depth, itemCheckpoint.Levels[0].SlotEntityBindings, objects)],
            spellBindings);

        var policy = entryRequest is null
            ? new AuthoritativeCathedralNativePopulationPolicy(request, state, monsters, objects, itemProfile,
                isDefaultStart: depthCandidate.Level.IsDefaultStart,
                suppressMultiplayerDeathDrops: depthCandidate.Level.SuppressMultiplayerDeathDrops)
            : AuthoritativeCathedralNativePopulationPolicy.CreateLevelEntry(request, state, monsters, objects,
                itemProfile, isDefaultStart: depthCandidate.Level.IsDefaultStart,
                suppressMultiplayerDeathDrops: depthCandidate.Level.SuppressMultiplayerDeathDrops);
        var boundCheckpoint = itemCheckpoint with { GenerationFingerprint = policy.Fingerprint };
        itemProfile = itemProfile with {
            GenerationFingerprint = policy.Fingerprint,
            InitialCheckpoint = boundCheckpoint,
        };
        if (!string.Equals(policy.Fingerprint, boundCheckpoint.GenerationFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Native item and monster owners did not bind one shared generation fingerprint.");

        var targets = monsters.Records.Where(record => record.Target is not null)
            .Select(record => record.Target!).ToArray();
        var aiCounts = targets.GroupBy(target => target.NativeAi, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new(generated, monsterMetadataFingerprint, monsters, objects, itemProfile, policy,
            aiCounts, targets.Count(target => target.IsPlayerMinion),
            targets.Count(target => string.Equals(target.NativeAi, "Scavenger", StringComparison.Ordinal)));
    }

    private static IReadOnlyList<AuthoritativeWorldObject> BuildObjects(
        AuthoritativeCathedralThemeRoomState state,
        uint levelId,
        AuthoritativeCathedralDoorDefinition doorDefinition,
        uint objectEntityIdBase)
    {
        if (state.ActiveObjectCount is < 0 or > AuthoritativeCathedralThemeRoomState.ObjectCapacity
            || state.ActiveObjectIds.Count < state.ActiveObjectCount)
            throw new NotSupportedException("Native Cathedral object order exceeds its retained active capacity.");
        var activeSlots = state.ActiveObjectIds.Take(state.ActiveObjectCount).ToArray();
        if (activeSlots.Any(slot => (uint)slot >= (uint)state.ObjectSlots.Count)
            || activeSlots.Distinct().Count() != activeSlots.Length)
            throw new InvalidDataException("Native Cathedral object order must retain distinct in-range pool slots.");

        var result = new List<AuthoritativeWorldObject>(state.ActiveObjectCount);
        var doorPositions = new HashSet<(int X, int Y)>();
        foreach (var slot in activeSlots) {
            var raw = state.ObjectSlots[slot];
            if (raw.Slot != slot || raw.Type < 0)
                throw new InvalidDataException("A native active object row lost its slot or type identity.");
            var entityId = raw.DoorFlag
                ? DiabloWorldState.GetCathedralDoorEntityId(levelId, AuthoritativeCathedralThemeRoomState.Width,
                    raw.Position.X, raw.Position.Y)
                : checked(objectEntityIdBase + (uint)slot);
            var objectId = raw.DoorFlag
                ? raw.Type switch {
                    1 => doorDefinition.LeftObjectId,
                    2 => doorDefinition.RightObjectId,
                    _ => throw new NotSupportedException("A native Cathedral door type is not bound to the L1 door catalog."),
                }
                : checked(NativeObjectIdBase + (uint)raw.Type + 1);
            AuthoritativeCathedralDoorState? door = null;
            if (raw.DoorFlag) {
                if (!doorPositions.Add((raw.Position.X, raw.Position.Y)))
                    throw new InvalidDataException("Native Cathedral door positions must be unique.");
                door = new((AuthoritativeCathedralDoorKind)raw.Type, raw.Position.X, raw.Position.Y,
                    (AuthoritativeCathedralDoorStatus)raw.Var4, raw.Var1, raw.Var2,
                    checked((int)raw.AnimFrame), raw.SelectionRegion, raw.PreFlag,
                    raw.MissilePassable, raw.SolidFlag);
            }
            var projection = new AuthoritativeWorldObject(
                entityId,
                objectId,
                levelId,
                raw.Position.X,
                raw.Position.Y,
                missilePassable: raw.MissilePassable,
                breakable: false,
                trapArmed: raw.TrapFlag,
                nativeObjectId: checked((uint)raw.Type),
                hideWhenActivated: false,
                animationFrame: checked((int)raw.AnimFrame)) {
                    CathedralDoor = door,
                };
            result.Add(projection);
        }
        if (result.Count != state.ActiveObjectCount || result.Count(value => value.CathedralDoor is not null)
            != activeSlots.Count(slot => state.ObjectSlots[slot].DoorFlag))
            throw new InvalidDataException("Native Cathedral object projection omitted an active slot or door.");
        return Array.AsReadOnly(result.ToArray());
    }

    internal static IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> BuildItemBindings(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCathedralThemeRoomState state,
        IReadOnlyCollection<sbyte> supportedLootLevels,
        IReadOnlyCollection<AuthoritativeItemState> initialOwnedItems,
        IReadOnlyCollection<AuthoritativeItemState> storeItems,
        AuthoritativeCathedralNativeItemSidecars itemSidecars,
        bool includeUniqueLoot = false,
        bool includeCompleteAllowlist = false)
        => BuildItemBindingsCore(nativeItems, contentItems, spells, state, supportedLootLevels,
            initialOwnedItems, storeItems, itemSidecars, includeUniqueLoot, includeCompleteAllowlist,
            familyContext: null);

    internal static IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> BuildItemBindings(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCathedralThemeRoomState state,
        IReadOnlyCollection<sbyte> supportedLootLevels,
        IReadOnlyCollection<AuthoritativeItemState> initialOwnedItems,
        IReadOnlyCollection<AuthoritativeItemState> storeItems,
        AuthoritativeCathedralNativeItemSidecars itemSidecars,
        bool includeUniqueLoot,
        bool includeCompleteAllowlist,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        familyContext.RequireMatch(familyContext.Depth);
        return BuildItemBindingsCore(nativeItems, contentItems, spells, state, supportedLootLevels,
            initialOwnedItems, storeItems, itemSidecars, includeUniqueLoot, includeCompleteAllowlist,
            familyContext);
    }

    private static IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> BuildItemBindingsCore(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCathedralThemeRoomState state,
        IReadOnlyCollection<sbyte> supportedLootLevels,
        IReadOnlyCollection<AuthoritativeItemState> initialOwnedItems,
        IReadOnlyCollection<AuthoritativeItemState> storeItems,
        AuthoritativeCathedralNativeItemSidecars itemSidecars,
        bool includeUniqueLoot,
        bool includeCompleteAllowlist,
        AuthoritativeNativeDungeonFamilyContext? familyContext)
    {
        var result = new Dictionary<int, AuthoritativeCathedralItemContentBinding>();
        var requiredIndexes = nativeItems.Items
            .Where(item => item.Available && item.DropRate > 0 && item.SpellId is not 32 and not 34
                && (supportedLootLevels.Any(level => item.MinimumMonsterLevel <= level)
                    || includeUniqueLoot && item.ItemType == 0 && item.MiscId == 24))
            .Select(item => item.NativeItemIndex)
            .ToHashSet();
        if (includeCompleteAllowlist)
            requiredIndexes.UnionWith(itemSidecars.Semantics.Keys);
        requiredIndexes.Add(0); // Native gold generation resolves this canonical raw item independently of DropRate.
        foreach (var sourceItem in initialOwnedItems.Concat(storeItems)) {
            if (sourceItem.ItemIndex < 0)
                throw new NotSupportedException("Native Cathedral ownership requires a stable native index for every starting loadout and store item.");
            requiredIndexes.Add(sourceItem.ItemIndex);
        }
        foreach (var reference in state.ItemRefs.Where(value => value != 0)) {
            if (reference < 0)
                throw new NotSupportedException("Native item bindings do not support negative signed-reference rows.");
            var slot = reference - 1;
            if ((uint)slot >= (uint)state.ItemSlots.Count)
                throw new InvalidDataException("A native item reference points outside the retained item pool.");
            requiredIndexes.Add(state.ItemSlots[slot].ItemIndex);
        }
        foreach (var slot in state.ActiveItemIds.Take(state.ActiveItemCount))
            if ((uint)slot < (uint)state.ItemSlots.Count && state.ItemSlots[slot].ItemIndex >= 0)
                requiredIndexes.Add(state.ItemSlots[slot].ItemIndex);

        foreach (var nativeIndex in requiredIndexes.Order()) {
            var native = nativeItems.Items.SingleOrDefault(item => item.NativeItemIndex == nativeIndex)
                ?? throw new InvalidDataException($"Native Cathedral item index {nativeIndex} has no raw metadata row.");
            if (!itemSidecars.Footprints.TryGetValue(nativeIndex, out var footprint)
                || !itemSidecars.Semantics.TryGetValue(nativeIndex, out var semantic))
                throw new InvalidDataException($"Native Cathedral item index {nativeIndex} has no verified footprint or semantic row.");
            if (footprint.NativeCursorId != native.CursorGraphic + 12
                || semantic.NativeCursorId != footprint.NativeCursorId
                || semantic.InventoryWidth != footprint.InventoryWidth
                || semantic.InventoryHeight != footprint.InventoryHeight)
                throw new InvalidDataException($"Native Cathedral item index {nativeIndex} does not match its captured cursor footprint.");
            var stableSpellId = native.MiscId == 21 && native.SpellId == 5
                ? 0 // Native Identify scrolls dispatch an item action, not the unrelated spell ordinal 5.
                : native.SpellId == 0
                    ? 0
                    : spells.TryGetByNativeId(checked((uint)native.SpellId), out var spell)
                        ? checked((int)spell.SpellId)
                        : throw new InvalidDataException(
                            $"Native Cathedral item index {nativeIndex} references an unmapped spell ordinal.");
            var candidates = contentItems.Definitions
                .Where(definition => definition.NativeItemIndex == nativeIndex
                    && string.Equals(definition.StableSymbol, semantic.StableItemSymbol, StringComparison.Ordinal)
                    && (familyContext is { IsCatacombs: true }
                        && AuthoritativeCatacombsNativeItemBindings.MatchesElixir(native, definition, stableSpellId)
                        || MatchesNativeItemDefinition(native, definition, stableSpellId, includeNativeDepthItems: true)))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException(
                    $"Native Cathedral item index {nativeIndex} has {candidates.Length} exact stable content identities. "
                    + $"Raw(type={native.ItemType},class={native.ItemClass},equip={native.EquipLocation},cursor={native.CursorGraphic},"
                    + $"value={native.Value},damage={native.MinimumDamage}-{native.MaximumDamage},durability={native.Durability},"
                    + $"minLevel={native.MinimumMonsterLevel},dropRate={native.DropRate},spell={native.SpellId}); candidates=["
                    + string.Join(",", contentItems.Definitions.Where(definition => definition.NativeItemIndex == nativeIndex)
                        .Select(definition => $"{definition.ItemId}/{definition.StableSymbol}:{definition.ItemType}/{definition.ItemClass}/{definition.EquipLocation}/{definition.NativeCursorGraphic}/{definition.Value}/{definition.MinDamage}-{definition.MaxDamage}/{definition.MaxDurability}/{definition.MinimumMonsterLevel}/{definition.DropRate}/{definition.SpellId}/{definition.InventoryWidth}x{definition.InventoryHeight}"))
                    + "].");
            var definition = candidates[0];
            foreach (var sourceItem in initialOwnedItems.Concat(storeItems)
                         .Where(value => value.ItemIndex == nativeIndex)) {
                if (sourceItem.StableItemId != definition.ItemId)
                    throw new NotSupportedException(
                        $"Native Cathedral item index {nativeIndex} maps to '{definition.StableSymbol}' ({definition.ItemId}), "
                        + $"but an admitted starter or store row uses stable item {sourceItem.StableItemId}.");
            }
            var action = ResolveNativeItemAction(native, definition, contentItems,
                nativeSpellActions: semantic.ItemActionKind == DiabloItemActionKind.None);
            if (semantic.ItemActionKind != action)
                throw new InvalidDataException($"Native Cathedral item index {nativeIndex} has an unsupported action mapping.");
            if (definition.InventoryWidth != footprint.InventoryWidth
                || definition.InventoryHeight != footprint.InventoryHeight)
                throw new InvalidDataException($"Native Cathedral item index {nativeIndex} has no footprint-sized effective item definition.");
            var spellSource = ResolveNativeItemSpellSource(native, definition, semantic.SpellSourcePolicy);
            result.Add(nativeIndex, new AuthoritativeCathedralItemContentBinding(
                nativeIndex, definition.ItemId, definition.StableSymbol,
                footprint.InventoryWidth, footprint.InventoryHeight,
                spellSource, action));
        }
        return result;
    }

    internal static bool MatchesNativeItemDefinition(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition content,
        int stableSpellId,
        bool includeNativeDepthItems = false)
    {
        if (!TryGetStableItemType(native.ItemType, out var stableItemType, out var requiredTag)
            || (content.ItemType != stableItemType
                && content.ItemType != native.ItemType)
            || !content.GenerationTags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase))
            return false;

        var identifiedName = string.IsNullOrWhiteSpace(content.IdentifiedName)
            ? content.Name
            : content.IdentifiedName;
        var nativeIdentifiedName = string.IsNullOrWhiteSpace(native.ShortName)
            ? native.Name
            : native.ShortName;
        var stableMiscId = native.MiscId switch {
            18 => 19, // Native rejuvenation misc IDs map to the stable action rows.
            19 => 20,
            23 => 0,  // Staff identity is carried by the stable Staff tag and native item binding.
            24 => 25, // Native books use the shared stable book-template misc ID.
            25 when native.ItemType == 12 => 26, // Stable jewelry misc IDs follow Book.
            26 when native.ItemType == 13 => 27,
            _ => native.MiscId,
        };
        var effectiveStaffMiscId = native.MiscId == 23 && native.ItemType == 10
            && content.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase)
            && content.MiscId == 23;
        var identifiedSpell = native.MiscId == 21 && native.SpellId == 5
            ? 0
            : stableSpellId;
        var minimumMagicMatches = content.MinimumMagic == native.MinimumMagic
            || (native.MiscId == 22 || includeNativeDepthItems && native.MiscId == 21)
                && native.SpellId != 0 && native.MinimumMagic > 0 && content.MinimumMagic == 0;
        var dropRateMatches = content.DropRate == native.DropRate
            || native.ItemType == 3 && native.DropRate == 2 && content.DropRate == 1;
        var actionMatches = native.MiscId != 21 || native.SpellId != 5
            || content.ItemActionKind is DiabloItemActionKind.None or DiabloItemActionKind.Identify;

        var exactBookTemplateName = native.ItemType == 0
            && native.MiscId == 24
            && content.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase)
            && native.Name.EndsWith(" ", StringComparison.Ordinal)
            && !native.Name.EndsWith("  ", StringComparison.Ordinal)
            && native.MinimumMonsterLevel > 0
            && native.MinimumMonsterLevel % 2 == 0
            && content.BookTemplateMinimumItemLevel == native.MinimumMonsterLevel / 2
            && string.Equals(content.Name, native.Name[..^1], StringComparison.Ordinal)
            && string.Equals(identifiedName, native.Name[..^1], StringComparison.Ordinal)
            && string.Equals(nativeIdentifiedName, native.Name, StringComparison.Ordinal);
        return (string.Equals(content.Name, native.Name, StringComparison.Ordinal) || exactBookTemplateName)
            && (string.Equals(identifiedName, nativeIdentifiedName, StringComparison.Ordinal) || exactBookTemplateName)
            && content.ItemClass == native.ItemClass
            && content.EquipLocation == native.EquipLocation
            && content.NativeCursorGraphic == native.CursorGraphic
            && content.Value == native.Value
            && content.MinDamage == native.MinimumDamage
            && content.MaxDamage == native.MaximumDamage
            && content.MinArmorClass == native.MinimumArmorClass
            && content.MaxArmorClass == native.MaximumArmorClass
            && content.MaxDurability == native.Durability
            && content.MinimumStrength == native.MinimumStrength
            && minimumMagicMatches
            && content.MinimumDexterity == native.MinimumDexterity
            && content.Flags == native.Flags
            && (content.MiscId == stableMiscId || effectiveStaffMiscId)
            && content.SpellId == identifiedSpell
            && content.MinimumMonsterLevel == native.MinimumMonsterLevel
            && dropRateMatches
            && actionMatches;
    }

    internal static bool SourceSemanticsMatch(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition content)
    {
        if (native.ItemType == 10 && native.MiscId == 23 && native.SpellId == 0
            && content.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase))
            return content.MiscId == 23 && content.SpellSourceKind is null;
        if (native.MiscId is 21 or 22 && native.SpellId != 0
            && !(native.MiscId == 21 && native.SpellId == 5))
            return content.SpellSourceKind == DiabloItemSpellSourceKind.ConsumableItem;
        return true;
    }

    internal static AuthoritativeItemCatalog ApplyItemSidecars(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog baseItems,
        AuthoritativeSpellCatalog spells,
        AuthoritativeCathedralNativeItemSidecars sidecars)
    {
        ArgumentNullException.ThrowIfNull(nativeItems);
        ArgumentNullException.ThrowIfNull(baseItems);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(sidecars);
        var rawByIndex = nativeItems.Items.ToDictionary(item => item.NativeItemIndex);
        var replacements = new Dictionary<uint, AuthoritativeItemDefinition>();
        foreach (var semantic in sidecars.Semantics.Values.OrderBy(value => value.NativeItemIndex)) {
            if (!rawByIndex.TryGetValue(semantic.NativeItemIndex, out var native)
                || !sidecars.Footprints.TryGetValue(semantic.NativeItemIndex, out var footprint))
                throw new InvalidDataException("Native item semantics reference an item without raw metadata and a footprint.");
            if (semantic.NativeCursorId != checked(native.CursorGraphic + NativeFirstItemCursorOffset)
                || footprint.NativeCursorId != semantic.NativeCursorId
                || semantic.InventoryWidth != footprint.InventoryWidth
                || semantic.InventoryHeight != footprint.InventoryHeight)
                throw new InvalidDataException($"Native item {semantic.NativeItemIndex} semantic row conflicts with its verified cursor footprint.");

            var stableSpellId = native.MiscId == 21 && native.SpellId == 5
                ? 0
                : native.SpellId == 0
                    ? 0
                    : spells.TryGetByNativeId(checked((uint)native.SpellId), out var mappedSpell)
                        ? checked((int)mappedSpell.SpellId)
                        : throw new InvalidDataException($"Native item {native.NativeItemIndex} references an unmapped spell.");
            var candidates = baseItems.Definitions.Where(definition =>
                    definition.NativeItemIndex == native.NativeItemIndex
                    && string.Equals(definition.StableSymbol, semantic.StableItemSymbol, StringComparison.Ordinal)
                    && MatchesNativeItemDefinition(native, definition, stableSpellId, includeNativeDepthItems: true))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException($"Native item semantics must select one exact stable identity for native index {native.NativeItemIndex}.");
            var selected = candidates[0];
            var expectedAction = ResolveNativeItemAction(native, selected, baseItems,
                nativeSpellActions: semantic.ItemActionKind == DiabloItemActionKind.None);
            if (semantic.ItemActionKind != expectedAction
                || semantic.MiscId != GetStableMiscId(native, selected))
                throw new InvalidDataException($"Native item semantics contain an unsupported action or misc mapping for index {native.NativeItemIndex}.");
            var dynamicStaff = native.ItemType == 10 && native.MiscId == 23 && native.SpellId == 0
                && selected.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase);
            if (dynamicStaff) {
                if (semantic.SpellSourcePolicy != "native-staff-dynamic"
                    || selected.SpellSourceKind is not (null or DiabloItemSpellSourceKind.None)
                    || semantic.MiscId != 23)
                    throw new InvalidDataException($"Native staff index {native.NativeItemIndex} lacks the explicit dynamic-source staff overlay.");
            } else if (semantic.SpellSourcePolicy != "catalog"
                || !SourceSemanticsMatch(native, selected)) {
                throw new InvalidDataException($"Native item index {native.NativeItemIndex} source policy conflicts with canonical content.");
            }

            replacements.Add(selected.ItemId, selected with {
                InventoryWidth = footprint.InventoryWidth,
                InventoryHeight = footprint.InventoryHeight,
                // The new explicit targetless-scroll rows carry their native
                // requirement; legacy rows already match or remain unselected.
                MinimumMagic = native.MiscId == 21 ? native.MinimumMagic : selected.MinimumMagic,
                MiscId = semantic.MiscId,
                SpellSourceKind = dynamicStaff ? null : selected.SpellSourceKind,
                ItemActionKind = semantic.ItemActionKind,
            });
        }
        var goldRows = nativeItems.Items.Where(item => item.ItemType == 11).ToArray();
        if (goldRows.Length != 1)
            throw new InvalidDataException("Native population metadata must contain exactly one Gold item row.");
        var nativeGold = goldRows[0];
        if (!sidecars.Semantics.TryGetValue(nativeGold.NativeItemIndex, out var goldSemantic)
            || !sidecars.Footprints.TryGetValue(nativeGold.NativeItemIndex, out var goldFootprint))
            throw new InvalidDataException("Native Gold requires an admitted stable identity and verified cursor footprint.");
        var goldDefinitions = replacements.Values.Where(definition =>
            definition.NativeItemIndex == nativeGold.NativeItemIndex
            && string.Equals(definition.StableSymbol, goldSemantic.StableItemSymbol, StringComparison.Ordinal)
            && AuthoritativeItemClassification.IsGold(definition)).ToArray();
        if (goldDefinitions.Length != 1 || string.IsNullOrWhiteSpace(nativeGold.Name)
            || goldFootprint.InventoryWidth != 1 || goldFootprint.InventoryHeight != 1)
            throw new InvalidDataException("Native Gold metadata does not identify one admitted 1x1 stable item.");
        var goldDefinition = goldDefinitions[0];
        var goldStackMetadata = new AuthoritativeNativeGoldStackMetadata(
            nativeGold.NativeItemIndex,
            goldDefinition.ItemId,
            nativeGold.ItemType,
            nativeGold.EquipLocation,
            nativeGold.ItemClass,
            nativeGold.Flags,
            nativeGold.MiscId,
            nativeGold.SpellId,
            nativeGold.Name,
            nativeGold.Name,
            goldFootprint.InventoryWidth,
            goldFootprint.InventoryHeight,
            nativeItems.Cursors.GoldSmall,
            nativeItems.Cursors.GoldMedium,
            nativeItems.Cursors.GoldLarge);
        return baseItems.WithNativePopulationOverlay(replacements, goldStackMetadata);
    }

    private static int GetStableMiscId(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition selected)
        => native.MiscId switch {
            18 => 19,
            19 => 20,
            23 when native.ItemType == 10 && selected.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase) => 23,
            24 => 25,
            25 when native.ItemType == 12 => 26,
            26 when native.ItemType == 13 => 27,
            _ => native.MiscId,
        };

    internal static Devilution.Data.TsvTable CreateSemanticTable(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog baseItems,
        AuthoritativeSpellCatalog spells,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> footprints)
        => CreateSemanticTable(nativeItems, baseItems, spells, footprints, includeNativeJewelry: false);

    internal static Devilution.Data.TsvTable CreateSemanticTable(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog baseItems,
        AuthoritativeSpellCatalog spells,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> footprints,
        bool includeNativeJewelry)
        => CreateSemanticTable(nativeItems, baseItems, spells, footprints,
            includeNativeJewelry, includeNativeDepthItems: false);

    internal static Devilution.Data.TsvTable CreateSemanticTable(
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog baseItems,
        AuthoritativeSpellCatalog spells,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> footprints,
        bool includeNativeJewelry,
        bool includeNativeDepthItems)
    {
        ArgumentNullException.ThrowIfNull(nativeItems);
        ArgumentNullException.ThrowIfNull(baseItems);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(footprints);
        var output = new System.Text.StringBuilder(
            "native_item_index\tstable_item_symbol\tnative_cursor_id\tinventory_width\tinventory_height\tmisc_id\tspell_source_policy\titem_action_kind\n");
        foreach (var (nativeIndex, footprint) in footprints.OrderBy(entry => entry.Key)) {
            var native = nativeItems.Items.SingleOrDefault(item => item.NativeItemIndex == nativeIndex)
                ?? throw new InvalidDataException($"Native item footprint index {nativeIndex} has no canonical raw metadata row.");
            if (footprint.NativeCursorId != checked(native.CursorGraphic + NativeFirstItemCursorOffset))
                throw new InvalidDataException($"Native item footprint index {nativeIndex} has a cursor ID that conflicts with its raw row.");
            // Keep the legacy compiler's exact allowlist and content pins.
            if (!includeNativeJewelry && (native.ItemType == 12 && native.MiscId == 25
                || native.ItemType == 13 && native.MiscId == 26))
                continue;
            // Additional depth items are selected only by the four-floor
            // compiler; legacy L1/L2 semantic tables retain their exact bytes.
            if (native.MiscId == 19 && !includeNativeDepthItems)
                continue;
            if (!TryGetStableSpellId(native, spells, out var stableSpellId))
                continue; // Optional allowlist rows with unsupported spell identities remain unbound.
            var candidates = baseItems.Definitions.Where(definition => definition.NativeItemIndex == nativeIndex
                && MatchesNativeItemDefinition(native, definition, stableSpellId, includeNativeDepthItems: includeNativeDepthItems)).ToArray();
            if (candidates.Length != 1)
                continue; // This table is an explicit allowlist; required rows fail during profile binding.
            var selected = candidates[0];
            var sourcePolicy = native.ItemType == 10 && native.MiscId == 23 && native.SpellId == 0
                && selected.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase)
                ? "native-staff-dynamic"
                : "catalog";
            output.Append(nativeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(selected.StableSymbol).Append('\t')
                .Append(footprint.NativeCursorId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(footprint.InventoryWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(footprint.InventoryHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(GetStableMiscId(native, selected).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(sourcePolicy).Append('\t')
                .Append(ResolveNativeItemAction(native, selected, baseItems, nativeSpellActions: includeNativeDepthItems).ToString()).Append('\n');
        }
        return Devilution.Data.TsvTable.Parse("native_item_semantics.tsv", output.ToString());
    }

    internal static Devilution.Data.TsvTable PrepareNativeProfileStoreTable(
        Devilution.Data.TsvTable storesTable,
        AuthoritativeItemCatalog items,
        AuthoritativeCathedralNativeItemSidecars sidecars,
        bool nativeProgression = false)
    {
        ArgumentNullException.ThrowIfNull(storesTable);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sidecars);
        if (!string.Equals(storesTable.SourcePath, "stores.tsv", StringComparison.Ordinal))
            throw new InvalidDataException("Native profile stock preparation requires the exact stores.tsv table.");
        StoreCatalog.ValidateExternalizedProduction(storesTable);

        var healerStoreId = (uint?)null;
        if (nativeProgression) {
            var declaredHealerRows = storesTable.Rows.Where(row =>
                string.Equals(row.Required("store_role"), "Healer", StringComparison.Ordinal)
                || string.Equals(row.Required("store_symbol"), "healer", StringComparison.Ordinal)).ToArray();
            if (declaredHealerRows.Length < 2
                || declaredHealerRows.Any(row =>
                    !string.Equals(row.Required("store_role"), "Healer", StringComparison.Ordinal)
                    || !string.Equals(row.Required("store_symbol"), "healer", StringComparison.Ordinal)))
                throw new InvalidDataException("Native progression stock requires the exact symbolic Healer store.");
            var healerStoreIds = declaredHealerRows.Select(row => row.RequiredUInt32("store_id")).Distinct().ToArray();
            if (healerStoreIds.Length != 1 || healerStoreIds[0] != 4
                || declaredHealerRows.Count(row => row.RequiredUInt32("store_slot") == 0) != 1
                || declaredHealerRows.Count(row => row.RequiredUInt32("store_slot") == 1) != 1)
                throw new InvalidDataException("Native progression stock requires Pepin store 4 with unique slots 0 and 1.");
            healerStoreId = healerStoreIds[0];
        }

        AuthoritativeItemDefinition ResolvePinnedHealerItem(uint slot)
        {
            var expected = slot switch {
                0 => (Symbol: "potion_healing_standard", ItemId: 2180u, NativeIndex: 77, Price: 50),
                1 => (Symbol: "potion_full_healing", ItemId: 2181u, NativeIndex: 78, Price: 150),
                _ => throw new ArgumentOutOfRangeException(nameof(slot)),
            };
            var definition = items.Definitions.SingleOrDefault(item =>
                    string.Equals(item.StableSymbol, expected.Symbol, StringComparison.Ordinal))
                ?? throw new InvalidDataException($"Native profile stock is missing '{expected.Symbol}'.");
            if (definition.ItemId != expected.ItemId || definition.NativeItemIndex != expected.NativeIndex
                || definition.Value != expected.Price)
                throw new InvalidDataException($"Native profile stock item '{expected.Symbol}' has an unexpected identity or price.");
            return definition;
        }

        var output = new System.Text.StringBuilder();
        output.AppendJoin('\t', storesTable.Columns);
        foreach (var row in storesTable.Rows) {
            var currentId = row.RequiredUInt32("item_id");
            var current = items.GetRequired(currentId);
            if (!string.Equals(current.StableSymbol, row.Required("item_symbol"), StringComparison.Ordinal))
                throw new InvalidDataException("Native profile stock preparation found a mismatched stable item ID and symbol.");
            var healerSlot = healerStoreId == row.RequiredUInt32("store_id")
                && row.Required("store_role") == "Healer"
                && row.Required("store_symbol") == "healer"
                ? row.RequiredUInt32("store_slot")
                : uint.MaxValue;
            AuthoritativeItemDefinition selected;
            AuthoritativeCathedralNativeItemSemantic semantic;
            if (healerSlot is 0 or 1) {
                selected = ResolvePinnedHealerItem(healerSlot);
                if (!sidecars.Semantics.TryGetValue(selected.NativeItemIndex, out semantic!))
                    throw new NotSupportedException(
                        $"Native progression stock item '{selected.StableSymbol}' has no selected raw-item identity.");
            } else {
                if (!sidecars.Semantics.TryGetValue(current.NativeItemIndex, out semantic!))
                    throw new NotSupportedException(
                        $"Native profile stock item '{current.StableSymbol}' has no selected raw-item identity.");
                selected = items.Definitions.SingleOrDefault(definition =>
                        definition.NativeItemIndex == current.NativeItemIndex
                        && string.Equals(definition.StableSymbol, semantic.StableItemSymbol, StringComparison.Ordinal))
                    ?? throw new InvalidDataException(
                        $"Native profile stock selection '{semantic.StableItemSymbol}' is not a catalog identity for raw item {current.NativeItemIndex}.");
            }
            if (!sidecars.Semantics.TryGetValue(selected.NativeItemIndex, out var selectedSemantic)
                || !string.Equals(selectedSemantic.StableItemSymbol, selected.StableSymbol, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Native profile stock selection '{selected.StableSymbol}' does not match its explicit item semantics.");
            semantic = selectedSemantic;
            if (!sidecars.Footprints.TryGetValue(selected.NativeItemIndex, out var footprint)
                || semantic.NativeCursorId != footprint.NativeCursorId
                || semantic.InventoryWidth != footprint.InventoryWidth
                || semantic.InventoryHeight != footprint.InventoryHeight)
                throw new InvalidDataException(
                    $"Native profile stock item '{selected.StableSymbol}' has no matching captured cursor footprint.");
            if (!string.Equals(semantic.StableItemSymbol, selected.StableSymbol, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Native profile stock selection '{semantic.StableItemSymbol}' does not match the prepared identity '{selected.StableSymbol}'.");

            output.Append('\n');
            foreach (var column in storesTable.Columns) {
                if (column == "item_id")
                    output.Append(selected.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                else if (column == "item_symbol")
                    output.Append(selected.StableSymbol);
                else if (nativeProgression && column == "price" && (healerSlot is 0 or 1))
                    output.Append(selected.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                else
                    output.Append(row.Required(column));
                output.Append('\t');
            }
            output.Length--;
        }
        return Devilution.Data.TsvTable.Parse(storesTable.SourcePath, output.ToString());
    }

    /** Declares the two pinned healer rows only in the explicit progression content pack. */
    internal static Devilution.Data.TsvTable CreateNativeProgressionStoreCapabilityTable()
        => Devilution.Data.TsvTable.Parse(
            "native_pinned_stores.tsv",
            "store_id\tstore_symbol\tstore_role\tstore_slot\titem_id\titem_symbol\tnative_item_index\tprice\n"
                + "4\thealer\tHealer\t0\t2180\tpotion_healing_standard\t77\t50\n"
                + "4\thealer\tHealer\t1\t2181\tpotion_full_healing\t78\t150\n");

    private static DiabloItemSpellSourceKind? ResolveNativeItemSpellSource(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition content,
        string sourcePolicy)
    {
        if (native.ItemType == 10 && native.MiscId == 23 && native.SpellId == 0
            && content.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(sourcePolicy, "native-staff-dynamic", StringComparison.Ordinal)
                || content.MiscId != 23 || content.SpellSourceKind is not null)
                throw new InvalidDataException("Native dynamic staff semantics require the explicit null-source staff overlay.");
            return null;
        }
        if (!string.Equals(sourcePolicy, "catalog", StringComparison.Ordinal))
            throw new InvalidDataException("Native item semantics request an unsupported spell-source policy.");
        if (!SourceSemanticsMatch(native, content))
            throw new InvalidDataException("Native item spell-source semantics do not match the effective catalog.");
        return content.SpellSourceKind;
    }

    private static DiabloItemActionKind ResolveNativeItemAction(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition content,
        AuthoritativeItemCatalog contentItems,
        bool nativeSpellActions = false)
    {
        // Native scrolls dispatch their bound spell. Misc21's legacy Identify
        // fallback is retained only for older semantic tables.
        if (nativeSpellActions && native.MiscId == 21 && native.SpellId != 0 && native.SpellId != 5)
            return DiabloItemActionKind.None;
        if (native.MiscId == 21 && native.SpellId == 5)
            return DiabloItemActionKind.Identify;
        if (native.MiscId == 24
            && content.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase))
            return DiabloItemActionKind.ReadBook;
        if (content.ItemActionKind != DiabloItemActionKind.None)
            return content.ItemActionKind;
        return contentItems.TryGetItemActionMetadataByMiscId(
            GetStableMiscId(native, content), out var miscAction, out _)
            ? miscAction
            : DiabloItemActionKind.None;
    }

    private static bool TryGetStableSpellId(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeSpellCatalog spells,
        out int stableSpellId)
    {
        stableSpellId = 0;
        if (native.MiscId == 21 && native.SpellId == 5 || native.SpellId == 0)
            return true;
        if (native.SpellId < 0
            || !spells.TryGetByNativeId(checked((uint)native.SpellId), out var mappedSpell))
            return false;
        stableSpellId = checked((int)mappedSpell.SpellId);
        return true;
    }

    private static bool TryGetStableItemType(int nativeType, out int stableType, out string requiredTag)
    {
        switch (nativeType) {
        case 0:
            stableType = 0;
            requiredTag = "Misc";
            return true;
        case 1:
            stableType = 1;
            requiredTag = "Sword";
            return true;
        case 2:
            stableType = 1;
            requiredTag = "Axe";
            return true;
        case 3:
            stableType = 1;
            requiredTag = "Bow";
            return true;
        case 4:
            stableType = 1;
            requiredTag = "Mace";
            return true;
        case 5:
            stableType = 7;
            requiredTag = "Shield";
            return true;
        case 6:
            stableType = 7;
            requiredTag = "LightArmor";
            return true;
        case 7:
            stableType = 7;
            requiredTag = "Helmet";
            return true;
        case 8:
            stableType = 7;
            requiredTag = "MediumArmor";
            return true;
        case 9:
            stableType = 7;
            requiredTag = "HeavyArmor";
            return true;
        case 10:
            stableType = 1;
            requiredTag = "Staff";
            return true;
        case 11:
            stableType = 11;
            requiredTag = "Gold";
            return true;
        case 12:
            stableType = 12;
            requiredTag = "Ring";
            return true;
        case 13:
            stableType = 13;
            requiredTag = "Amulet";
            return true;
        default:
            stableType = 0;
            requiredTag = string.Empty;
            return false;
        }
    }

    internal static IReadOnlyDictionary<int, int> BuildSpellBindings(
        AuthoritativeSpellCatalog spells)
    {
        var result = new Dictionary<int, int> { [0] = 0 };
        foreach (var definition in spells.Definitions.Where(definition => definition.NativeSpellId != 0)) {
            if (!result.TryAdd(checked((int)definition.NativeSpellId), checked((int)definition.SpellId)))
                throw new InvalidDataException($"Native spell ordinal {definition.NativeSpellId} has ambiguous stable spell identities.");
        }
        return result;
    }

    internal static DiabloCathedralItemCheckpoint BuildItemCheckpoint(
        AuthoritativeCathedralThemeRoomState state,
        uint levelId,
        int depth,
        string generationIdentity,
        string metadataFingerprint,
        uint randomState,
        IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> itemBindings,
        uint itemEntityIdBase)
    {
        if (state.ItemSlots.Count != AuthoritativeCathedralInitialItemGenerator.PoolCapacity
            || state.ActiveItemCount is < 0 or > AuthoritativeCathedralInitialItemGenerator.ActiveCapacity
            || state.ActiveItemIds.Count != AuthoritativeCathedralInitialItemGenerator.ActiveCapacity
            || state.ItemRefs.Any(value => value < 0))
            throw new NotSupportedException("The native item pool or signed item-reference form is outside the current runtime contract.");
        var items = state.ItemSlots.Select(item => item with { }).ToArray();
        var activeSlots = state.ActiveItemIds.Take(state.ActiveItemCount).ToHashSet();
        var references = state.ItemRefs.ToArray();
        var bindings = new DiabloCathedralItemSlotEntityBinding[AuthoritativeCathedralInitialItemGenerator.PoolCapacity];
        for (var slot = 0; slot < bindings.Length; slot++) {
            var visible = references.Any(value => value == slot + 1);
            if (visible && (!activeSlots.Contains(slot) || !itemBindings.ContainsKey(items[slot].ItemIndex)))
                throw new InvalidDataException("A visible native item has no active slot or stable item binding.");
            bindings[slot] = new(slot, visible ? checked(itemEntityIdBase + (uint)slot) : 0);
        }
        var level = new DiabloCathedralItemLevelCheckpoint(
            levelId, depth, items, state.ActiveItemIds.ToArray(), state.ActiveItemCount,
            references, bindings, randomState, generationIdentity);
        return new DiabloCathedralItemCheckpoint(
            metadataFingerprint,
            UnboundGenerationFingerprint,
            state.UniqueItemFlags.Order().ToArray(),
            [level]);
    }

    private static AuthoritativeCathedralItemLevelProfile BuildItemLevelProfile(
        AuthoritativeCathedralThemeRoomState state,
        uint levelId,
        int depth,
        IReadOnlyList<DiabloCathedralItemSlotEntityBinding> itemBindings,
        IReadOnlyList<AuthoritativeWorldObject> objects)
    {
        var objectBindings = objects.Select((value, index) => {
            var raw = state.ObjectSlots[state.ActiveObjectIds[index]];
            var flags = (raw.AnimFlag != 0 ? 1 : 0)
                | (raw.SolidFlag ? 2 : 0)
                | (raw.MissilePassable ? 4 : 0)
                | (raw.ApplyLighting ? 8 : 0)
                | (raw.BreakFlag != 0 ? 32 : 0);
            return new AuthoritativeCathedralItemObjectBinding(
                raw.Slot, value.EntityId, raw.Type, raw.Position.X, raw.Position.Y,
                raw.SolidFlag, raw.SelectionRegion, flags,
                raw.DoorFlag ? AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor
                    : AuthoritativeCathedralNativePopulationPolicy.IsSupportedNativeChest(state, raw)
                        ? AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                        : AuthoritativeCathedralNativePopulationPolicy.IsSupportedNativeSarcophagus(state, raw)
                            ? AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                            : AuthoritativeCathedralNativePopulationPolicy.IsSupportedNativeBarrel(state, raw)
                                ? AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel
                                : AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused);
        }).ToArray();
        var solid = state.Pieces.Select((piece, index) => {
            if (piece >= state.PieceProperties.Count)
                throw new InvalidDataException("Native piece property identity exceeds its retained table.");
            return (state.PieceProperties[piece] & (byte)AuthoritativeDunTileFlags.Solid) != 0;
        }).ToArray();
        var setPiece = state.TileFlags.Select(flags => (flags & TilePopulated) != 0).ToArray();
        if (itemBindings.Count != AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            throw new InvalidDataException("Native item entity bindings must preserve all 128 source slots.");
        return new(levelId, depth, solid, setPiece, state.ObjectRefs.ToArray(), objectBindings) {
            PreserveSparseNativeObjectSlots = true,
        };
    }
}

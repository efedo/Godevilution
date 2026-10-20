using System.Security.Cryptography;
using System.Text.Json;
using Devilution.Data;

namespace Devilution.Diablo;

/** Detached L5-L8 owner construction; publication remains the world registry's responsibility. */
internal sealed record AuthoritativeCatacombsNativePopulationBuild(
    AuthoritativeCatacombsLevelReadyResult Ready,
    string MonsterMetadataFingerprint,
    AuthoritativeCatacombsMonsterRuntimeAdapterResult Monsters,
    IReadOnlyList<AuthoritativeWorldObject> Objects,
    AuthoritativeCathedralItemRuntimeProfile Items,
    AuthoritativeCatacombsNativePopulationPolicy Policy,
    AuthoritativeCatacombsDoorCatalog DoorCatalog,
    IReadOnlyDictionary<string, int> NativeAiCounts,
    int PlayerMinionCount,
    int ScavengerCount);

internal static class AuthoritativeCatacombsNativePopulationBuilder
{
    private const uint MonsterEntityIdBase = 0x09001000;
    private const uint ObjectEntityIdBase = 0x09002000;
    private const uint NativeObjectIdBase = 0x09000000;
    private const uint ItemEntityIdBase = 0x0A001000;
    private const byte TilePopulated = 1 << 3;
    private const string UniqueIdentityPrefix = "native-catacombs-unique-";
    private static readonly string UnboundGenerationFingerprint = new('0', 64);

    internal static AuthoritativeCatacombsNativePopulationBuild Build(
        AuthoritativeCatacombsLevelReadyResult ready,
        uint stableLevelId,
        AuthoritativeCathedralNativeCharacterStart characterStart,
        AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells,
        IReadOnlyCollection<AuthoritativeItemState> carriedItems,
        IReadOnlyCollection<AuthoritativeItemState> storeItems,
        AuthoritativeCathedralNativeItemSidecars itemSidecars)
    {
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(characterStart);
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        ArgumentNullException.ThrowIfNull(playerClasses);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        ArgumentNullException.ThrowIfNull(contentItems);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(carriedItems);
        ArgumentNullException.ThrowIfNull(storeItems);
        ArgumentNullException.ThrowIfNull(itemSidecars);
        if (stableLevelId == 0)
            throw new ArgumentOutOfRangeException(nameof(stableLevelId));

        var fresh = ready.Request.FreshLevel;
        var request = fresh.Request;
        var metadata = request.Metadata;
        var depth = request.Depth;
        if (depth is < 5 or > 8 || ready.Stage != AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin
            || ready.Request.FreshLevel.Geometry.GenerationResult.Depth != depth
            || ready.Request.FreshLevel.FullPopulationRequest.InitialPopulation.Depth != depth
            || ready.Player.Level != depth || !ready.Player.Active || !ready.Player.LevelChanging)
            throw new InvalidDataException("Catacombs population requires the matching prepared ordinary L5-L8 state.");
        if (!characterStart.HasCompiledTable || characterStart.ClassId is not (0U or 2U))
            throw new NotSupportedException("Native Catacombs requires an explicit compiled Warrior or Sorcerer class profile.");
        var playerClass = playerClasses.GetRequired(characterStart.ClassId);
        var expectedSymbol = characterStart.ClassId == 0 ? "warrior" : "sorcerer";
        if (playerClass.Availability != DiabloPlayerClassAvailability.BaseGame
            || !string.Equals(playerClass.Symbol, expectedSymbol, StringComparison.Ordinal)
            || !string.Equals(characterStart.ClassSymbol, playerClass.Symbol, StringComparison.Ordinal))
            throw new InvalidDataException("Native Catacombs class ID and symbol must match its active base-game class catalog.");
        if (!ReferenceEquals(metadata.Items.Catalog, fresh.FullPopulationRequest.ItemCatalog)
            || !metadata.UniqueMonsters.Definitions.SequenceEqual(fresh.FullPopulationRequest.OrderedUniqueDefinitions)
            || !metadata.Population.Monsters.Select(value => value.NativeMonsterId)
                .SequenceEqual(fresh.InitialPopulation.BestiaryParameters.Catalog.Select(value => value.NativeMonsterId)))
            throw new InvalidDataException("Native Catacombs ready inputs no longer match their qualified population metadata.");
        if (request.Hero.IdleFrames != characterStart.Hero.IdleFrames)
            throw new InvalidDataException("Native Catacombs animation timing must match the bound class profile.");

        var entityOffset = checked((uint)(depth - 1) * 0x10000U);
        var state = ready.State;
        var monsterMetadataFingerprint = AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(
            metadata.Fingerprint, metadata.UniqueMonsters.Fingerprint, runtimeCatalog);
        var uniqueIdentities = metadata.UniqueMonsters.Definitions.ToDictionary(
            definition => definition.NativeUniqueId,
            definition => UniqueIdentityPrefix + definition.NativeUniqueId.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
        var monsterBindings = new List<AuthoritativeCathedralMonsterRuntimeBinding>(state.ActiveMonsterCount);
        foreach (var slot in state.ActiveMonsterIds.Take(state.ActiveMonsterCount)) {
            if ((uint)slot >= (uint)state.MonsterSlots.Count)
                throw new InvalidDataException("Catacombs monster order references a slot outside its retained pool.");
            var raw = state.MonsterSlots[slot];
            if (raw.Type < 0)
                throw new InvalidDataException("Catacombs active monster type cannot be negative.");
            var nativeType = checked((uint)raw.Type);
            AuthoritativeMonsterTypeDefinition type = null!;
            var bound = monsterTypes.TryGetNative(nativeType, out type);
            if (!bound && monsterTypes.IsNativeMonsterIdAmbiguous(nativeType)) {
                var nativeAi = AuthoritativeCathedralMonsterRuntimeAdapter.GetNativeAi(raw.Ai);
                var exact = monsterTypes.Definitions.Where(row => row.NativeMonsterId == nativeType
                    && string.Equals(row.NativeAi, nativeAi, StringComparison.Ordinal)).ToArray();
                if (exact.Length == 1) { type = exact[0]; bound = true; }
            }
            if (!bound)
                throw new InvalidDataException($"Catacombs monster slot {slot}, raw type {raw.Type}, has no exact stable type binding.");
            monsterBindings.Add(new AuthoritativeCathedralMonsterRuntimeBinding(
                slot,
                checked(MonsterEntityIdBase + entityOffset + (uint)slot),
                type.MonsterId,
                stableLevelId));
        }
        var monsters = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            depth, stableLevelId, state, runtimeCatalog, monsterMetadataFingerprint, monsterBindings,
            metadata.Population.Monsters, DiabloDifficulty.Normal, metadata.UniqueMonsters.Definitions,
            monsterTypes, uniqueIdentities);
        var objects = BuildObjects(state, stableLevelId, depth,
            checked(ObjectEntityIdBase + entityOffset), metadata.Population.Objects);

        var hasUniqueLoot = monsters.SharedRuntime.Records.Any(record => record.Target?.IsUnique == true);
        var supportedLootLevels = monsters.SharedRuntime.Records
            .Where(record => record.Target is { IsPlayerMinion: false })
            .Select(record => unchecked((sbyte)record.Target!.MonsterLevel))
            .Distinct()
            .ToArray();
        var itemBindings = AuthoritativeCathedralNativePopulationBuilder.BuildItemBindings(
            metadata.Items.Catalog, contentItems, spells, state, supportedLootLevels,
            carriedItems, storeItems, itemSidecars, hasUniqueLoot, includeCompleteAllowlist: true,
            familyContext: AuthoritativeNativeDungeonFamilyContext.Catacombs(depth));
        var spellBindings = AuthoritativeCathedralNativePopulationBuilder.BuildSpellBindings(spells);
        var itemCheckpoint = AuthoritativeCathedralNativePopulationBuilder.BuildItemCheckpoint(
            state, stableLevelId, depth, ComputeGeometryFingerprint(fresh, stableLevelId),
            metadata.Items.Fingerprint, fresh.Population.FinalRandomState, itemBindings,
            checked(ItemEntityIdBase + entityOffset));
        var itemProfile = new AuthoritativeCathedralItemRuntimeProfile(
            metadata.Items.Fingerprint,
            UnboundGenerationFingerprint,
            itemCheckpoint,
            metadata.Items.Catalog,
            fresh.FullPopulationRequest.ItemPolicy,
            runtimeCatalog,
            metadata.UniqueMonsters.Definitions,
            itemBindings,
            new AuthoritativeCathedralItemInventoryDimensions(),
            [BuildItemLevelProfile(state, stableLevelId, depth,
                itemCheckpoint.Levels[0].SlotEntityBindings, objects)],
            spellBindings);

        var policy = new AuthoritativeCatacombsNativePopulationPolicy(
            ready, stableLevelId, characterStart, monsters, objects, itemProfile);
        var boundCheckpoint = itemCheckpoint with { GenerationFingerprint = policy.Fingerprint };
        itemProfile = itemProfile with {
            GenerationFingerprint = policy.Fingerprint,
            InitialCheckpoint = boundCheckpoint,
        };
        policy.ValidateItemProfile(itemProfile);

        var targets = monsters.SharedRuntime.Records.Where(record => record.Target is not null)
            .Select(record => record.Target!).ToArray();
        var aiCounts = targets.GroupBy(target => target.NativeAi, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new(ready, monsterMetadataFingerprint, monsters, objects, itemProfile, policy,
            policy.DoorCatalog, aiCounts, targets.Count(target => target.IsPlayerMinion),
            targets.Count(target => string.Equals(target.NativeAi, "Scavenger", StringComparison.Ordinal)));
    }

    private static IReadOnlyList<AuthoritativeWorldObject> BuildObjects(
        AuthoritativeCathedralThemeRoomState state,
        uint levelId,
        int depth,
        uint objectEntityIdBase,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> definitions)
    {
        if (state.ActiveObjectCount is < 0 or > AuthoritativeCathedralThemeRoomState.ObjectCapacity
            || state.ActiveObjectIds.Count < state.ActiveObjectCount
            || definitions.Count != AuthoritativeCathedralPopulationCatalog.ObjectCatalogCount)
            throw new InvalidDataException("Catacombs objects require the complete native catalog and active pool.");
        var activeSlots = state.ActiveObjectIds.Take(state.ActiveObjectCount).ToArray();
        if (activeSlots.Any(slot => (uint)slot >= (uint)state.ObjectSlots.Count)
            || activeSlots.Distinct().Count() != activeSlots.Length)
            throw new InvalidDataException("Catacombs object order must retain distinct in-range pool slots.");

        var result = new List<AuthoritativeWorldObject>(state.ActiveObjectCount);
        var doorPositions = new HashSet<(int X, int Y)>();
        foreach (var slot in activeSlots) {
            var raw = state.ObjectSlots[slot];
            if (raw.Slot != slot || raw.Type < 0 || (uint)raw.Type >= (uint)definitions.Count
                || raw.AnimFrame > byte.MaxValue)
                throw new InvalidDataException("A Catacombs active object row lost its native slot, type, or frame bounds.");
            var definition = definitions[raw.Type];
            if ((raw.Type is 42 or 43) != raw.DoorFlag
                || raw.DoorFlag && raw.Type != (int)AuthoritativeCatacombsDoorCatalog.LeftNativeObjectId
                    && raw.Type != (int)AuthoritativeCatacombsDoorCatalog.RightNativeObjectId)
                throw new InvalidDataException("Catacombs door type and raw door flag must remain paired.");
            if (raw.DoorFlag && !doorPositions.Add((raw.Position.X, raw.Position.Y)))
                throw new InvalidDataException("Native Catacombs doors must have unique owner cells.");
            if (definition.NativeType != raw.Type || definition.AnimWidth != raw.AnimWidth)
                throw new InvalidDataException("A Catacombs object no longer matches its native type or sprite width.");

            var entityId = checked(objectEntityIdBase + (uint)slot);
            var objectId = checked(NativeObjectIdBase + (uint)raw.Type + 1);
            result.Add(new AuthoritativeWorldObject(
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
                animationFrame: checked((int)raw.AnimFrame)));
        }
        if (result.Count != state.ActiveObjectCount)
            throw new InvalidDataException("Catacombs object projection omitted an active native slot.");
        _ = depth; // Depth is bound by the separate Catacombs door registry, not an object component.
        return Array.AsReadOnly(result.ToArray());
    }

    private static AuthoritativeCathedralItemLevelProfile BuildItemLevelProfile(
        AuthoritativeCathedralThemeRoomState state,
        uint levelId,
        int depth,
        IReadOnlyList<DiabloCathedralItemSlotEntityBinding> itemBindings,
        IReadOnlyList<AuthoritativeWorldObject> objects)
    {
        if (objects.Count != state.ActiveObjectCount)
            throw new InvalidDataException("Catacombs item object profiles must retain every active object row.");
        var objectBindings = new AuthoritativeCathedralItemObjectBinding[objects.Count];
        for (var index = 0; index < objects.Count; index++) {
            var slot = state.ActiveObjectIds[index];
            var raw = state.ObjectSlots[slot];
            var value = objects[index];
            var flags = (raw.AnimFlag != 0 ? 1 : 0)
                | (raw.SolidFlag ? 2 : 0)
                | (raw.MissilePassable ? 4 : 0)
                | (raw.ApplyLighting ? 8 : 0)
                | (raw.BreakFlag != 0 ? 32 : 0);
            var mutationPolicy = raw.DoorFlag
                ? AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor
                : AuthoritativeNativePopulationBindings.IsSupportedNativeCatacombsChest(state, raw)
                    ? AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                    : AuthoritativeNativePopulationBindings.IsSupportedNativeSarcophagus(state, raw)
                        ? AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus
                        : AuthoritativeNativePopulationBindings.IsSupportedNativeBarrel(state, raw)
                            ? AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel
                            : AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused;
            objectBindings[index] = new(raw.Slot, value.EntityId, raw.Type,
                raw.Position.X, raw.Position.Y, raw.SolidFlag, raw.SelectionRegion,
                flags, mutationPolicy);
        }
        var solid = state.Pieces.Select((piece, index) => {
            if (piece >= state.PieceProperties.Count)
                throw new InvalidDataException("Catacombs item placement references a missing effective SOL row.");
            return (state.PieceProperties[piece] & (byte)AuthoritativeDunTileFlags.Solid) != 0;
        }).ToArray();
        var setPiece = state.TileFlags.Select(flags => (flags & TilePopulated) != 0).ToArray();
        if (itemBindings.Count != AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            throw new InvalidDataException("Catacombs item entity bindings must preserve all native item slots.");
        return new(levelId, depth, solid, setPiece, state.ObjectRefs.ToArray(), objectBindings) {
            PreserveSparseNativeObjectSlots = true,
        };
    }

    private static string ComputeGeometryFingerprint(AuthoritativeCatacombsFreshLevelResult fresh, uint levelId)
    {
        var identity = new {
            levelId,
            fresh.Request.Depth,
            fresh.Request.StableLevelSymbol,
            fresh.Geometry.GenerationResult,
            Pieces = fresh.Geometry.Level.NativePieceLayer,
            Special = fresh.Geometry.Level.NativeSpecialLayer,
            Sol = fresh.Geometry.EffectivePieceProperties,
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
    }
}

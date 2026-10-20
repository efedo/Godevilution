using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/** Immutable native L5-L8 owner for the explicit quest-disabled Catacombs family. */
internal sealed class AuthoritativeCatacombsNativePopulationPolicy
{
    internal const string PolicyId = "native-catacombs-l5-l8-population-v1";
    private readonly AuthoritativeNativePopulationBindings bindings;

    internal AuthoritativeCatacombsNativePopulationPolicy(
        AuthoritativeCatacombsLevelReadyResult ready,
        uint levelId,
        AuthoritativeCathedralNativeCharacterStart characterStart,
        AuthoritativeCatacombsMonsterRuntimeAdapterResult monsters,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        AuthoritativeCathedralItemRuntimeProfile itemProfile)
    {
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(characterStart);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(itemProfile);
        if (levelId == 0)
            throw new ArgumentOutOfRangeException(nameof(levelId));

        var fresh = ready.Request.FreshLevel;
        var request = fresh.Request;
        var metadata = request.Metadata;
        var depth = request.Depth;
        if (depth is < 5 or > 8 || ready.Stage != AuthoritativeCathedralLevelReadyStage.PreparedBeforeJoin
            || ready.Request.FreshLevel.Geometry.GenerationResult.Depth != depth
            || ready.Request.FreshLevel.FullPopulationRequest.InitialPopulation.Depth != depth
            || ready.State.ActiveMonsterCount != monsters.SharedRuntime.ActiveMonsterCount
            || ready.State.ActiveObjectCount != objects.Count
            || monsters.Depth != depth || monsters.LevelId != levelId
            || !characterStart.HasCompiledTable || characterStart.ClassId is not (0 or 2)
            || request.Hero.IdleFrames != characterStart.Hero.IdleFrames)
            throw new InvalidDataException("Native Catacombs ownership requires one qualified class-bound, prepared L5-L8 floor.");

        var sourceClass = characterStart.ClassId == 0 ? "warrior" : "sorcerer";
        if (!string.Equals(characterStart.ClassSymbol, sourceClass, StringComparison.Ordinal))
            throw new InvalidDataException("Native Catacombs class identity must be the qualified Warrior or Sorcerer.");

        Depth = depth;
        LevelId = levelId;
        CharacterClassId = characterStart.ClassId;
        MetadataFingerprint = metadata.Fingerprint;
        MonsterMetadataFingerprint = monsters.SharedRuntime.MetadataFingerprint;
        DoorCatalog = AuthoritativeCatacombsDoorCatalog.Load(metadata);
        DoorCatalogFingerprint = DoorCatalog.Fingerprint;
        InitialState = ready.State;
        InitialDoorCount = InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount)
            .Count(slot => InitialState.ObjectSlots[slot].DoorFlag);
        InitialSpecialStandActors = Array.AsReadOnly(monsters.SharedRuntime.InitialSpecialStandActors.ToArray());

        var materialized = AuthoritativeCatacombsGeometryMaterializer.Materialize(levelId,
            fresh.Geometry.GenerationResult, metadata.Geometry.Pieces, metadata.EffectivePieceProperties,
            request.StableLevelSymbol);
        ValidateReadyGeometry(materialized.Level, InitialState, metadata);

        var blocked = new List<int>();
        var missile = new List<int>();
        var vision = new List<int>();
        for (var cell = 0; cell < InitialState.Pieces.Count; cell++) {
            var piece = InitialState.Pieces[cell];
            if (piece >= metadata.EffectivePieceProperties.Count)
                throw new InvalidDataException("Catacombs fine geometry references a missing effective SOL row.");
            var flags = (AuthoritativeDunTileFlags)(metadata.EffectivePieceProperties[piece] & 0xBF);
            if ((flags & AuthoritativeDunTileFlags.Solid) != 0) blocked.Add(cell);
            if ((flags & AuthoritativeDunTileFlags.BlockMissile) != 0) missile.Add(cell);
            if ((flags & AuthoritativeDunTileFlags.BlockLight) != 0) vision.Add(cell);
        }
        var backing = AuthoritativeCathedralDoors.CopyLevel(materialized.Level,
            pieces: InitialState.Pieces,
            special: InitialState.Special.Select(value => checked((ushort)value)),
            blocked: blocked,
            missile: missile,
            nativeBlocked: blocked,
            nativeMissile: missile,
            vision: vision);
        var objectRows = InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount)
            .Select(slot => InitialState.ObjectSlots[slot]).ToDictionary(row => row.Slot);
        foreach (var doorRaw in objectRows.Values.Where(value => value.DoorFlag)) {
            var projection = objects.SingleOrDefault(value => value.NativeObjectId == (uint)doorRaw.Type
                && value.PositionX == doorRaw.Position.X && value.PositionY == doorRaw.Position.Y)
                ?? throw new InvalidDataException("A Catacombs door row has no persistent plain object projection.");
            _ = AuthoritativeCatacombsDoors.BindRetained(backing, checked((uint)depth), DoorCatalog,
                InitialState, doorRaw, projection);
        }
        bindings = new AuthoritativeNativePopulationBindings(
            AuthoritativeNativePopulationFamily.Catacombs, depth, InitialState,
            monsters.SharedRuntime, objects, backing,
            metadata.Population.Monsters.Select(value => value.PopulationDefinition).ToArray(),
            itemProfile,
            cathedralPolicy: null,
            catacombsDoorCatalogFingerprint: DoorCatalogFingerprint);

        var rawIdentity = JsonSerializer.SerializeToNode(InitialState)!.AsObject();
        rawIdentity["UniqueItemFlags"] = JsonSerializer.SerializeToNode(InitialState.UniqueItemFlags.Order().ToArray());
        var freshIdentity = new {
            request.Depth,
            request.DungeonSeed,
            request.StorySeed,
            request.Entry,
            request.RetainedLevelSeed,
            request.StableLevelSymbol,
            request.Hero,
            InitialUniqueItemHistory = request.InitialUniqueItemFlags.Order().ToArray(),
            Generation = fresh.Geometry.GenerationResult,
            Geometry = new {
                FinePieces = fresh.Geometry.Level.NativePieceLayer,
                FineSpecial = fresh.Geometry.Level.NativeSpecialLayer,
                EffectiveSol = fresh.Geometry.EffectivePieceProperties,
                fresh.Geometry.Triggers,
            },
            PlayerPreparation = fresh.PlayerPreparation,
            PopulationRandomState = fresh.Population.FinalRandomState,
            fresh.MonsterClearFinalRandomState,
        };
        var readyIdentity = new {
            State = rawIdentity,
            ready.Player,
            ready.Corpses,
            ready.BestiaryCorpseIds,
            ready.BloodSpatterSlot,
            ready.StoneCorpseId,
            ready.VisionRecords,
            ready.VisionActive,
            ready.UpdateVision,
            ready.VisibleTransparencyRegions,
            ready.AutomapExploration,
            ready.SpellFlags,
            ready.Infravision,
            ready.MissilePreFlag,
            ready.AutoMapShowItems,
            ready.MissilesCleared,
            ready.FinalRandomState,
            ready.MonsterClearRandomState,
            ready.Stage,
        };
        var identity = new {
            PolicyId,
            Depth,
            LevelId,
            CharacterClassId = characterStart.ClassId,
            CharacterClassSymbol = characterStart.ClassSymbol,
            characterStart.HasCompiledTable,
            CharacterStartHero = characterStart.Hero,
            MetadataFingerprint,
            ManifestHash = metadata.ManifestHash,
            DoorCatalogFingerprint = DoorCatalog.Fingerprint,
            MonsterMetadataFingerprint,
            ItemMetadataFingerprint = metadata.Items.Fingerprint,
            ItemBindingFingerprint = bindings.ItemBindingFingerprint,
            Fresh = freshIdentity,
            Ready = readyIdentity,
            Initializations = monsters.SharedRuntime.Records.Select(value => value.Initialization).ToArray(),
            LiveActorClassifications = bindings.LiveNativeAis.OrderBy(value => value.Key).ToArray(),
            Objects = objects.Select(DiabloSimulation.CaptureObject).ToArray(),
        };
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
        bindings.BindGenerationFingerprint(Fingerprint);
    }

    internal int Depth { get; }
    internal uint LevelId { get; }
    internal uint CharacterClassId { get; }
    internal string Fingerprint { get; }
    internal string MetadataFingerprint { get; }
    internal string MonsterMetadataFingerprint { get; }
    internal string DoorCatalogFingerprint { get; }
    internal int InitialDoorCount { get; }
    internal AuthoritativeCatacombsDoorCatalog DoorCatalog { get; }
    internal AuthoritativeCathedralThemeRoomState InitialState { get; }
    internal IReadOnlyList<AuthoritativeNativeInitialSpecialStand> InitialSpecialStandActors { get; }
    internal IReadOnlyList<AuthoritativeNativeMonsterInitialization> Reservations => bindings.Reservations;
    internal IEnumerable<uint> BoundEntityIds => bindings.BoundEntityIds;
    internal AuthoritativeNativePopulationBindings Bindings => bindings;

    internal bool ReservesEntityId(uint entityId) => bindings.ReservesEntityId(entityId);
    internal AuthoritativeLevel CreateBackingLevel() => bindings.CreateBackingLevel();
    internal void ValidateBackingLevel(AuthoritativeLevel level, bool allowOmittedSpecial = false)
        => bindings.ValidateBackingLevel(level, allowOmittedSpecial);
    internal uint? ObjectEntityAt(int x, int y) => bindings.ObjectEntityAt(x, y);
    internal IEnumerable<(uint LevelId, int X, int Y)> SolidObjectPositions() => bindings.SolidObjectPositions();
    internal bool ObjectIsSolid(uint entityId) => bindings.ObjectIsSolid(entityId);
    internal void ValidateObject(AuthoritativeWorldObject value) => bindings.ValidateObject(value);
    internal void ValidateObjectProjection(DiabloObjectState value) => bindings.ValidateObjectProjection(value);
    internal bool CanOperate(AuthoritativeWorldObject value) => bindings.CanOperate(value);
    internal bool CanOperateNativeChest(AuthoritativeWorldObject value) => bindings.CanOperateNativeChest(value);
    internal bool TryGetNativeChestLootParameters(AuthoritativeWorldObject value,
        out int nativeType, out uint randomSeed, out int itemCount, out int itemBranch)
        => bindings.TryGetNativeChestLootParameters(value, out nativeType, out randomSeed, out itemCount, out itemBranch);
    internal bool TryCommitNativeChestOpen(AuthoritativeWorldObject value) => bindings.TryCommitNativeChestOpen(value);
    internal void ValidateWorld(DiabloWorldState world) => bindings.ValidateWorld(world);
    internal void ValidateItemProfile(AuthoritativeCathedralItemRuntimeProfile items)
        => bindings.ValidateItemProfile(items);

    private static void ValidateReadyGeometry(
        AuthoritativeLevel generated,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCatacombsPopulationMetadata metadata)
    {
        if (state.Pieces.Count != AuthoritativeCatacombsGeometryMaterializer.NativeWidth
                * AuthoritativeCatacombsGeometryMaterializer.NativeHeight
            || state.Special.Count != state.Pieces.Count
            || state.Transparency.Count != state.Pieces.Count
            || !state.PieceProperties.SequenceEqual(metadata.EffectivePieceProperties))
            throw new InvalidDataException("Catacombs ready state must retain complete native fine layers and effective SOL.");

        var doorCells = new HashSet<int>();
        foreach (var slot in state.ActiveObjectIds.Take(state.ActiveObjectCount)) {
            var raw = state.ObjectSlots[slot];
            if (!raw.DoorFlag) continue;
            if (raw.Type is not (42 or 43)
                || (uint)raw.Position.X >= (uint)generated.Width
                || (uint)raw.Position.Y >= (uint)generated.Height
                || !doorCells.Add(raw.Position.Y * generated.Width + raw.Position.X))
                throw new InvalidDataException("Catacombs door rows require distinct in-range native 42/43 positions.");
        }

        for (var cell = 0; cell < state.Pieces.Count; cell++) {
            var transparency = checked((ushort)unchecked((byte)state.Transparency[cell]));
            if (transparency != generated.NativeTransparency[cell])
                throw new InvalidDataException("Catacombs ready transparency diverged from its generated fine layer.");
            if (doorCells.Contains(cell)) continue;
            if (state.Pieces[cell] != generated.NativePieceLayer[cell]
                || state.Special[cell] != generated.NativeSpecialLayer[cell])
                throw new InvalidDataException("Catacombs ready geometry changed outside a bound native door cell.");
        }

    }
}

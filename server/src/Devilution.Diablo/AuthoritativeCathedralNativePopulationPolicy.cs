using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Diablo;

/** Immutable generation provenance for the explicit native L1 route. */
public sealed partial class AuthoritativeCathedralNativePopulationPolicy
{
    // Keep the saved generation identity stable; the chest capability is an
    // additive runtime rule over the same immutable native population.
    public const string PolicyId = "native-cathedral-l1-doors-only-v1";
    private const string LevelEntryPolicyId = "native-cathedral-level-entry-v1";
    internal AuthoritativeNativePopulationBindings Bindings { get; }

    public AuthoritativeCathedralNativePopulationPolicy(
        AuthoritativeCathedralFreshLevelRequest request,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeAdapterResult monsters,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        AuthoritativeCathedralItemRuntimeProfile? itemProfile = null,
        bool isDefaultStart = false,
        bool suppressMultiplayerDeathDrops = false)
        : this(request, state, monsters, objects, itemProfile, isDefaultStart,
            suppressMultiplayerDeathDrops, levelEntry: false)
    {
    }

    /** Separate first-entry owner; the legacy constructor and its generation identity stay unchanged. */
    internal static AuthoritativeCathedralNativePopulationPolicy CreateLevelEntry(
        AuthoritativeCathedralFreshLevelRequest request,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeAdapterResult monsters,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        AuthoritativeCathedralItemRuntimeProfile itemProfile,
        bool isDefaultStart = false,
        bool suppressMultiplayerDeathDrops = false)
        => new(request, state, monsters, objects, itemProfile, isDefaultStart,
            suppressMultiplayerDeathDrops, levelEntry: true);

    private AuthoritativeCathedralNativePopulationPolicy(
        AuthoritativeCathedralFreshLevelRequest request,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeAdapterResult monsters,
        IReadOnlyList<AuthoritativeWorldObject> objects,
        AuthoritativeCathedralItemRuntimeProfile? itemProfile,
        bool isDefaultStart,
        bool suppressMultiplayerDeathDrops,
        bool levelEntry)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(objects);
        if (!levelEntry && (request.Depth != 1 || request.Entry != AuthoritativeCathedralEntry.Main))
            throw new NotSupportedException("The populated runtime policy supports only a fresh main-entry Cathedral L1.");
        if (levelEntry && (request.Depth is < 1 or > 4 || request.Entry != AuthoritativeCathedralEntry.Main))
            throw new NotSupportedException("Native population requires a supported main-entry Cathedral floor.");
        if (!levelEntry && monsters.InitialSpecialStandActors.Count != 0)
            throw new NotSupportedException("The legacy L1 owner cannot admit first-entry emergence state.");
        Depth = request.Depth;
        InitialSpecialStandActors = Array.AsReadOnly(monsters.InitialSpecialStandActors.ToArray());
        var levelIds = monsters.Records.Select(row => row.Binding.LevelId).Distinct().ToArray();
        if (levelIds.Length != 1 || levelIds[0] == 0)
            throw new InvalidDataException("The populated Cathedral requires one explicit non-Town level identity.");
        LevelId = levelIds[0];
        MonsterMetadataFingerprint = monsters.MetadataFingerprint;
        InitialState = state;
        var coarse = AuthoritativeCathedralDepthGenerator.Generate(request.Depth, request.DungeonSeed,
            request.Entry, AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs);
        var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(LevelId, coarse,
            request.Metadata.Geometry.Pieces, request.Metadata.Geometry.TileProperties, request.StableLevelSymbol,
            isDefaultStart, suppressMultiplayerDeathDrops).Level;
        if (!state.Transparency.Select(number => checked((ushort)number)).SequenceEqual(materialized.NativeTransparency)
            || !state.PieceProperties.SequenceEqual(request.Metadata.EffectivePieceProperties))
            throw new InvalidDataException("Native population transparency or SOL differs from its generation metadata.");
        var blocked = Enumerable.Range(0, state.Pieces.Count)
            .Where(cell => (state.PieceProperties[state.Pieces[cell]] & (byte)AuthoritativeDunTileFlags.Solid) != 0).ToArray();
        var missile = Enumerable.Range(0, state.Pieces.Count)
            .Where(cell => (state.PieceProperties[state.Pieces[cell]] & (byte)AuthoritativeDunTileFlags.BlockMissile) != 0).ToArray();
        var vision = Enumerable.Range(0, state.Pieces.Count)
            .Where(cell => (state.PieceProperties[state.Pieces[cell]] & (byte)AuthoritativeDunTileFlags.BlockLight) != 0).ToArray();
        var backingLevel = AuthoritativeCathedralDoors.CopyLevel(materialized, state.Pieces,
            state.Special.Select(number => (ushort)number), blocked, missile, blocked, missile, vision);
        Bindings = new AuthoritativeNativePopulationBindings(AuthoritativeNativePopulationFamily.Cathedral,
            request.Depth, state, monsters, objects, backingLevel,
            request.Metadata.Population.Monsters.Select(monster => monster.PopulationDefinition).ToArray(),
            itemProfile, this);
        Reservations = Bindings.Reservations;
        var itemBindingFingerprint = Bindings.ItemBindingFingerprint;
        var liveNativeAis = Bindings.LiveNativeAis;
        var rawIdentity = JsonSerializer.SerializeToNode(InitialState)!.AsObject();
        rawIdentity["UniqueItemFlags"] = JsonSerializer.SerializeToNode(InitialState.UniqueItemFlags.Order().ToArray());
        var identity = new {
            PolicyId = levelEntry ? LevelEntryPolicyId : PolicyId,
            request.Depth,
            request.DungeonSeed,
            request.StorySeed,
            request.Entry,
            request.StableLevelSymbol,
            request.Hero,
            MetadataFingerprint = request.Metadata.Fingerprint,
            request.Metadata.ManifestHash,
            MonsterMetadataFingerprint,
            LevelId,
            backingLevel.IsDefaultStart,
            backingLevel.SuppressMultiplayerDeathDrops,
            InitialState = rawIdentity,
            ItemBindingFingerprint = itemBindingFingerprint,
            // Keep the published generation identity stable. Attack timing is
            // already bound by the complete population MetadataFingerprint.
            Initializations = monsters.Records.Select(row => new {
                row.Initialization.MetadataFingerprint,
                row.Initialization.InitialState,
                row.Initialization.Treasure,
                row.Initialization.SelectionRegion,
                row.Initialization.AttackEffectFrame,
                row.Initialization.SpecialAttackEffectFrame,
                row.Initialization.Binding,
                row.Initialization.InitialAnimationPetrified,
            }).ToArray(),
            LiveActorClassifications = liveNativeAis.OrderBy(row => row.Key).ToArray(),
            Objects = objects.Select(DiabloSimulation.CaptureObject).ToArray(),
        };
        var identityBytes = levelEntry
            ? JsonSerializer.SerializeToUtf8Bytes(new {
                Generation = identity,
                InitialUniqueHistory = request.InitialUniqueItemFlags.Order().ToArray(),
            })
            : JsonSerializer.SerializeToUtf8Bytes(identity);
        Fingerprint = Convert.ToHexString(SHA256.HashData(identityBytes));
        Bindings.BindGenerationFingerprint(Fingerprint);
    }

    public int Depth { get; }
    public IReadOnlyList<AuthoritativeNativeInitialSpecialStand> InitialSpecialStandActors { get; }
    internal IEnumerable<uint> BoundEntityIds
        => Bindings.BoundEntityIds;

    public uint LevelId { get; }
    public string Fingerprint { get; }
    public string MonsterMetadataFingerprint { get; }
    /** Full immutable initial pools, unused tails, raw fields and signed references. */
    public AuthoritativeCathedralThemeRoomState InitialState { get; }
    public IReadOnlyList<AuthoritativeNativeMonsterInitialization> Reservations { get; }
    internal bool ReservesEntityId(uint entityId)
        => Bindings.ReservesEntityId(entityId);

    internal AuthoritativeLevel CreateBackingLevel()
        => Bindings.CreateBackingLevel();

    internal void ValidateBackingLevel(AuthoritativeLevel level, bool allowOmittedSpecial = false)
        => Bindings.ValidateBackingLevel(level, allowOmittedSpecial);

    internal uint? ObjectEntityAt(int x, int y)
        => Bindings.ObjectEntityAt(x, y);

    internal IEnumerable<(uint LevelId, int X, int Y)> SolidObjectPositions()
        => Bindings.SolidObjectPositions();

    internal bool ObjectIsSolid(uint entityId)
        => Bindings.ObjectIsSolid(entityId);

    internal AuthoritativeCathedralDoorState GetInitialDoor(uint entityId)
        => Bindings.GetInitialDoor(entityId);

    internal void ValidateObject(AuthoritativeWorldObject value)
        => Bindings.ValidateObject(value);

    internal void ValidateObjectProjection(DiabloObjectState current)
        => Bindings.ValidateObjectProjection(current);

    internal bool CanOperate(AuthoritativeWorldObject value)
        => Bindings.CanOperate(value);

    internal bool CanOperateNativeChest(AuthoritativeWorldObject value)
        => Bindings.CanOperateNativeChest(value);

    internal bool TryGetNativeChestLootParameters(
        AuthoritativeWorldObject value,
        out int nativeType,
        out uint randomSeed,
        out int itemCount,
        out int itemBranch)
        => Bindings.TryGetNativeChestLootParameters(value, out nativeType, out randomSeed, out itemCount, out itemBranch);

    internal bool TryCommitNativeChestOpen(AuthoritativeWorldObject value)
        => Bindings.TryCommitNativeChestOpen(value);

    internal static bool IsSupportedNativeChest(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState raw)
        => AuthoritativeNativePopulationBindings.IsSupportedNativeChest(state, raw);

    internal void ValidateWorld(DiabloWorldState world)
        => Bindings.ValidateWorld(world);

    internal void ValidateItemProfile(AuthoritativeCathedralItemRuntimeProfile items)
        => Bindings.ValidateItemProfile(items);

    internal int InitialDoorCount => Bindings.InitialDoorCount;
}

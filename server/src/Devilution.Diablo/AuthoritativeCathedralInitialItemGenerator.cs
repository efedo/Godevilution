using System.Collections.ObjectModel;
using System.Text;

namespace Devilution.Diablo;

/** The bounded native item-pool shape used by InitItems and AddInitItems. */
public static class AuthoritativeCathedralInitialItemGenerator
{
    public const int PoolCapacity = 128;
    public const int ActiveCapacity = 127;
    public const int NativeTileWidth = 112;
    public const int NativeTileHeight = 112;
    public const int NativeTileCount = NativeTileWidth * NativeTileHeight;
    public const int NativeHealingPotionId = 24; // IDI_HEAL (quest aliases do not consume ordinals)
    public const int NativeManaPotionId = 25; // IDI_MANA
    private const ushort CreateInfoPregen = 1 << 15; // CF_PREGEN
    private const int SelectionNone = 0;
    private const int SelectionBottom = 1;
    private const int NativeCursorAnimationCount = 229; // ItemCAnimTbl array bound

    public static AuthoritativeCathedralInitialItemResult Generate(
        AuthoritativeCathedralInitialItemParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var items = parameters.Items.Select(CloneItem).ToArray();
        var activeItemIds = parameters.ActiveItemIds.ToArray();
        var tileRefs = parameters.Tiles.Select(tile => tile.ItemRef).ToArray();
        var random = new AuthoritativeLegacyRandom(parameters.RandomState);
        var definitions = ValidateAndIndexDefinitions(parameters.PotionDefinitions);

        // InitItems clears only the fields touched by Item::clear plus its
        // explicit presentation fields. Other scalar fields remain intact.
        var activeItemCount = 0;
        for (var index = 0; index < PoolCapacity; index++) {
            items[index] = items[index] with {
                ItemType = -1,
                PositionX = 0,
                PositionY = 0,
                AnimFlag = false,
                SelectionRegion = SelectionNone,
                Identified = false,
                PostDraw = false,
            };
        }
        for (var index = 0; index < ActiveCapacity; index++)
            activeItemIds[index] = index;

        // Ordinary fresh Cathedral generation is the only supported policy:
        // no set levels or quests, single-player Normal, and legacy item
        // generation enabled. InitItems consumes this discard before drops.
        random.Discard(1);
        var itemCount = GenerateRnd(random, 3, "AddInitItems count") + 3;
        for (var index = 0; index < itemCount; index++) {
            if (activeItemCount >= ActiveCapacity)
                throw new InvalidDataException("The native item pool is full.");

            var slot = activeItemIds[activeItemCount++];
            if (slot < 0 || slot >= ActiveCapacity)
                throw new InvalidDataException("ActiveItems contains the scratch slot.");

            // AllocateItem performs a complete value assignment, unlike
            // Item::clear. This is why a previously inactive slot loses all
            // sentinels before its potion fields are assigned.
            items[slot] = new AuthoritativeCathedralInitialItemState();

            var position = FindAvailablePosition(random, parameters.Tiles, tileRefs,
                parameters.MaximumPlacementAttempts);
            tileRefs[TileIndex(position.X, position.Y)] = checked(slot + 1);
            items[slot] = items[slot] with {
                PositionX = position.X,
                PositionY = position.Y,
            };

            var itemSeed = random.AdvanceRndSeed();
            items[slot] = items[slot] with { Seed = unchecked((uint)itemSeed) };
            // SetRndSeed(item._iSeed) re-seeds the native stream with the
            // absolute signed seed, rather than continuing its prior state.
            random.Restore(unchecked((uint)itemSeed));

            // PickRandomlyAmong clamps the signed GenerateRnd result to zero;
            // preserve that native INT_MIN boundary instead of introducing a
            // retry or changing the random stream.
            var potionRoll = Math.Max(GenerateRnd(random, 2, "PickRandomlyAmong potion"), 0);
            var definition = potionRoll == 0
                ? definitions[NativeManaPotionId]
                : definitions[NativeHealingPotionId];

            // GetItemAttrs always performs the AC draw, including the
            // maxAC-minAC+1 == 1 potion case. Keep this draw even though it
            // cannot change the value.
            var armorClass = checked(definition.MinArmorClass +
                GenerateRnd(random, checked(definition.MaxArmorClass - definition.MinArmorClass + 1),
                    "GetItemAttrs AC"));
            var createInfo = checked((ushort)(parameters.Depth | CreateInfoPregen));
            items[slot] = items[slot] with {
                CreateInfo = createInfo,
                ItemType = definition.ItemType,
                CursorGraphic = definition.CursorGraphic,
                BaseName = CopyNativeName(definition.Name),
                IdentifiedName = CopyNativeName(definition.Name),
                EquipLocation = definition.EquipLocation,
                ItemClass = definition.ItemClass,
                MinDamage = definition.MinDamage,
                MaxDamage = definition.MaxDamage,
                ArmorClass = armorClass,
                Flags = definition.Flags,
                MiscId = definition.MiscId,
                SpellId = definition.SpellId,
                Magical = 0,
                Value = definition.Value,
                IdentifiedValue = definition.Value,
                Durability = definition.Durability,
                MaxDurability = definition.Durability,
                MinimumStrength = unchecked((sbyte)definition.MinimumStrength),
                MinimumMagic = definition.MinimumMagic,
                MinimumDexterity = unchecked((sbyte)definition.MinimumDexterity),
                ItemIndex = definition.NativeItemId,
                Buff = 0,
                PrefixPower = -1,
                SuffixPower = -1,
                Animation = NewGroundAnimation(definition.AnimationFrameCount,
                    definition.AnimationTicksPerFrame),
                AnimFlag = false,
                SelectionRegion = SelectionBottom,
                Identified = false,
                PostDraw = false,
                Request = false,
            };
        }

        var detachedItems = new ReadOnlyCollection<AuthoritativeCathedralInitialItemState>(
            items.Select(CloneItem).ToArray());
        var detachedActiveIds = new ReadOnlyCollection<int>(activeItemIds.ToArray());
        var detachedTileRefs = new ReadOnlyCollection<int>(tileRefs.ToArray());
        var records = new ReadOnlyCollection<AuthoritativeCathedralInitialItemGetRecord>(
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemGetRecord(), ActiveCapacity).ToArray());
        return new(detachedItems, detachedActiveIds, activeItemCount, detachedTileRefs,
            random.State, false, 0, records);
    }

    private static AuthoritativeCathedralInitialItemAnimation NewGroundAnimation(
        int frameCount, int ticksPerFrame)
    {
        // Item::setNewAnimation(false) uses ProcessAnimationPending and then
        // AddInitItems explicitly selects the final frame. The native sprite
        // pointer is intentionally not part of this detached contract.
        return new() {
            TicksPerFrame = ticksPerFrame,
            TickCounterOfCurrentFrame = 0,
            NumberOfFrames = frameCount,
            CurrentFrame = frameCount - 1,
            IsPetrified = false,
        };
    }

    private static (int X, int Y) FindAvailablePosition(AuthoritativeLegacyRandom random,
        IReadOnlyList<AuthoritativeCathedralInitialItemTile> tiles, int[] tileRefs,
        int maximumAttempts)
    {
        for (var attempt = 0; attempt < maximumAttempts; attempt++) {
            var x = GenerateRnd(random, 80, "GetRandomAvailableItemPosition x");
            var y = GenerateRnd(random, 80, "GetRandomAvailableItemPosition y");
            if (x is < 0 or >= 80 || y is < 0 or >= 80)
                throw new InvalidDataException(
                    "Native GenerateRnd returned a signed value that cannot be used as a tile coordinate.");
            x += 16;
            y += 16;
            var tileIndex = TileIndex(x, y);
            var tile = tiles[tileIndex];
            if (tile.PlayerRef != 0 || tile.MonsterRef != 0 || tile.ObjectRef != 0
                || tile.ItemRef != 0 || tileRefs[tileIndex] != 0 || tile.SetPiece || tile.Solid)
                continue;
            return new(x, y);
        }

        throw new InvalidDataException(
            "Native GetRandomAvailableItemPosition exceeded the configured bounded attempt limit.");
    }

    private static int GenerateRnd(AuthoritativeLegacyRandom random, int limit, string operation)
    {
        if (limit <= 0)
            throw new InvalidDataException($"{operation} has an invalid native random bound {limit}.");
        var value = random.GenerateRnd(limit);
        // Negative values are a real native edge for INT_MIN. They are safe
        // for the count arithmetic and for GenerateRnd(1), but not for an
        // array index or a coordinate; those callers reject them explicitly.
        return value;
    }

    private static int TileIndex(int x, int y)
    {
        if ((uint)x >= NativeTileWidth || (uint)y >= NativeTileHeight)
            throw new InvalidDataException("Native item tile coordinate is outside MAXDUNX/MAXDUNY.");
        return checked(y * NativeTileWidth + x);
    }

    private static Dictionary<int, AuthoritativeCathedralInitialItemDefinition>
        ValidateAndIndexDefinitions(IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count != 2)
            throw new InvalidDataException("Ordinary Cathedral InitItems requires exactly two potion definitions.");

        var result = new Dictionary<int, AuthoritativeCathedralInitialItemDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            if (definition.NativeItemId is not (NativeHealingPotionId or NativeManaPotionId))
                throw new InvalidDataException("Only IDI_HEAL and IDI_MANA are supported by Cathedral InitItems.");
            if (!result.TryAdd(definition.NativeItemId, definition))
                throw new InvalidDataException("The Cathedral potion definitions contain a duplicate native ID.");
            if (definition.Name is null || definition.IdentifiedName is null)
                throw new InvalidDataException("Potion names must be supplied externally.");
            if (!string.Equals(definition.Name, definition.IdentifiedName, StringComparison.Ordinal))
                throw new InvalidDataException("Native GetItemAttrs copies iName to both item name fields.");
            if (definition.ItemType != 0)
                throw new InvalidDataException("Cathedral initial items must use the native Misc item type.");
            var expectedMisc = definition.NativeItemId == NativeHealingPotionId ? 3 : 6;
            if (definition.MiscId != expectedMisc)
                throw new InvalidDataException("Potion metadata has an incorrect native misc ID.");
            if (definition.CursorGraphic is < 0 or >= NativeCursorAnimationCount
                || definition.EquipLocation is < sbyte.MinValue or > sbyte.MaxValue
                || definition.ItemClass is < byte.MinValue or > byte.MaxValue
                || definition.MinDamage is < byte.MinValue or > byte.MaxValue
                || definition.MaxDamage is < byte.MinValue or > byte.MaxValue
                || definition.MinArmorClass is < byte.MinValue or > byte.MaxValue
                || definition.MaxArmorClass is < byte.MinValue or > byte.MaxValue
                || definition.Durability is < byte.MinValue or > byte.MaxValue
                || definition.MinimumStrength is < byte.MinValue or > byte.MaxValue
                || definition.MinimumMagic is < byte.MinValue or > byte.MaxValue
                || definition.MinimumDexterity is < byte.MinValue or > byte.MaxValue
                || definition.SpellId is < sbyte.MinValue or > sbyte.MaxValue
                || definition.MiscId is < sbyte.MinValue or > sbyte.MaxValue
                || definition.Value is < ushort.MinValue or > ushort.MaxValue
                || definition.Buff != 0)
                throw new InvalidDataException("Potion metadata contains a scalar outside its native field width.");
            if (definition.AnimationTicksPerFrame != 1)
                throw new InvalidDataException("Native potion SetupItem uses one tick per animation frame.");
            if (definition.MaxArmorClass < definition.MinArmorClass)
                throw new InvalidDataException("Potion armor bounds are inverted.");
            if (definition.AnimationFrameCount is < 1 or > sbyte.MaxValue)
                throw new InvalidDataException("Potion animation frame count is outside native int8 range.");
            if (definition.AnimationTicksPerFrame is < 1 or > sbyte.MaxValue)
                throw new InvalidDataException("Potion animation delay is outside native int8 range.");
        }
        if (!result.ContainsKey(NativeHealingPotionId) || !result.ContainsKey(NativeManaPotionId))
            throw new InvalidDataException("Both IDI_HEAL and IDI_MANA are required.");
        return result;
    }

    private static string CopyNativeName(string value)
    {
        try {
            // The native buffer is exposed as a NUL-terminated C string.
            var terminator = value.IndexOf('\0');
            if (terminator >= 0)
                value = value[..terminator];
            var bytes = new UTF8Encoding(false, true).GetBytes(value);
            if (bytes.Length <= 63)
                return value;
            var length = 63;
            while (length > 0 && (bytes[length] & 0xC0) == 0x80)
                length--;
            return new UTF8Encoding(false, true).GetString(bytes, 0, length);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException("Potion name is not valid UTF-8.", exception);
        }
    }

    private static AuthoritativeCathedralInitialItemState CloneItem(
        AuthoritativeCathedralInitialItemState item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Animation);
        return item with { Animation = item.Animation with { } };
    }

    internal static void ValidateItemProjection(AuthoritativeCathedralInitialItemState item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Animation is null || item.BaseName is null || item.IdentifiedName is null)
            throw new InvalidDataException("A native item projection cannot contain null names or animation.");
        if (new[] { item.ItemType, item.EquipLocation, item.MiscId, item.SpellId,
                item.SpellLevelAdd, item.PrefixPower, item.SuffixPower,
                item.MinimumStrength, item.MinimumDexterity, item.Animation.TicksPerFrame,
                item.Animation.TickCounterOfCurrentFrame, item.Animation.NumberOfFrames,
                item.Animation.CurrentFrame }.Any(value => value is < sbyte.MinValue or > sbyte.MaxValue)
            || new[] { item.SelectionRegion, item.Magical, item.ItemClass, item.CursorGraphic,
                item.MinDamage, item.MaxDamage, item.MinimumMagic,
                item.HellfireDamageArmorFlags }.Any(value => value is < byte.MinValue or > byte.MaxValue)
            || new[] { item.ArmorClass, item.ItemIndex, item.PlusDamage, item.PlusToHit,
                item.PlusArmorClass, item.PlusStrength, item.PlusMagic, item.PlusDexterity,
                item.PlusVitality, item.PlusFireResistance, item.PlusLightningResistance,
                item.PlusMagicResistance, item.PlusMana, item.PlusHitPoints,
                item.PlusDamageModifier, item.PlusGetHit, item.PlusLight, item.FireMinDamage,
                item.FireMaxDamage, item.LightningMinDamage, item.LightningMaxDamage,
                item.PlusEnemyArmorClass }.Any(value => value is < short.MinValue or > short.MaxValue))
            throw new InvalidDataException("A native item projection contains a scalar outside its field width.");
        try {
            var utf8 = new UTF8Encoding(false, true);
            foreach (var name in new[] { item.BaseName, item.IdentifiedName })
                if (name.Contains('\0') || utf8.GetByteCount(name) > 63)
                    throw new InvalidDataException("Native projected item names must fit their 64-byte C-string buffers.");
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException("Native projected item names must be valid UTF-8.", exception);
        }
    }

    internal static void ValidateParameters(AuthoritativeCathedralInitialItemParameters parameters)
    {
        if (parameters.Depth is < 1 or > 4)
            throw new InvalidDataException("Ordinary Cathedral InitItems requires a depth from 1 through 4.");
        if (parameters.MaximumPlacementAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(parameters.MaximumPlacementAttempts));
        if (parameters.Tiles.Count != NativeTileCount)
            throw new InvalidDataException($"Native item tiles require exactly {NativeTileCount} entries.");
        if (parameters.Items.Count != PoolCapacity)
            throw new InvalidDataException($"Native item storage requires exactly {PoolCapacity} entries.");
        if (parameters.ActiveItemIds.Count != ActiveCapacity)
            throw new InvalidDataException($"Native ActiveItems requires exactly {ActiveCapacity} entries.");
        if (parameters.ActiveItemCount is < 0 or > ActiveCapacity)
            throw new InvalidDataException("Native ActiveItemCount is outside the active pool range.");

        foreach (var tile in parameters.Tiles) {
            ArgumentNullException.ThrowIfNull(tile);
            if (tile.PlayerRef is < sbyte.MinValue or > sbyte.MaxValue
                || tile.ObjectRef is < sbyte.MinValue or > sbyte.MaxValue
                || tile.ItemRef is < sbyte.MinValue or > sbyte.MaxValue
                || tile.MonsterRef is < short.MinValue or > short.MaxValue)
                throw new InvalidDataException("A native signed tile reference is outside its Tile field width.");
        }
        foreach (var item in parameters.Items)
            ValidateItemProjection(item);
        foreach (var activeItemId in parameters.ActiveItemIds)
            if (activeItemId is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException("A native ActiveItems identity is outside uint8 range.");
    }

}

public sealed class AuthoritativeCathedralInitialItemParameters
{
    public AuthoritativeCathedralInitialItemParameters(
        int depth,
        uint randomState,
        IReadOnlyList<AuthoritativeCathedralInitialItemTile> tiles,
        IReadOnlyList<AuthoritativeCathedralInitialItemState> items,
        IReadOnlyList<int> activeItemIds,
        int activeItemCount,
        IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> potionDefinitions,
        int maximumPlacementAttempts = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(activeItemIds);
        ArgumentNullException.ThrowIfNull(potionDefinitions);
        Depth = depth;
        RandomState = randomState;
        Tiles = new ReadOnlyCollection<AuthoritativeCathedralInitialItemTile>(tiles.ToArray());
        Items = new ReadOnlyCollection<AuthoritativeCathedralInitialItemState>(items.ToArray());
        ActiveItemIds = new ReadOnlyCollection<int>(activeItemIds.ToArray());
        ActiveItemCount = activeItemCount;
        PotionDefinitions = new ReadOnlyCollection<AuthoritativeCathedralInitialItemDefinition>(potionDefinitions.ToArray());
        MaximumPlacementAttempts = maximumPlacementAttempts;
        AuthoritativeCathedralInitialItemGenerator.ValidateParameters(this);
    }

    public int Depth { get; }
    public uint RandomState { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemTile> Tiles { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemState> Items { get; }
    public IReadOnlyList<int> ActiveItemIds { get; }
    public int ActiveItemCount { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> PotionDefinitions { get; }
    public int MaximumPlacementAttempts { get; }
}

/** Detached copy of one native Tile's occupancy and placement inputs. */
public sealed record AuthoritativeCathedralInitialItemTile(
    int PlayerRef = 0,
    int MonsterRef = 0,
    int ObjectRef = 0,
    int ItemRef = 0,
    bool SetPiece = false,
    bool Solid = false);

/** Raw itemdat plus animation metadata supplied by the caller. */
public sealed record AuthoritativeCathedralInitialItemDefinition(
    int NativeItemId,
    int ItemType,
    int CursorGraphic,
    string Name,
    string IdentifiedName,
    int EquipLocation,
    int ItemClass,
    int MinDamage,
    int MaxDamage,
    int MinArmorClass,
    int MaxArmorClass,
    uint Flags,
    int MiscId,
    int SpellId,
    int Value,
    int Durability,
    int MinimumStrength,
    int MinimumMagic,
    int MinimumDexterity,
    int AnimationFrameCount,
    int AnimationTicksPerFrame = 1,
    uint Buff = 0);

/** Five public AnimationInfo scalars; sprites/private visual state are excluded. */
public sealed record AuthoritativeCathedralInitialItemAnimation
{
    public int TicksPerFrame { get; init; }
    public int TickCounterOfCurrentFrame { get; init; }
    public int NumberOfFrames { get; init; }
    public int CurrentFrame { get; init; }
    public bool IsPetrified { get; init; }
}

/** Complete scalar Item projection used by InitItems/AddInitItems. */
public sealed record AuthoritativeCathedralInitialItemState
{
    public uint Seed { get; init; }
    public ushort CreateInfo { get; init; }
    public int ItemType { get; init; } = -1;
    public bool AnimFlag { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public AuthoritativeCathedralInitialItemAnimation Animation { get; init; } = new();
    public bool Deleted { get; init; }
    public int SelectionRegion { get; init; }
    public bool PostDraw { get; init; }
    public bool Identified { get; init; }
    public int Magical { get; init; }
    public string BaseName { get; init; } = string.Empty;
    public string IdentifiedName { get; init; } = string.Empty;
    public int EquipLocation { get; init; }
    public int ItemClass { get; init; }
    public int CursorGraphic { get; init; }
    public int Value { get; init; }
    public int IdentifiedValue { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int ArmorClass { get; init; }
    public uint Flags { get; init; }
    public int MiscId { get; init; }
    public int SpellId { get; init; }
    public int ItemIndex { get; init; } = -1;
    public int Charges { get; init; }
    public int MaxCharges { get; init; }
    public int Durability { get; init; }
    public int MaxDurability { get; init; }
    public int PlusDamage { get; init; }
    public int PlusToHit { get; init; }
    public int PlusArmorClass { get; init; }
    public int PlusStrength { get; init; }
    public int PlusMagic { get; init; }
    public int PlusDexterity { get; init; }
    public int PlusVitality { get; init; }
    public int PlusFireResistance { get; init; }
    public int PlusLightningResistance { get; init; }
    public int PlusMagicResistance { get; init; }
    public int PlusMana { get; init; }
    public int PlusHitPoints { get; init; }
    public int PlusDamageModifier { get; init; }
    public int PlusGetHit { get; init; }
    public int PlusLight { get; init; }
    public int SpellLevelAdd { get; init; }
    public int UniqueId { get; init; }
    public int FireMinDamage { get; init; }
    public int FireMaxDamage { get; init; }
    public int LightningMinDamage { get; init; }
    public int LightningMaxDamage { get; init; }
    public int PlusEnemyArmorClass { get; init; }
    public int PrefixPower { get; init; } = -1;
    public int SuffixPower { get; init; } = -1;
    public int ValueAdd1 { get; init; }
    public int ValueMultiply1 { get; init; }
    public int ValueAdd2 { get; init; }
    public int ValueMultiply2 { get; init; }
    public int MinimumStrength { get; init; }
    public int MinimumMagic { get; init; }
    public int MinimumDexterity { get; init; }
    public bool StatFlag { get; init; }
    public bool Request { get; init; }
    public int HellfireDamageArmorFlags { get; init; }
    public uint Buff { get; init; }
}

/** Zeroed native item-get record projection after initItemGetRecords. */
public sealed record AuthoritativeCathedralInitialItemGetRecord
{
    public uint Seed { get; init; }
    public ushort CreateInfo { get; init; }
    public int ItemIndex { get; init; }
    public uint Timestamp { get; init; }
}

public sealed class AuthoritativeCathedralInitialItemResult
{
    internal AuthoritativeCathedralInitialItemResult(
        IReadOnlyList<AuthoritativeCathedralInitialItemState> items,
        IReadOnlyList<int> activeItemIds,
        int activeItemCount,
        IReadOnlyList<int> itemTileRefs,
        uint finalRandomState,
        bool showUniqueItemInfoBox,
        int itemGetRecordCount,
        IReadOnlyList<AuthoritativeCathedralInitialItemGetRecord> itemGetRecords)
    {
        Items = items;
        ActiveItemIds = activeItemIds;
        ActiveItemCount = activeItemCount;
        ItemTileRefs = itemTileRefs;
        FinalRandomState = finalRandomState;
        ShowUniqueItemInfoBox = showUniqueItemInfoBox;
        ItemGetRecordCount = itemGetRecordCount;
        ItemGetRecords = itemGetRecords;
    }

    public IReadOnlyList<AuthoritativeCathedralInitialItemState> Items { get; }
    public IReadOnlyList<int> ActiveItemIds { get; }
    public int ActiveItemCount { get; }
    public IReadOnlyList<int> ItemTileRefs { get; }
    public IReadOnlyList<int> SignedItemGrid => ItemTileRefs;
    public uint FinalRandomState { get; }
    public uint RandomState => FinalRandomState;
    public bool ShowUniqueItemInfoBox { get; }
    public int ItemGetRecordCount { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemGetRecord> ItemGetRecords { get; }
}

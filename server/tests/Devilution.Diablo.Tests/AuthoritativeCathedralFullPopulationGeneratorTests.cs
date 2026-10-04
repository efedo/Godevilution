using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Bounded composition tests for the controlled ordinary Cathedral phase seam. */
public sealed class AuthoritativeCathedralFullPopulationGeneratorTests
{
    private const int CellCount = 112 * 112;

    [Fact]
    public void RejectsUnsupportedPolicyBeforeGenerationAndLeavesCallerArraysUntouched()
    {
        var tileFlags = new byte[CellCount];
        var original = tileFlags.ToArray();

        Assert.Throws<InvalidDataException>(() =>
            CreateRequest(new AuthoritativeCathedralThemeItemPolicy(
                1, Hellfire: true), tileFlags: tileFlags));
        Assert.Equal(original, tileFlags);
    }

    [Fact]
    public void RejectsPrePhaseOccupancyInsteadOfDroppingItAfterInitialObjects()
    {
        var occupied = new bool[CellCount];
        occupied[112 + 112] = true;
        var pieces = Enumerable.Repeat((ushort)1, CellCount).ToArray();
        var geometry = new AuthoritativeCathedralPopulationGeometry(
            pieces, new byte[CellCount], new byte[CellCount], new byte[1379],
            new bool[CellCount], new bool[CellCount], occupied);
        var initial = new AuthoritativeCathedralInitialPopulationParameters(
            1, 1, 0x13579BDF, geometry, MonsterCatalog(), ObjectCatalog(),
            Player(), 0, [100, 101, 102], 0, []);
        Assert.Throws<InvalidDataException>(() =>
            CreateRequest(initialPopulation: initial));
    }

    [Fact]
    public void ComposesIndependentClearInitialItemsAndEmptyThemePassDetached()
    {
        var request = CreateRequest();
        var first = AuthoritativeCathedralFullPopulationGenerator.Generate(request);
        var second = AuthoritativeCathedralFullPopulationGenerator.Generate(request);

        Assert.Equal(first.FinalRandomState, second.FinalRandomState);
        Assert.Equal(first.ClearedMonsterPool.FinalRandomState,
            second.ClearedMonsterPool.FinalRandomState);
        Assert.Equal(AuthoritativeCathedralPopulationParameters.MonsterCapacity,
            first.ClearedMonsterPool.Monsters.Count);
        Assert.Equal(AuthoritativeCathedralInitialItemGenerator.PoolCapacity,
            first.InitialItems.Items.Count);
        Assert.InRange(first.InitialItems.ActiveItemCount, 3, 5);
        Assert.Equal(7, Assert.Single(first.State.UniqueItemFlags));
        Assert.Equal(first.InitialItems.FinalRandomState, first.ThemeRooms.FinalRandomState);
        Assert.NotSame(first.State.MonsterSlots, second.State.MonsterSlots);
        Assert.NotSame(first.InitialItems.Items, second.InitialItems.Items);
        Assert.Equal(Enumerable.Repeat((byte)15, CellCount), first.State.Light);
        Assert.Equal(Enumerable.Repeat((byte)0, CellCount), first.State.PreLight);
    }

    [Fact]
    public void CurrentEncounterChannelsOverrideStaleConvenienceItemTileFlags()
    {
        var request = CreateRequest(initialItemTiles: Enumerable.Repeat(
            new AuthoritativeCathedralInitialItemTile(SetPiece: true, Solid: true),
            CellCount).ToArray());

        var result = AuthoritativeCathedralFullPopulationGenerator.Generate(request);

        // The all-flat pre-theme SOL and empty TileFlags are the actual channels
        // seen by InitItems. If the stale booleans were honored, no bounded item
        // position would be available.
        Assert.InRange(result.InitialItems.ActiveItemCount, 3, 5);
        Assert.Contains(result.InitialItems.ItemTileRefs, value => value > 0);
    }

    [Fact]
    public void ConstructorBoundsCloneHistoryAndRetainInactiveLightRecords()
    {
        var records = Enumerable.Range(0, 32).Select(index =>
            new AuthoritativeCathedralEncounterLightState(
                index, new(index, index + 1), new(index + 1, index), -128 + index,
                127 - index, index, 255 - index, index % 2 == 0, index % 3 == 0)).ToArray();
        var allocator = new AuthoritativeCathedralEncounterLightAllocator(
            Enumerable.Range(0, 32).ToArray(), 0, records, updateLighting: true, disableLighting: true);
        var history = new HashSet<int> { 7 };
        var request = CreateRequest(
            lightAllocator: allocator,
            initialUniqueItemFlags: history);

        history.Add(8);
        Assert.Equal([7], request.InitialUniqueItemFlags);
        Assert.NotSame(records, request.LightAllocator.LightRecords);
        Assert.Equal(records, request.LightAllocator.LightRecords);

        var result = AuthoritativeCathedralFullPopulationGenerator.Generate(request);
        Assert.Equal(records, result.BeforeThemeRooms.LightRecords);
        Assert.Equal(result.InitialPopulation.Themes.PlacementOrigin,
            result.ThemeRooms.FinalThemeOrigin);
        Assert.Equal(result.InitialPopulation.Themes.PlacementVariant,
            result.ThemeRooms.FinalThemeVar1);
    }

    [Fact]
    public void ConstructorRejectsUnsafeBoundsHistoryShrineAndSharedPotionMetadata()
    {
        Assert.Throws<InvalidDataException>(() => CreateRequest(monsterDensityDivisor: 0));
        Assert.Throws<InvalidDataException>(() => CreateRequest(maximumPlacementAttempts: 0));
        Assert.Throws<InvalidDataException>(() => CreateRequest(maximumItemPlacementAttempts: 0));
        Assert.Throws<InvalidDataException>(() => CreateRequest(initialActiveItemCount: 1));
        Assert.Throws<InvalidDataException>(() => CreateRequest(
            initialUniqueItemFlags: new HashSet<int> { 128 }));
        Assert.Throws<InvalidDataException>(() => CreateRequest(shrineAvailable: [true]));

        var catalog = ItemCatalog();
        var mismatched = new AuthoritativeCathedralThemeItemCatalog(
            catalog.Items.Select(item => item.NativeItemIndex == 24
                ? item with { Value = item.Value + 1 }
                : item),
            catalog.Prefixes, catalog.Suffixes, catalog.Uniques, catalog.Spells,
            catalog.CursorAnimations, catalog.GlyphWidths, catalog.Cursors,
            catalog.AlwaysMagicMiscIds, catalog.Rules, catalog.ReplacementGlyphWidth);
        Assert.Throws<InvalidDataException>(() => CreateRequest(itemCatalog: mismatched));
        Assert.Throws<InvalidDataException>(() => CreateRequest(
            potionDefinitions: PotionDefinitions().Select(value => value with { Buff = 1 }).ToArray()));
    }

    [Fact]
    public void SharedPotionRequirementsRemainRawBytesBeforeGeneratedSignedAssignment()
    {
        var catalog = ItemCatalog();
        var rawRows = catalog.Items.Select(item => item with {
            MinimumStrength = 128,
            MinimumDexterity = 255,
        }).ToArray();
        var rawCatalog = WithItems(catalog, rawRows);
        var rawDefinitions = PotionDefinitions().Select(value => value with {
            MinimumStrength = 128,
            MinimumDexterity = 255,
        }).ToArray();

        var result = AuthoritativeCathedralFullPopulationGenerator.Generate(CreateRequest(
            itemCatalog: rawCatalog,
            potionDefinitions: rawDefinitions));

        Assert.All(result.InitialItems.Items.Take(result.InitialItems.ActiveItemCount), item => {
            Assert.Equal(unchecked((sbyte)128), item.MinimumStrength);
            Assert.Equal(unchecked((sbyte)255), item.MinimumDexterity);
        });
    }

    [Fact]
    public void PublicControlledConstructorStillRejectsActivePlayerLights()
    {
        var records = Enumerable.Range(0, 32).Select(index =>
            new AuthoritativeCathedralEncounterLightState(index, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        records[0] = new(0, new(40, 40), new(40, 40), 0, 0, 8, 8, false, true);
        Assert.Throws<InvalidDataException>(() => CreateRequest(lightAllocator:
            new(Enumerable.Range(0, 32).ToArray(), 1, records, updateLighting: true)));
    }

    [Fact]
    public void FreshInternalLightAdmissionRejectsMissingChangedOrBorrowedRecords()
    {
        var source = CreateRequest();
        var records = Enumerable.Range(0, 32).Select(index =>
            new AuthoritativeCathedralEncounterLightState(index, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        records[0] = new(0, new(40, 40), new(40, 40), 0, 0, 8, 8, false, true);
        AuthoritativeCathedralFullPopulationRequest Admit(AuthoritativeCathedralEncounterLightAllocator allocator)
            => new(source.InitialPopulation, source.MonsterClearRandomState, source.InitialMonsterTileRefs,
                source.InitialPlayerRefs, source.InitialTileFlags, source.InitialCorpses, source.InitialLight,
                source.InitialPreLight, allocator, source.TriggerPositions, source.OrderedUniqueDefinitions,
                source.MonsterDensityDivisor, source.ShrineAvailable, source.ItemCatalog, source.ItemPolicy,
                new(MaximumLifeBase: 6400, MaximumManaBase: 3200), source.InitialItemSlots,
                source.InitialActiveItemIds, source.InitialActiveItemCount, source.InitialUniqueItemFlags,
                source.InitialItemTiles, source.PotionDefinitions, source.MaximumPlacementAttempts,
                source.MaximumItemPlacementAttempts, source.InitialVisibleTransparencyRegions, playerInitialized: true);
        var valid = new AuthoritativeCathedralEncounterLightAllocator(Enumerable.Range(0, 32).ToArray(), 1, records, updateLighting: true);
        Assert.Equal(1, Admit(valid).LightAllocator.ActiveLightCount);
        Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 0, records, updateLighting: true)));
        Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 2, records, updateLighting: true)));
        Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 1, records)));
        Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 1, records, true, true)));
        foreach (var changed in new[] {
            records[0] with { Tile = new(39, 40) }, records[0] with { OldRadius = 7 },
            records[0] with { HasChanged = false }, records[0] with { IsInvalid = true },
            records[0] with { OffsetX = 1 }, records[0] with { Radius = 16, OldRadius = 16 },
        }) {
            var corrupt = records.ToArray(); corrupt[0] = changed;
            Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 1, corrupt, updateLighting: true)));
        }
        records[31] = records[31] with { Radius = 1 };
        Assert.Throws<InvalidDataException>(() => Admit(new(Enumerable.Range(0, 32).ToArray(), 1, records, updateLighting: true)));
    }

    private static AuthoritativeCathedralFullPopulationRequest CreateRequest(
        AuthoritativeCathedralThemeItemPolicy? policy = null,
        AuthoritativeCathedralInitialPopulationParameters? initialPopulation = null,
        IReadOnlyList<byte>? tileFlags = null,
        IReadOnlyList<AuthoritativeCathedralInitialItemTile>? initialItemTiles = null,
        AuthoritativeCathedralThemeItemCatalog? itemCatalog = null,
        AuthoritativeCathedralEncounterLightAllocator? lightAllocator = null,
        IReadOnlySet<int>? initialUniqueItemFlags = null,
        IReadOnlyList<bool>? shrineAvailable = null,
        int monsterDensityDivisor = 1000,
        int maximumPlacementAttempts = 1_000_000,
        int maximumItemPlacementAttempts = 1_000_000,
        int initialActiveItemCount = 0,
        IReadOnlyList<AuthoritativeCathedralInitialItemDefinition>? potionDefinitions = null)
    {
        initialPopulation ??= CreateInitialPopulation();
        var tiles = initialItemTiles ?? Enumerable.Repeat(
            new AuthoritativeCathedralInitialItemTile(), CellCount).ToArray();
        return new(
            initialPopulation,
            monsterClearRandomState: 0x12345678,
            initialMonsterTileRefs: new int[CellCount],
            initialPlayerRefs: new int[CellCount],
            initialTileFlags: tileFlags ?? new byte[CellCount],
            initialCorpses: new byte[CellCount],
            initialLight: Enumerable.Repeat((byte)15, CellCount).ToArray(),
            initialPreLight: new byte[CellCount],
            lightAllocator: lightAllocator ?? new AuthoritativeCathedralEncounterLightAllocator(
                Enumerable.Range(0, 32).ToArray(), 0),
            triggerPositions: [],
            orderedUniqueDefinitions: [],
            monsterDensityDivisor: monsterDensityDivisor,
            shrineAvailable: shrineAvailable ?? Enumerable.Repeat(true, 26).ToArray(),
            itemCatalog: itemCatalog ?? ItemCatalog(),
            itemPolicy: policy ?? new AuthoritativeCathedralThemeItemPolicy(1),
            itemPlayer: new AuthoritativeCathedralThemeItemPlayerContext(),
            initialItemSlots: Enumerable.Range(0, 128)
                .Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray(),
            initialActiveItemIds: Enumerable.Range(0, 127).ToArray(),
            initialActiveItemCount: initialActiveItemCount,
            initialUniqueItemFlags: initialUniqueItemFlags ?? new HashSet<int> { 7 },
            initialItemTiles: tiles,
            potionDefinitions: potionDefinitions ?? PotionDefinitions(),
            maximumPlacementAttempts: maximumPlacementAttempts,
            maximumItemPlacementAttempts: maximumItemPlacementAttempts);
    }

    private static AuthoritativeCathedralInitialPopulationParameters CreateInitialPopulation()
    {
        var geometry = new AuthoritativeCathedralPopulationGeometry(
            Enumerable.Repeat((ushort)1, CellCount).ToArray(),
            new byte[CellCount], new byte[CellCount], new byte[1379],
            new bool[CellCount], new bool[CellCount], new bool[CellCount]);
        return new(1, 1, 0x13579BDF, geometry, MonsterCatalog(), ObjectCatalog(),
            Player(), 0, [100, 101, 102], 0, []);
    }

    private static AuthoritativeCathedralPopulationPlayer Player()
        => new(0, 1, true, false, new(40, 40), new(40, 40));

    private static AuthoritativeCathedralPopulationObjectDefinition[] ObjectCatalog()
        => Enumerable.Range(0, 109)
            .Select(id => new AuthoritativeCathedralPopulationObjectDefinition(id, 0, 1, 1, 0, 0))
            .ToArray();

    private static AuthoritativeCathedralRawMonsterDefinition[] MonsterCatalog()
        => Enumerable.Range(0, 138).Select(id => new AuthoritativeCathedralRawMonsterDefinition(
            nativeMonsterId: id,
            availability: AuthoritativeCathedralMonsterAvailability.Never,
            image: 0,
            minDungeonLevel: 0,
            maxDungeonLevel: 0,
            isSkeleton: id is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27,
            minimumHitPoints: 2,
            maximumHitPoints: 4,
            ai: 0,
            intelligence: 0,
            abilityFlags: 0,
            minDamage: 1,
            maxDamage: 2,
            minDamageSpecial: 0,
            maxDamageSpecial: 0,
            armorClass: 0,
            resistance: 0,
            reducePlayerStrength: 0,
            reducePlayerMagic: 0,
            reducePlayerDexterity: 0,
            reducePlayerVitality: 0,
            reducePlayerMaxHP: 0,
            reducePlayerMaxMana: 0,
            hasSpecial: false,
            width: 64,
            frames: [1, 0, 0, 0, 0, 0],
            rates: [1, 1, 1, 1, 1, 1])).ToArray();

    private static AuthoritativeCathedralInitialItemDefinition[] PotionDefinitions()
        => [
            new(24, 0, 32, "Potion of Healing", "Potion of Healing", 7, 3,
                0, 0, 0, 0, 0, 3, 0, 50, 0, 0, 0, 0, 1),
            new(25, 0, 39, "Potion of Mana", "Potion of Mana", 7, 3,
                0, 0, 0, 0, 0, 6, 0, 50, 0, 0, 0, 0, 1),
        ];

    private static AuthoritativeCathedralThemeItemCatalog ItemCatalog()
    {
        var items = Enumerable.Range(0, 26).Select(ItemRow).ToArray();
        var spells = new[] {
            new AuthoritativeCathedralNativeSpellDefinition(0, "None", -1, -1, 0, 0, 0, 0, 0, 0),
            new AuthoritativeCathedralNativeSpellDefinition(1, "Firebolt", 0, 0, 0, 0, 0, 0, 0, 0),
        };
        return new AuthoritativeCathedralThemeItemCatalog(
            items, [], [], [], spells, new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition>(),
            Enumerable.Range(0, 256).ToDictionary(value => value, _ => 8),
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            replacementGlyphWidth: 8);
    }

    private static AuthoritativeCathedralThemeItemCatalog WithItems(
        AuthoritativeCathedralThemeItemCatalog source,
        IEnumerable<AuthoritativeCathedralNativeItemDefinition> items)
        => new(
            items, source.Prefixes, source.Suffixes, source.Uniques, source.Spells,
            source.CursorAnimations, source.GlyphWidths, source.Cursors,
            source.AlwaysMagicMiscIds, source.Rules, source.ReplacementGlyphWidth);

    private static AuthoritativeCathedralNativeItemDefinition ItemRow(int index)
    {
        var healing = index == 24;
        var mana = index == 25;
        var name = healing ? "Potion of Healing" : mana ? "Potion of Mana" : $"Item {index}";
        var cursor = healing ? 32 : mana ? 39 : 4;
        var misc = healing ? 3 : mana ? 6 : 0;
        return new(
            NativeItemIndex: index,
            NativeItemId: index,
            DropRate: 1,
            ItemClass: 3,
            EquipLocation: 7,
            CursorGraphic: cursor,
            ItemType: 0,
            Name: name,
            ShortName: name,
            MinimumMonsterLevel: 0,
            Durability: 0,
            MinimumDamage: 0,
            MaximumDamage: 0,
            MinimumArmorClass: 0,
            MaximumArmorClass: 0,
            MinimumStrength: 0,
            MinimumMagic: 0,
            MinimumDexterity: 0,
            Flags: 0,
            MiscId: misc,
            SpellId: 0,
            Usable: true,
            Value: healing || mana ? 50 : 1,
            AnimationFrameCount: 1);
    }
}

using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Native item publication must be scoped to its owned levels and bound content. */
public sealed class DiabloWorldStateCathedralItemsIntegrationTests
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void InitialNativePoolIsPublishedWithoutReplacingTownItems()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var profile = Profile(checkpoint);
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var townItem = TownItem();
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112, worldItems: [townItem]);

        world.ConfigureCathedralItems(profile, null, Fingerprint, options.Items, options.Spells!);

        Assert.Equal(3, world.WorldItems.Count);
        Assert.Equal(townItem, world.WorldItems[townItem.EntityId]);
        Assert.Equal(new uint[] { 101, 102 }, world.WorldItems.Values
            .Where(item => item.LevelId == 1).Select(item => item.EntityId).Order().ToArray());
        Assert.Equal(2, world.CaptureCathedralItems()!.Levels.Single().ActiveItemCount);
    }

    [Fact]
    public void InitialGlobalEntityCollisionRefusesPoolBeforePublishingAnyItems()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var profile = Profile(checkpoint);
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var townItem = new AuthoritativeWorldItem(102, 0, 62, 63, 0, 100, TownItem().State);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112, worldItems: [townItem]);
        Assert.DoesNotContain(world.WorldItems.Values, item => item.LevelId == 1);
        Assert.Equal(new uint[] { 101, 102 }, ProjectedPool(profile, checkpoint)
            .Select(item => item.EntityId).Order().ToArray());

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, options.Items, options.Spells!));

        Assert.False(world.HasNativeCathedralItems);
        Assert.Single(world.WorldItems);
        Assert.Same(townItem, world.WorldItems[102]);
        Assert.DoesNotContain(101U, world.WorldItems.Keys);
        Assert.DoesNotContain(world.WorldItems.Values, item => item.LevelId == 1);
    }

    [Fact]
    public void RestoreAcceptsCompleteOwnedPoolAlongsideUnrelatedTownItems()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var profile = Profile(checkpoint);
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var townItem = TownItem();
        var worldItems = new[] { townItem }.Concat(ProjectedPool(profile, checkpoint)).ToArray();
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112, worldItems: worldItems);

        world.ConfigureCathedralItems(profile, checkpoint, Fingerprint, options.Items, options.Spells!);

        Assert.Equal(3, world.WorldItems.Count);
        Assert.Equal(townItem, world.WorldItems[townItem.EntityId]);
        Assert.Equal(worldItems.OrderBy(item => item.EntityId), world.WorldItems.Values.OrderBy(item => item.EntityId));
        Assert.Equal(2, world.CaptureCathedralItems()!.Levels.Single().ActiveItemCount);
    }

    [Fact]
    public void PartialOwnedProjectionIsRejectedWithoutPublishingMissingItems()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var profile = Profile(checkpoint);
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var townItem = TownItem();
        var partialItem = ProjectedPool(profile, checkpoint).First();
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112,
            worldItems: [townItem, partialItem]);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, options.Items, options.Spells!));

        Assert.False(world.HasNativeCathedralItems);
        Assert.Equal(2, world.WorldItems.Count);
        Assert.Equal(townItem, world.WorldItems[townItem.EntityId]);
        Assert.Equal(partialItem, world.WorldItems[partialItem.EntityId]);
        Assert.DoesNotContain(102U, world.WorldItems.Keys);
    }

    [Fact]
    public void StableSymbolMustResolveToTheBoundContentItem()
    {
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[1] = bindings[1] with { StableSymbol = "not_the_sword" };
        profile = profile with { ItemBindings = bindings };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var catalog = WithDefinition(options.Items!, 1002, definition => definition with {
            StableSymbol = "short_sword",
        });
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, catalog, options.Spells!));
        Assert.Empty(world.WorldItems);
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void NativeGoldCursorTracksBoundWorldValueAcrossRestore()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var profile = Profile(checkpoint);
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var baseItems = options.Items!;
        var baseGold = baseItems.GetRequired(1001);
        var nativeGold = profile.ItemCatalog.Items.Single(item => item.NativeItemIndex == 0);
        var goldDefinition = baseGold with {
            ItemType = nativeGold.ItemType,
            Value = nativeGold.Value,
            IdentifiedValue = nativeGold.Value,
            EquipLocation = nativeGold.EquipLocation,
            ItemClass = nativeGold.ItemClass,
            Flags = nativeGold.Flags,
            MiscId = nativeGold.MiscId,
            SpellId = nativeGold.SpellId,
            NativeItemIndex = nativeGold.NativeItemIndex,
            Name = nativeGold.Name,
            IdentifiedName = nativeGold.Name,
            GenerationTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Gold" },
        };
        var goldMetadata = new AuthoritativeNativeGoldStackMetadata(
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
            goldDefinition.InventoryWidth,
            goldDefinition.InventoryHeight,
            profile.ItemCatalog.Cursors.GoldSmall,
            profile.ItemCatalog.Cursors.GoldMedium,
            profile.ItemCatalog.Cursors.GoldLarge);
        var items = baseItems.WithNativePopulationOverlay(
            new Dictionary<uint, AuthoritativeItemDefinition> { [goldDefinition.ItemId] = goldDefinition },
            goldMetadata);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);
        world.ConfigureCathedralItems(profile, null, Fingerprint, items, options.Spells!);

        var initialGold = world.WorldItems[101];
        world.SetWorldItem(new AuthoritativeWorldItem(initialGold.EntityId, initialGold.LevelId,
            initialGold.PositionX, initialGold.PositionY, initialGold.ItemSeed, 1000, initialGold.State with {
                Value = 1000, IdentifiedValue = 1000, NativeCursorGraphic = goldMetadata.GoldSmallCursor,
            }));
        var gold = world.WorldItems[101];
        Assert.Equal(1000, gold.State.Value);
        Assert.Equal((int?)goldMetadata.GoldSmallCursor, gold.State.NativeCursorGraphic);
        var largeGoldState = gold.State with {
            Value = 2500,
            IdentifiedValue = 2500,
            NativeCursorGraphic = goldMetadata.GoldLargeCursor,
        };
        world.SetWorldItem(new AuthoritativeWorldItem(gold.EntityId, gold.LevelId,
            gold.PositionX, gold.PositionY, gold.ItemSeed, 2500, largeGoldState));

        var updatedGold = world.WorldItems[101];
        Assert.Equal(2500U, updatedGold.Price);
        Assert.Equal(2500, updatedGold.State.Value);
        Assert.Equal(2500, updatedGold.State.IdentifiedValue);
        Assert.Equal((int?)goldMetadata.GoldLargeCursor, updatedGold.State.NativeCursorGraphic);
        var updatedCheckpoint = Assert.IsType<DiabloCathedralItemCheckpoint>(world.CaptureCathedralItems());
        Assert.Equal(goldMetadata.GoldLargeCursor,
            updatedCheckpoint.Levels.Single().Items[0].CursorGraphic);

        var restored = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112,
            worldItems: world.WorldItems.Values.ToArray());
        restored.ConfigureCathedralItems(profile, updatedCheckpoint, Fingerprint, items, options.Spells!);
        var restoredGold = restored.WorldItems[101];
        Assert.Equal((updatedGold.EntityId, updatedGold.LevelId, updatedGold.PositionX,
            updatedGold.PositionY, updatedGold.ItemSeed, updatedGold.Price, updatedGold.State),
            (restoredGold.EntityId, restoredGold.LevelId, restoredGold.PositionX,
                restoredGold.PositionY, restoredGold.ItemSeed, restoredGold.Price, restoredGold.State));
        Assert.Equal(goldMetadata.GoldLargeCursor,
            restored.CaptureCathedralItems()!.Levels.Single().Items[0].CursorGraphic);
    }

    [Theory]
    [InlineData(23, 40, false, DiabloItemSpellSourceKind.ChargedItem)]
    [InlineData(21, 0, true, DiabloItemSpellSourceKind.ConsumableItem)]
    [InlineData(22, 0, true, DiabloItemSpellSourceKind.ConsumableItem)]
    public void LegacyStaffAndScrollMappingsMatchNativeSpellSourceRules(
        int miscId, int charges, bool usable, DiabloItemSpellSourceKind sourceKind)
    {
        var symbol = miscId == 23 ? "test_staff" : "test_scroll";
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [0] = new(0, 1001, symbol, SpellSourceKind: sourceKind),
            },
        };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var catalog = WithDefinition(options.Items!, 1001, definition => definition with {
            StableSymbol = symbol,
            MiscId = miscId,
            SpellId = 101,
            Charges = charges,
            MaxCharges = charges,
            Usable = usable,
        });
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        world.ConfigureCathedralItems(profile, null, Fingerprint, catalog, options.Spells!);

        Assert.True(world.HasNativeCathedralItems);
    }

    [Fact]
    public void LoadedNullSpellStaffTemplateAllowsItsNativeChargedBinding()
    {
        var contentItems = LoadBaseLegacyItem("SHORTSTAFF", "SHORT_STAFF", "Staff", "STAFF");
        var itemId = contentItems.ResolveLegacySymbol("SHORTSTAFF");
        var definition = contentItems.GetRequired(itemId);
        Assert.Equal(0, definition.SpellId);
        Assert.Equal(40, definition.MaxCharges);
        Assert.Contains("Staff", definition.GenerationTags);
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [definition.NativeItemIndex] = new(definition.NativeItemIndex, definition.ItemId, "SHORTSTAFF",
                    SpellSourceKind: DiabloItemSpellSourceKind.ChargedItem),
            },
        };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        world.ConfigureCathedralItems(profile, null, Fingerprint, contentItems, options.Spells!);

        Assert.True(world.HasNativeCathedralItems);
    }

    [Fact]
    public void LoadedNullSpellBookTemplateAllowsItsNativeReadBookBinding()
    {
        var contentItems = LoadBaseLegacyItem("NONE", "BOOK_BLUE", "Misc", "BOOK");
        var itemId = contentItems.ResolveLegacySymbol("BOOK_BLUE");
        var definition = contentItems.GetRequired(itemId);
        Assert.Equal(0, definition.SpellId);
        Assert.Equal(25, definition.MiscId);
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [definition.NativeItemIndex] = new(definition.NativeItemIndex, definition.ItemId, "BOOK_BLUE",
                    ItemActionKind: DiabloItemActionKind.ReadBook),
            },
        };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        world.ConfigureCathedralItems(profile, null, Fingerprint, contentItems, options.Spells!);

        Assert.True(world.HasNativeCathedralItems);
    }

    [Fact]
    public void ConflictingLegacyStaffSourceIsRejected()
    {
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [0] = new(0, 1001, "test_staff", SpellSourceKind: DiabloItemSpellSourceKind.ConsumableItem),
            },
        };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var catalog = WithDefinition(options.Items!, 1001, definition => definition with {
            StableSymbol = "test_staff",
            MiscId = 23,
            SpellId = 101,
            Charges = 40,
            MaxCharges = 40,
            Usable = false,
        });
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, catalog, options.Spells!));
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void ExplicitNoneContentSourceRejectsLegacyStaffChargedBinding()
    {
        var contentItems = LoadBaseLegacyItem("SHORTSTAFF", "SHORT_STAFF", "Staff", "STAFF");
        var definition = contentItems.GetRequired(contentItems.ResolveLegacySymbol("SHORTSTAFF"));
        contentItems = WithDefinition(contentItems, definition.ItemId, item => item with {
            SpellSourceKind = DiabloItemSpellSourceKind.None,
        });
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemBindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
                [definition.NativeItemIndex] = new(definition.NativeItemIndex, definition.ItemId, "SHORTSTAFF",
                    SpellSourceKind: DiabloItemSpellSourceKind.ChargedItem),
            },
        };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, contentItems, options.Spells!));

        Assert.Empty(world.WorldItems);
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void SwordCannotClaimChargedSpellSource()
    {
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[1] = bindings[1] with { SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem };
        profile = profile with { ItemBindings = bindings };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, options.Items!, options.Spells!));

        Assert.Empty(world.WorldItems);
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void ExplicitContentActionMustMatchItsStableBinding()
    {
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[2] = bindings[2] with { ItemActionKind = DiabloItemActionKind.Identify };
        profile = profile with { ItemBindings = bindings };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var catalog = WithDefinition(options.Items!, 1003, definition => definition with {
            ItemActionKind = DiabloItemActionKind.ReadBook,
        });
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, catalog, options.Spells!));
        Assert.Empty(world.WorldItems);
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void SwordCannotClaimAnUnresolvedItemAction()
    {
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[1] = bindings[1] with { ItemActionKind = DiabloItemActionKind.Identify };
        profile = profile with { ItemBindings = bindings };
        var options = CathedralItemLiveSimulationTests.Options(profile);
        var catalog = WithDefinition(options.Items!, 1002, definition => definition with {
            ItemActionKind = DiabloItemActionKind.None,
        });
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112);

        Assert.Throws<InvalidDataException>(() =>
            world.ConfigureCathedralItems(profile, null, Fingerprint, catalog, options.Spells!));

        Assert.Empty(world.WorldItems);
        Assert.False(world.HasNativeCathedralItems);
    }

    [Fact]
    public void LingeringProjectileCanPlaceNonGoldLootOnADeadPlayersTile()
    {
        const int monsterX = 10;
        const int monsterY = 10;
        const int deadPlayerX = 11;
        const int deadPlayerY = 10;
        var profile = Profile(AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint()) with {
            ItemCatalog = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog(
                items: [
                    new(0, 0, 0, 4, 7, 4, 11, "Gold", "Gold", 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, true, 10, 4, Available: false),
                    new(1, 1, 1, 1, 7, 10, 1, "Sword", "Sword", 0,
                        10, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, false, 10, 4),
                ],
                noDropPercent: 0,
                goldPercent: 0),
        };
        var level = profile.LevelProfiles.Single(value => value.LevelId == 1);
        var solidTiles = Enumerable.Repeat(true, AuthoritativeCathedralItemRuntime.CellCount).ToArray();
        solidTiles[monsterY * AuthoritativeCathedralItemRuntime.Width + monsterX] = false;
        solidTiles[deadPlayerY * AuthoritativeCathedralItemRuntime.Width + deadPlayerX] = false;
        profile = profile with {
            LevelProfiles = [level with { SolidTiles = solidTiles }],
        };
        var target = CathedralItemLiveSimulationTests.Target(9, 7, monsterX, monsterY);
        target.IsPetrified = true;
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = AuthoritativeCathedralItemRuntime.Width,
            FallbackHeight = AuthoritativeCathedralItemRuntime.Height,
            FallbackBlockedCells = Enumerable.Range(0, solidTiles.Length)
                .Where(index => solidTiles[index]),
            RandomSeed = 71,
            CombatTargets = [target],
            CombatRules = new(100, 1, 10),
            NativeMonsterMetadataFingerprint = Fingerprint,
            NativeCathedralItems = profile,
            SuppressInitialWorldScaling = true,
            Items = new(profile.ItemBindings.Values.Select(binding => new AuthoritativeItemDefinition(
                binding.StableItemId, binding.NativeItemIndex == 0 ? 11 : 1, 999, 999, 7, 12, 9, 50, 50,
                binding.InventoryWidth, binding.InventoryHeight, true) {
                    NativeItemIndex = binding.NativeItemIndex,
                    StableSymbol = binding.StableSymbol,
                    ItemActionKind = binding.ItemActionKind ?? DiabloItemActionKind.None,
                }).Append(new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                    NativeItemIndex = 23,
                    MiscId = 44,
                })),
            Spells = new([new AuthoritativeSpellDefinition(101, 1, 0, 0, 0, 0) { NativeSpellId = 1 }]),
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var deadPlayer = DiabloPlayerTemplate.CreateDefault(1000, 1000, 0, 0).CreatePlayer();
        deadPlayer.EntityId = 1;
        deadPlayer.LevelId = 1;
        deadPlayer.PositionX = deadPlayerX;
        deadPlayer.PositionY = deadPlayerY;
        deadPlayer.Life = 0;
        simulation.WorldState.Projectiles.Add(700, new AuthoritativeProjectile(
            700, deadPlayer.EntityId, target.EntityId, 101, 1,
            monsterX, monsterY, monsterX, monsterY,
            10, AuthoritativeDamageType.Physical, 0, 2) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
        });

        var events = simulation.AdvanceTick(1, [deadPlayer]);

        Assert.Equal(0, target.HitPoints);
        Assert.Empty(simulation.WorldState.CombatTargets);
        var droppedItem = Assert.Single(simulation.WorldState.WorldItems.Values);
        Assert.Equal(1U, droppedItem.LevelId);
        Assert.Equal((deadPlayerX, deadPlayerY), (droppedItem.PositionX, droppedItem.PositionY));
        Assert.NotEqual(11, droppedItem.State.ItemType);
        Assert.Contains(events, value => value.Event.Kind == DiabloGameplayEventKind.Damage
            && value.Event.SourceEntityId == deadPlayer.EntityId
            && value.Event.TargetEntityId == target.EntityId);
        Assert.Contains(events, value => value.Event.Kind == DiabloGameplayEventKind.Experience
            && value.Event.SourceEntityId == deadPlayer.EntityId);
    }

    private static AuthoritativeCathedralItemRuntimeProfile Profile(DiabloCathedralItemCheckpoint checkpoint)
    {
        var profile = CathedralItemLiveSimulationTests.Profile(checkpoint);
        var bindings = profile.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[1] = bindings[1] with { StableSymbol = "short_sword" };
        return profile with { ItemBindings = bindings };
    }

    private static AuthoritativeWorldItem TownItem()
    {
        var state = new AuthoritativeItemState(
            0, 11, 4, 5, false, true, 0, 0, 0, 111, 222,
            0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0) {
            StableItemId = 1001,
            BaseName = "Gold",
            IdentifiedName = "Gold",
            NativeCursorGraphic = 4,
        };
        return new(900, 0, 62, 63, 0, 100, state);
    }

    private static IEnumerable<AuthoritativeWorldItem> ProjectedPool(
        AuthoritativeCathedralItemRuntimeProfile profile, DiabloCathedralItemCheckpoint checkpoint)
    {
        var runtime = new AuthoritativeCathedralItemRuntime(profile, checkpoint);
        foreach (var level in runtime.CaptureCheckpoint().Levels) {
            foreach (var slotBinding in level.SlotEntityBindings.Where(value => value.EntityId != 0)) {
                var raw = level.Items[slotBinding.Slot];
                var binding = profile.ItemBindings[raw.ItemIndex];
                var projection = runtime.ProjectNativeItem(slotBinding.Slot, slotBinding.EntityId, raw.Seed,
                    checked((uint)Math.Max(0, raw.Identified ? raw.IdentifiedValue : raw.Value)), raw,
                    binding, profile.ContentInventoryDimensions);
                yield return new(projection.EntityId, level.LevelId, projection.Raw.PositionX,
                    projection.Raw.PositionY, projection.ItemSeed, projection.Price, projection.State);
            }
        }
    }

    private static AuthoritativeItemCatalog WithDefinition(AuthoritativeItemCatalog catalog, uint itemId,
        Func<AuthoritativeItemDefinition, AuthoritativeItemDefinition> update)
        => new(catalog.Definitions.Select(definition => definition.ItemId == itemId
            ? update(definition)
            : definition));

    private static AuthoritativeItemCatalog LoadBaseLegacyItem(
        string uniqueBaseItem, string cursorGraphic, string itemType, string miscId)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                var contentRoot = Path.Combine(directory.FullName, "server", "content", "base");
                var itemDataPath = Path.Combine(contentRoot, "itemdat.tsv");
                if (!File.Exists(itemDataPath))
                    continue;
                var lines = File.ReadAllLines(itemDataPath);
                var columns = lines[0].Split('\t')
                    .Select((name, index) => (name, index))
                    .ToDictionary(value => value.name, value => value.index, StringComparer.Ordinal);
                var row = lines.Skip(1).FirstOrDefault(line => {
                    var values = line.Split('\t');
                    return string.Equals(values[columns["uniqueBaseItem"]], uniqueBaseItem,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(values[columns["cursorGraphic"]], cursorGraphic,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(values[columns["itemType"]], itemType,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(values[columns["miscId"]], miscId,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(values[columns["spell"]], "Null",
                            StringComparison.OrdinalIgnoreCase);
                });
                if (row is null)
                    throw new InvalidDataException("Could not locate the requested null-spell base item row.");
                var isolatedTable = string.Join(Environment.NewLine, lines[0], row, string.Empty);
                return AuthoritativeItemCatalog.LoadLegacyTsv(
                    "itemdat.tsv",
                    isolatedTable,
                    nativeMiscCatalog: AuthoritativeNativeMiscCatalog.LoadTsv(
                        "native_item_misc.tsv", File.ReadAllText(Path.Combine(contentRoot, "native_item_misc.tsv"))));
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository base item data.");
    }
}

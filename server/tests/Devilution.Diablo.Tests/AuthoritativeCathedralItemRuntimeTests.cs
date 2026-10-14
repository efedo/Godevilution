using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralItemRuntimeTests
{
    [Fact]
    public void EmptyCheckpointRoundTripsWithGlobalUniqueHistory()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();

        var checkpoint = runtime.CaptureCheckpoint();

        checkpoint.ValidateShape();
        Assert.Equal(1, checkpoint.Levels.Count);
        Assert.Empty(checkpoint.UniqueItemFlags);
        Assert.Equal(0, checkpoint.Levels[0].ActiveItemCount);
    }

    [Fact]
    public void NativeProjectionPreservesZeroCreateInfoAndRawIdentity()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();
        var raw = AuthoritativeCathedralItemRuntimeTestData.Item(0) with {
            Seed = 0,
            CreateInfo = 0,
            BaseName = "Raw zero item",
            IdentifiedName = "Raw zero item",
            SpellId = -1,
        };

        var projection = runtime.ProjectNativeItem(
            0, 77, 0, 100, raw, runtime.Profile.ItemBindings[0],
            runtime.Profile.ContentInventoryDimensions);

        Assert.Equal((uint)0, projection.State.CreateInfo);
        Assert.Equal((uint)1001, projection.State.StableItemId);
        Assert.Equal(-1, projection.State.SpellId);
        Assert.Equal("Raw zero item", projection.State.BaseName);
    }

    [Fact]
    public void RemoveUsesNativeSwapAndLeavesRawSlotForReuse()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());

        Assert.True(runtime.RemoveWorldItem(1, 101, out var removedSlot));
        var level = runtime.CaptureLevel(1);

        Assert.Equal(0, removedSlot);
        Assert.Equal(1, level.ActiveItemCount);
        Assert.Equal(1, level.ActiveItemIds[0]);
        Assert.Equal(0, level.ActiveItemIds[1]);
        Assert.Equal((uint)0, level.SlotEntityBindings[0].EntityId);
        Assert.Equal((uint)102, level.SlotEntityBindings[1].EntityId);
        Assert.Equal(0, level.SignedItemRefs[1 * AuthoritativeCathedralItemRuntime.Width + 1]);
        Assert.Equal(0, level.Items[0].ItemIndex);
        level.ValidateShape();
    }

    [Fact]
    public void PublicCheckpointShapeRejectsVisibleTopologyCorruption()
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var level = source.Levels[0];
        var badRefs = level.SignedItemRefs.ToArray();
        badRefs[3 * AuthoritativeCathedralItemRuntime.Width + 3] = 1;
        var corrupted = source with {
            Levels = new[] { level with { SignedItemRefs = badRefs } },
        };

        Assert.Throws<InvalidDataException>(() => corrupted.ValidateShape());
    }

    [Fact]
    public void PublicCheckpointShapeRejectsInactiveVisibleBinding()
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var level = source.Levels[0] with {
            ActiveItemCount = 1,
            SignedItemRefs = new int[AuthoritativeCathedralItemRuntime.CellCount],
        };

        Assert.Throws<InvalidDataException>(() => (source with { Levels = new[] { level } }).ValidateShape());
    }

    [Fact]
    public void PublicCheckpointShapeRejectsUnboundVisibleTileReference()
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var bindings = source.Levels[0].SlotEntityBindings.ToArray();
        bindings[0] = bindings[0] with { EntityId = 0 };

        Assert.Throws<InvalidDataException>(() => (source with {
            Levels = new[] { source.Levels[0] with { SlotEntityBindings = bindings } },
        }).ValidateShape());
    }

    [Fact]
    public void PublicCheckpointShapeRequiresCanonicalFingerprints()
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint();

        Assert.Throws<InvalidDataException>(() => (source with { MetadataFingerprint = "metadata" }).ValidateShape());
    }

    [Fact]
    public void FailedReplacementDoesNotClearExistingTile()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var before = runtime.CaptureCheckpoint();
        var existing = runtime.GetProjection(1, 101)!;
        var replacement = runtime.ProjectNativeItem(
            existing.Slot,
            existing.EntityId,
            existing.ItemSeed,
            existing.Price,
            existing.Raw with { PositionX = 2, PositionY = 2 },
            existing.Binding,
            runtime.Profile.ContentInventoryDimensions);

        Assert.False(runtime.TryReplaceWorldItem(1, 101, replacement));
        var after = runtime.CaptureCheckpoint();
        Assert.Equal(before.Levels[0].SignedItemRefs, after.Levels[0].SignedItemRefs);
        Assert.Equal(before.Levels[0].Items[0].PositionX, after.Levels[0].Items[0].PositionX);
        Assert.Equal(before.Levels[0].Items[0].PositionY, after.Levels[0].Items[0].PositionY);
    }

    [Fact]
    public void ForgedProjectionBonusIsRejectedBeforeMutation()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var existing = runtime.GetProjection(1, 101)!;
        var forged = existing with { State = existing.State with { PlusDamage = 999 } };

        Assert.Throws<InvalidDataException>(() => runtime.TryReplaceWorldItem(1, 101, forged));
        Assert.Equal(2, runtime.CaptureLevel(1).ActiveItemCount);
    }

    [Fact]
    public void PickupCopiesBookMinimumMagicAndClearsPregenInInactiveSlot()
    {
        var checkpoint = AuthoritativeCathedralItemRuntimeTestData.BoundBookCheckpoint();
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(checkpoint);
        var projection = runtime.GetProjection(1, 124)!;
        var worldItem = new AuthoritativeWorldItem(
            124, 1, projection.Raw.PositionX, projection.Raw.PositionY,
            projection.ItemSeed, projection.Price, projection.State);
        var pickedUp = projection.State with { MinimumMagic = 123, StatFlag = true };

        Assert.True(runtime.RemoveWorldItem(worldItem, pickedUp));
        var inactive = runtime.CaptureLevel(1).Items[24];
        Assert.Equal(123, inactive.MinimumMagic);
        Assert.Equal(0, inactive.CreateInfo & 0x8000);
        Assert.True(inactive.StatFlag);
    }

    [Fact]
    public void ProjectWorldItemRehydratesAndCanonicalizesPrice()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();
        var state = new AuthoritativeItemState(
            0, 11, 4, 5, false, true, 0, 0, 0, 111, 222,
            0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0) {
            StableItemId = 1001,
            BaseName = "Gold",
            IdentifiedName = "Gold",
            NativeCursorGraphic = 4,
        };
        var worldItem = new AuthoritativeWorldItem(200, 1, 4, 5, 99, 999, state);

        var projection = runtime.ProjectWorldItem(worldItem);

        Assert.Equal((uint)222, projection.Price);
        Assert.Equal((uint)99, projection.ItemSeed);
        Assert.Equal(4, projection.Raw.PositionX);
        Assert.Equal(5, projection.Raw.PositionY);
    }

    [Fact]
    public void ProjectWorldItemAppliesModifiedOwnedFieldsForBoundItem()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var existing = runtime.GetProjection(1, 101)!;
        var modifiedState = existing.State with {
            Identified = true,
            Value = 333,
            IdentifiedValue = 444,
        };
        var worldItem = new AuthoritativeWorldItem(
            101, 1, existing.Raw.PositionX, existing.Raw.PositionY,
            existing.ItemSeed, 1, modifiedState);

        var projection = runtime.ProjectWorldItem(worldItem);

        Assert.True(projection.Raw.Identified);
        Assert.Equal(333, projection.Raw.Value);
        Assert.Equal(444, projection.Raw.IdentifiedValue);
        Assert.Equal((uint)444, projection.Price);
    }

    [Fact]
    public void CurrentOccupancyIsUsedForNativeWorldView()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();
        var occupancy = AuthoritativeCathedralItemRuntimeTestData.Occupancy(solidIndex: 7);

        var state = runtime.BuildWorldState(1, occupancy);

        Assert.True(state.SolidTiles[7]);
        Assert.False(state.SolidTiles[0]);
    }

    [Fact]
    public void TransactionCannotCrossRuntimeIdentity()
    {
        var first = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();
        var second = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime();
        var transaction = first.PrepareLoot(new AuthoritativeCathedralItemDeathRequest(
            1, 0, 7, 10, 10, AuthoritativeCathedralItemRuntimeTestData.Occupancy(),
            IsPlayerMinion: true));

        Assert.Throws<InvalidOperationException>(() => second.Commit(transaction));
    }

    [Fact]
    public void TransactionCannotCommitAfterRawPoolMutation()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var transaction = runtime.PrepareLoot(new AuthoritativeCathedralItemDeathRequest(
            1, 0, 7, 10, 10, AuthoritativeCathedralItemRuntimeTestData.Occupancy(),
            IsPlayerMinion: true));
        Assert.True(runtime.RemoveWorldItem(1, 101, out _));

        Assert.Throws<InvalidOperationException>(() => runtime.Commit(transaction));
    }

    [Fact]
    public void PoolFullDeathLeavesAnOrphanWithoutWorldProjection()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.FullPoolCheckpoint(),
            forceDrop: true);
        var transaction = runtime.PrepareLoot(new AuthoritativeCathedralItemDeathRequest(
            1, 0, 7, 10, 10, AuthoritativeCathedralItemRuntimeTestData.Occupancy()));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.PoolFull, transaction.Loot.Outcome);
        var result = runtime.Commit(transaction);
        Assert.False(result.HasWorldItem);
        Assert.Equal(127, runtime.CaptureLevel(1).ActiveItemCount);
    }

    [Fact]
    public void PlacementFailureCommitsNativeOrphanWithoutEntityBinding()
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(forceDrop: true);
        var transaction = runtime.PrepareLoot(new AuthoritativeCathedralItemDeathRequest(
            1, 0, 7, 10, 10,
            AuthoritativeCathedralItemRuntimeTestData.Occupancy(solidAll: true)));

        Assert.Equal(AuthoritativeCathedralMonsterLootOutcome.DroppedItem, transaction.Loot.Outcome);
        Assert.False(transaction.Loot.PlacementSucceeded);
        var result = runtime.Commit(transaction);
        var level = runtime.CaptureLevel(1);
        Assert.False(result.HasWorldItem);
        Assert.Equal((uint)0, level.SlotEntityBindings[0].EntityId);
        Assert.Equal(1, level.ActiveItemCount);
    }
}

/** Shared valid raw-item runtime fixture for live integration tests. */
internal static class AuthoritativeCathedralItemRuntimeTestData
{
    private const uint LevelId = 1;
    private const string MetadataFingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string GenerationFingerprint = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    internal sealed class ProfileRuntime : AuthoritativeCathedralItemRuntime
    {
        internal ProfileRuntime(
            DiabloCathedralItemCheckpoint? checkpoint = null,
            bool forceDrop = false)
            : base(Profile(forceDrop), checkpoint)
        {
        }
    }

    internal static AuthoritativeCathedralItemRuntimeProfile Profile(bool forceDrop = false)
    {
        var sourceCatalog = AuthoritativeCathedralThemeItemMetadataTests.CreateCatalog();
        var itemCatalog = AuthoritativeCathedralThemeItemMetadataTests.CreateCatalog(
            rules: forceDrop
                ? sourceCatalog.Rules with { NoDropPercent = 0, GoldPercent = 100 }
                : null);
        if (forceDrop) {
            var gold = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog().Items.Single(row => row.NativeItemIndex == 0);
            gold = gold with {
                AnimationFrameCount = itemCatalog.CursorAnimations[gold.CursorGraphic].FrameCount,
                AnimationTicksPerFrame = itemCatalog.CursorAnimations[gold.CursorGraphic].TicksPerFrame,
            };
            itemCatalog = new(itemCatalog.Items.Select(row => row.NativeItemIndex == 0 ? gold : row),
                itemCatalog.Prefixes, itemCatalog.Suffixes, itemCatalog.Uniques, itemCatalog.Spells,
                itemCatalog.CursorAnimations, itemCatalog.GlyphWidths, itemCatalog.Cursors,
                itemCatalog.AlwaysMagicMiscIds, itemCatalog.Rules, itemCatalog.ReplacementGlyphWidth);
        }
        var bindings = new Dictionary<int, AuthoritativeCathedralItemContentBinding> {
            [0] = new(0, 1001, "gold"),
            [1] = new(1, 1002, "short_sword"),
            [24] = new(24, 1024, "book"),
        };
        var levelProfile = new AuthoritativeCathedralItemLevelProfile(
            LevelId,
            1,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            Array.Empty<AuthoritativeCathedralItemObjectBinding>());
        var checkpoint = EmptyCheckpoint();
        var monsters = Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
            .Select(index => new AuthoritativeCathedralMonsterRuntimeDefinition(
                index, $"MONSTER_{index:000}", $"Monster {index}", 10, 20, 4, 20, 4,
                0, 0, 0, 100, 0));
        return new AuthoritativeCathedralItemRuntimeProfile(
            MetadataFingerprint,
            GenerationFingerprint,
            checkpoint,
            itemCatalog,
            new AuthoritativeCathedralThemeItemPolicy(1),
            new AuthoritativeCathedralMonsterRuntimeCatalog(monsters),
            Array.Empty<AuthoritativeCathedralUniqueDefinition>(),
            bindings,
            new AuthoritativeCathedralItemInventoryDimensions(),
            new[] { levelProfile },
            itemCatalog.Spells.ToDictionary(spell => spell.NativeSpellId, spell => spell.NativeSpellId));
    }

    internal static DiabloCathedralItemCheckpoint EmptyCheckpoint()
        => new(
            MetadataFingerprint,
            GenerationFingerprint,
            Array.Empty<int>(),
            new[] { Level(0, Array.Empty<uint>()) });

    internal static DiabloCathedralItemCheckpoint TwoBoundItemCheckpoint()
    {
        var items = Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
            .Select(_ => Item(-1)).ToArray();
        items[0] = Item(0, 1, 1);
        items[1] = Item(1, 2, 2);
        var refs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        refs[1 * AuthoritativeCathedralItemRuntime.Width + 1] = 1;
        refs[2 * AuthoritativeCathedralItemRuntime.Width + 2] = 2;
        var bindings = Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
            .Select(slot => new DiabloCathedralItemSlotEntityBinding(slot, slot switch {
                0 => 101u,
                1 => 102u,
                _ => 0u,
            })).ToArray();
        return new(
            MetadataFingerprint,
            GenerationFingerprint,
            Array.Empty<int>(),
            new[] { Level(2, new[] { 101u, 102u }, items, refs, bindings) });
    }

    internal static DiabloCathedralItemCheckpoint BoundBookCheckpoint()
    {
        var items = Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
            .Select(_ => Item(-1)).ToArray();
        items[24] = Item(24, 3, 3) with {
            CreateInfo = 0x8000,
            MinimumMagic = 5,
        };
        var refs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        refs[3 * AuthoritativeCathedralItemRuntime.Width + 3] = 25;
        var bindings = Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
            .Select(slot => new DiabloCathedralItemSlotEntityBinding(slot, slot == 24 ? 124u : 0u))
            .ToArray();
        return new(
            MetadataFingerprint,
            GenerationFingerprint,
            Array.Empty<int>(),
            new[] { Level(25, Array.Empty<uint>(), items, refs, bindings) });
    }

    internal static DiabloCathedralItemCheckpoint FullPoolCheckpoint()
        => new(
            MetadataFingerprint,
            GenerationFingerprint,
            Array.Empty<int>(),
            new[] { Level(AuthoritativeCathedralItemRuntime.ActiveCapacity, Array.Empty<uint>()) });

    internal static AuthoritativeCathedralItemOccupancyContext Occupancy(
        int solidIndex = -1,
        bool solidAll = false)
    {
        var solid = Enumerable.Repeat(solidAll, AuthoritativeCathedralItemRuntime.CellCount).ToArray();
        if (solidIndex >= 0)
            solid[solidIndex] = true;
        return new(
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount],
            solid,
            new bool[AuthoritativeCathedralItemRuntime.CellCount],
            Array.Empty<bool>(),
            Array.Empty<bool>());
    }

    internal static AuthoritativeCathedralInitialItemState Item(
        int nativeItemIndex,
        int positionX = 0,
        int positionY = 0)
        => new() {
            ItemIndex = nativeItemIndex,
            ItemType = nativeItemIndex == 0 ? 11 : 1,
            PositionX = positionX,
            PositionY = positionY,
            CursorGraphic = 4,
            BaseName = nativeItemIndex < 0 ? string.Empty : $"Item{nativeItemIndex}",
            IdentifiedName = nativeItemIndex < 0 ? string.Empty : $"Item{nativeItemIndex}",
            SpellId = -1,
            Value = 100,
            IdentifiedValue = 100,
        };

    private static DiabloCathedralItemLevelCheckpoint Level(
        int activeCount,
        IReadOnlyList<uint> entities,
        IReadOnlyList<AuthoritativeCathedralInitialItemState>? items = null,
        IReadOnlyList<int>? refs = null,
        IReadOnlyList<DiabloCathedralItemSlotEntityBinding>? bindings = null)
    {
        var slotBindings = bindings?.ToArray() ?? Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
            .Select(slot => new DiabloCathedralItemSlotEntityBinding(
                slot,
                slot < entities.Count ? entities[slot] : 0u)).ToArray();
        return new(
            LevelId,
            1,
            items?.ToArray() ?? Enumerable.Range(0, AuthoritativeCathedralItemRuntime.PoolCapacity)
                .Select(_ => Item(-1)).ToArray(),
            Enumerable.Range(0, AuthoritativeCathedralItemRuntime.ActiveCapacity).ToArray(),
            activeCount,
            refs?.ToArray() ?? new int[AuthoritativeCathedralItemRuntime.CellCount],
            slotBindings,
            0,
            "generation");
    }
}

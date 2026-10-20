using System.Collections.ObjectModel;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Pure bounded parity tests for ordinary Cathedral InitItems/AddInitItems. */
public sealed class AuthoritativeCathedralInitialItemGeneratorTests
{
    private const int TileCount = AuthoritativeCathedralInitialItemGenerator.NativeTileCount;

    [Theory]
    [InlineData(1u, 3, 1367356842u)]
    [InlineData(5u, 5, 3714366108u)]
    [InlineData(14u, 5, 2172189869u)]
    [InlineData(988045466u, 1, 2218967789u)]
    public void OrdinaryFreshVectorsMatchIndependentLcGRoll(uint seed, int expectedCount, uint expectedFinalState)
    {
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(seed));

        // These literals are independently derived from the native LCG:
        // discard one, GenerateRnd(3)+3, then (x80,y80,AdvanceRndSeed,
        // re-seed,GenerateRnd(2),GenerateRnd(1)) for each item.
        Assert.Equal(expectedCount, result.ActiveItemCount);
        Assert.Equal(expectedFinalState, result.FinalRandomState);
        Assert.Equal(Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.ActiveCapacity),
            result.ActiveItemIds);
        Assert.Equal(expectedCount, result.ItemTileRefs.Count(value => value > 0));
    }

    [Fact]
    public void FamilyAwareInitialItemsRequireAnExplicitMatchingCatacombsContext()
    {
        var context = AuthoritativeNativeDungeonFamilyContext.Catacombs(5);
        var parameters = CreateParameters(2588, depth: 5, familyContext: context);

        var result = AuthoritativeCathedralInitialItemGenerator.Generate(parameters, context);

        Assert.True(result.ActiveItemCount > 0);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralInitialItemGenerator.Generate(parameters));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralInitialItemGenerator.Generate(
            parameters, AuthoritativeNativeDungeonFamilyContext.Catacombs(6)));
    }

    [Fact]
    public void GetItemAttrsCopiesRawPotionMetadataAndConsumesLimitOneAcDraw()
    {
        var healing = Definition(AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
            "healing externally supplied", "healing externally supplied", 32, 19, 0, 0);
        var mana = Definition(AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
            "mana externally supplied", "mana externally supplied", 39, 29, 0, 0);
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(
            CreateParameters(2588, definitions: [healing, mana]));

        Assert.All(result.Items.Take(result.ActiveItemCount), item => {
            Assert.Contains(item.ItemIndex, new[] {
                AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
                AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
            });
            var expected = item.ItemIndex == healing.NativeItemId ? healing : mana;
            Assert.Equal(expected.Name, item.BaseName);
            Assert.Equal(expected.IdentifiedName, item.IdentifiedName);
            Assert.Equal(expected.ItemType, item.ItemType);
            Assert.Equal(expected.CursorGraphic, item.CursorGraphic);
            Assert.Equal(expected.ItemClass, item.ItemClass);
            Assert.Equal(expected.EquipLocation, item.EquipLocation);
            Assert.Equal(expected.Value, item.Value);
            Assert.Equal(expected.Value, item.IdentifiedValue);
            Assert.Equal(expected.Durability, item.Durability);
            Assert.Equal(expected.Durability, item.MaxDurability);
            Assert.Equal(0, item.ArmorClass);
            Assert.Equal(expected.AnimationFrameCount, item.Animation.NumberOfFrames);
            Assert.Equal(expected.AnimationFrameCount - 1, item.Animation.CurrentFrame);
            Assert.False(item.AnimFlag);
            Assert.Equal(1, item.SelectionRegion);
            Assert.False(item.Identified);
        });
    }

    [Fact]
    public void CopiesExternallySuppliedScalarsAndTruncatesUtf8NamesAtNativeBoundary()
    {
        var sourceName = new string('a', 62) + "é";
        var healing = Definition(AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
            sourceName, sourceName, 201, 7, 2, 5) with {
                EquipLocation = -1,
                ItemClass = 5,
                MinDamage = 4,
                MaxDamage = 9,
                Flags = 0xAABBCCDD,
                SpellId = -7,
                Value = 32000,
                Durability = 200,
                MinimumStrength = 127,
                MinimumMagic = 255,
                MinimumDexterity = 127,
            };
        var mana = Definition(AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
            "mana", "mana", 202, 8, 6, 9) with {
                Value = 1234,
                Durability = 17,
            };

        var result = AuthoritativeCathedralInitialItemGenerator.Generate(
            CreateParameters(2588, definitions: [healing, mana]));
        var item = result.Items.Take(result.ActiveItemCount)
            .First(value => value.ItemIndex == healing.NativeItemId);

        Assert.Equal(new string('a', 62), item.BaseName);
        Assert.Equal(new string('a', 62), item.IdentifiedName);
        Assert.Equal(healing.CursorGraphic, item.CursorGraphic);
        Assert.Equal(healing.EquipLocation, item.EquipLocation);
        Assert.Equal(healing.ItemClass, item.ItemClass);
        Assert.Equal(healing.MinDamage, item.MinDamage);
        Assert.Equal(healing.MaxDamage, item.MaxDamage);
        Assert.Equal(healing.Flags, item.Flags);
        Assert.Equal(healing.SpellId, item.SpellId);
        Assert.Equal(healing.Value, item.Value);
        Assert.Equal(healing.Value, item.IdentifiedValue);
        Assert.Equal(healing.Durability, item.Durability);
        Assert.Equal(healing.Durability, item.MaxDurability);
        Assert.InRange(item.ArmorClass, healing.MinArmorClass, healing.MaxArmorClass);
        Assert.Equal(healing.MinimumStrength, item.MinimumStrength);
        Assert.Equal(healing.MinimumMagic, item.MinimumMagic);
        Assert.Equal(healing.MinimumDexterity, item.MinimumDexterity);
        Assert.Equal(healing.AnimationFrameCount, item.Animation.NumberOfFrames);
    }

    [Fact]
    public void InitItemsPreservesUntouchedInactiveFieldsAndAllocateItemZerosSelectedSlot()
    {
        var initial = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            .Select(_ => new AuthoritativeCathedralInitialItemState {
                ItemType = 7,
                PositionX = 91,
                PositionY = 92,
                PlusDamage = 31,
                ItemIndex = 42,
                BaseName = "sentinel",
                Identified = true,
            }).ToArray();
        initial[100] = initial[100] with { PlusDamage = 777, ItemIndex = 123, BaseName = "inactive" };
        initial[127] = initial[127] with { PlusDamage = 778, ItemIndex = 124, BaseName = "scratch" };
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(
            CreateParameters(1, items: initial, activeItemIds: Enumerable.Repeat(127, 127).ToArray(), activeItemCount: 126));

        var inactive = result.Items[100];
        Assert.Equal(-1, inactive.ItemType); // Item::clear only changes this enum.
        Assert.Equal(0, inactive.PositionX);
        Assert.Equal(0, inactive.PositionY);
        Assert.Equal(777, inactive.PlusDamage);
        Assert.Equal(123, inactive.ItemIndex);
        Assert.Equal("inactive", inactive.BaseName);
        Assert.False(inactive.Identified);

        var scratch = result.Items[127];
        Assert.Equal(-1, scratch.ItemType);
        Assert.Equal(778, scratch.PlusDamage);
        Assert.Equal(124, scratch.ItemIndex);
        Assert.Equal("scratch", scratch.BaseName);

        // Slot zero is selected first after InitItems resets ActiveItems,
        // irrespective of the prior ActiveItems/ActiveItemCount input.
        Assert.Equal(0, result.Items[0].PlusDamage);
        Assert.NotEqual("sentinel", result.Items[0].BaseName);
        Assert.Contains(result.Items[0].ItemIndex, new[] {
            AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
            AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
        });
        Assert.Equal(127, result.ActiveItemIds.Count);
        Assert.Equal(0, result.ActiveItemIds[0]);
        Assert.Equal(126, result.ActiveItemIds[^1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(127, 127)]
    [InlineData(128, -128)]
    [InlineData(255, -1)]
    public void RawByteRequirementsUseNativeSignedItemFields(int raw, int expected)
    {
        var definitions = new[] {
            Definition(24, "heal", "heal", 32, 1, 0, 0),
            Definition(25, "mana", "mana", 39, 127, 0, 0),
        }.Select(value => value with {
            MinimumStrength = raw, MinimumMagic = raw, MinimumDexterity = raw,
        }).ToArray();
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(2588, definitions: definitions));
        Assert.All(result.Items.Take(result.ActiveItemCount), item => {
            Assert.Equal(expected, item.MinimumStrength);
            Assert.Equal(expected, item.MinimumDexterity);
            Assert.Equal(raw, item.MinimumMagic);
            Assert.Equal(new AuthoritativeCathedralInitialItemAnimation {
                TicksPerFrame = 1, NumberOfFrames = item.ItemIndex == 24 ? 1 : 127,
                CurrentFrame = item.ItemIndex == 24 ? 0 : 126,
            }, item.Animation);
        });
    }

    [Theory]
    [InlineData("a\0b", "a")]
    [InlineData("\0b", "")]
    [InlineData("é\0b", "é")]
    public void PotionNameProjectionStopsAtFirstNativeCStringTerminator(string source, string expected)
    {
        var definitions = new[] {
            Definition(24, source, source, 32, 16, 0, 0),
            Definition(25, source, source, 39, 16, 0, 0),
        };
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(2588, definitions: definitions));
        Assert.All(result.Items.Take(result.ActiveItemCount), item => {
            Assert.Equal(expected, item.BaseName);
            Assert.Equal(expected, item.IdentifiedName);
        });
    }

    [Theory]
    [InlineData(228, true)]
    [InlineData(229, false)]
    [InlineData(255, false)]
    public void CursorMetadataCannotEscapeTheNativeAnimationTable(int cursor, bool supported)
    {
        var definitions = new[] {
            Definition(24, "heal", "heal", cursor, 16, 0, 0),
            Definition(25, "mana", "mana", cursor, 16, 0, 0),
        };
        if (supported) {
            var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(2588, definitions: definitions));
            Assert.All(result.Items.Take(result.ActiveItemCount), item => Assert.Equal(cursor, item.CursorGraphic));
        } else {
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralInitialItemGenerator.Generate(
                CreateParameters(2588, definitions: definitions)));
        }
    }

    [Fact]
    public void SignedTileReferencesArePreservedAndDoNotBecomeBooleanOccupancy()
    {
        var tiles = EmptyTiles();
        tiles[0] = new(-128, -32768, -128, -128);
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(1, tiles: tiles));

        Assert.Equal(-128, result.ItemTileRefs[0]);
        Assert.Contains(result.ItemTileRefs, value => value > 0);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void OccupancySetPieceAndSolidRejectWhenNoBoundedPositionExists(
        bool setPiece, bool solid, bool item)
    {
        var tiles = Enumerable.Range(0, TileCount)
            .Select(_ => new AuthoritativeCathedralInitialItemTile(
                ItemRef: item ? 1 : 0, SetPiece: setPiece, Solid: solid)).ToArray();

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(
                1, tiles: tiles, maximumPlacementAttempts: 1)));
    }

    [Fact]
    public void RejectsNativeSignedWidthOverflowAndUnboundedPlacementRequests()
    {
        var tiles = EmptyTiles();
        tiles[0] = new(PlayerRef: 128);
        Assert.Throws<InvalidDataException>(() => CreateParameters(1, tiles: tiles));

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateParameters(1, maximumPlacementAttempts: 0));
    }

    [Theory]
    [InlineData(4203210069u)]
    [InlineData(2557708932u)]
    public void FailsClosedWhenSignedIntMinWouldEscapeNativeItemPlace(uint seed)
    {
        // Independently inverted LCG vectors place INT_MIN at the first x
        // draw (4203210069) or first y draw (2557708932). ItemPlace has no
        // bounds guard, so no retry policy is invented here: reject before
        // indexing the detached tile grid.
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(seed)));
    }

    [Fact]
    public void SyntheticEmptyStateUsesNativeDefaultEnumsAndOutputIsDetached()
    {
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(1));

        Assert.Equal(-1, result.Items[127].ItemType);
        Assert.Equal(-1, result.Items[127].ItemIndex);
        Assert.Equal(-1, result.Items[127].PrefixPower);
        Assert.Equal(-1, result.Items[127].SuffixPower);
        Assert.False(result.ShowUniqueItemInfoBox);
        Assert.Equal(0, result.ItemGetRecordCount);
        Assert.Equal(AuthoritativeCathedralInitialItemGenerator.ActiveCapacity, result.ItemGetRecords.Count);
        Assert.IsType<ReadOnlyCollection<AuthoritativeCathedralInitialItemState>>(result.Items);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<AuthoritativeCathedralInitialItemState>)result.Items)[0] = new());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<int>)result.ItemTileRefs)[0] = 5);
    }

    [Fact]
    public void RejectsUnsupportedPotionDefinitionsAndMissingNativeIds()
    {
        var badId = Definition(23, "bad", "bad", 2, 2, 0, 0);
        var mana = Definition(AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
            "mana", "mana", 2, 2, 0, 0);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(1, definitions: [badId, mana])));

        var heal = Definition(AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
            "heal", "heal", 2, 2, 0, 0);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(1, definitions: [heal, heal])));
    }

    public static TheoryData<AuthoritativeCathedralInitialItemState> InvalidRetainedSlots => new() {
        new AuthoritativeCathedralInitialItemState { PlusDamage = 40000 },
        new AuthoritativeCathedralInitialItemState { MinimumStrength = 128 },
        new AuthoritativeCathedralInitialItemState { MinimumDexterity = -129 },
        new AuthoritativeCathedralInitialItemState { MiscId = 128 },
        new AuthoritativeCathedralInitialItemState { ItemClass = 256 },
        new AuthoritativeCathedralInitialItemState { HellfireDamageArmorFlags = 256 },
        new AuthoritativeCathedralInitialItemState { ItemIndex = 32768 },
        new AuthoritativeCathedralInitialItemState { BaseName = null! },
        new AuthoritativeCathedralInitialItemState { IdentifiedName = new string('é', 32) },
        new AuthoritativeCathedralInitialItemState { BaseName = "embedded\0terminator" },
        new AuthoritativeCathedralInitialItemState { Animation = null! },
        new AuthoritativeCathedralInitialItemState { Animation = new() { NumberOfFrames = 256 } },
    };

    [Theory]
    [MemberData(nameof(InvalidRetainedSlots))]
    public void RejectsUnrepresentableInactiveAndScratchState(AuthoritativeCathedralInitialItemState invalid)
    {
        foreach (var slot in new[] { 100, 127 }) {
            var items = Enumerable.Range(0, 128).Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
            items[slot] = invalid;
            Assert.Throws<InvalidDataException>(() => CreateParameters(1, items: items));
        }
    }

    [Fact]
    public void RetainedSlotsAdmitRawFieldWidthBoundariesRatherThanShippedEnumValues()
    {
        var items = Enumerable.Range(0, 128).Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
        items[127] = new() {
            MinimumStrength = -128, MinimumDexterity = 127, MinimumMagic = 255,
            PlusDamage = short.MinValue, PlusEnemyArmorClass = short.MaxValue,
            MiscId = -128, SpellId = 127, ItemIndex = short.MinValue,
            ItemClass = 255, HellfireDamageArmorFlags = 255, BaseName = new string('a', 63),
            Animation = new() { NumberOfFrames = -128, CurrentFrame = 127 },
        };
        var result = AuthoritativeCathedralInitialItemGenerator.Generate(CreateParameters(1, items: items));
        Assert.Equal(items[127], result.Items[127]);
    }

    private static AuthoritativeCathedralInitialItemParameters CreateParameters(
        uint seed,
        IReadOnlyList<AuthoritativeCathedralInitialItemTile>? tiles = null,
        IReadOnlyList<AuthoritativeCathedralInitialItemState>? items = null,
        IReadOnlyList<int>? activeItemIds = null,
        int activeItemCount = 0,
        IReadOnlyList<AuthoritativeCathedralInitialItemDefinition>? definitions = null,
        int maximumPlacementAttempts = 1_000_000,
        int depth = 1,
        AuthoritativeNativeDungeonFamilyContext? familyContext = null)
    {
        var resolvedTiles = tiles ?? EmptyTiles();
        var resolvedItems = items ?? Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            .Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
        var resolvedActiveItemIds = activeItemIds
            ?? Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.ActiveCapacity).ToArray();
        var resolvedDefinitions = definitions ?? [
            Definition(AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
                "Potion of Healing", "Potion of Healing", 32, 9, 0, 0),
            Definition(AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
                "Potion of Mana", "Potion of Mana", 39, 10, 0, 0),
        ];
        if (familyContext is { } context)
            return new(depth, seed, resolvedTiles, resolvedItems, resolvedActiveItemIds, activeItemCount,
                resolvedDefinitions, maximumPlacementAttempts, context);
        return new(depth, seed, resolvedTiles, resolvedItems, resolvedActiveItemIds, activeItemCount,
            resolvedDefinitions, maximumPlacementAttempts);
    }

    private static AuthoritativeCathedralInitialItemTile[] EmptyTiles()
        => Enumerable.Repeat(new AuthoritativeCathedralInitialItemTile(), TileCount).ToArray();

    private static AuthoritativeCathedralInitialItemDefinition Definition(
        int id, string name, string identifiedName, int cursor, int frames,
        int minArmor, int maxArmor)
        => new(id, 0, cursor, name, identifiedName, 7, 3, 0, 0,
            minArmor, maxArmor, 0, id == AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId ? 3 : 6,
            0, 50, 0, 0, 0, 0, frames, 1, 0);
}

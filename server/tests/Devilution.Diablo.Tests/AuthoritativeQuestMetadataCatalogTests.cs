using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestMetadataCatalogTests
{
    [Fact]
    public void ResolvesStableQuestSymbolsInPoolEntries()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("quest", "magic_rock", 3001),
        ]);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\tquest_symbol\n1\t3001\tmagic_rock\n",
            questTypes,
            stableIds);

        Assert.Equal(3001U, Assert.Single(pools.Definitions).QuestId);
    }

    [Fact]
    public void LoadsNativeSetLevelNamesAndQuestPools()
    {
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            "set_level_id\tsymbol\tdisplay_name\n"
                + "0\tSL_NONE\t\n"
                + "1\tSL_SKELKING\tSkeleton King's Lair\n"
                + "2\tSL_BONECHAMB\tChamber of Bone\n"
                + "3\tSL_MAZE\tMaze\n"
                + "4\tSL_POISONWATER\tPoisoned Water Supply\n"
                + "5\tSL_VILEBETRAYER\tArchbishop Lazarus' Lair\n"
                + "6\tSL_ARENA_CHURCH\tChurch Arena\n"
                + "7\tSL_ARENA_HELL\tHell Arena\n"
                + "8\tSL_ARENA_CIRCLE_OF_LIFE\tCircle of Life Arena\n");
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
            new AuthoritativeQuestTypeDefinition(3013, 12, "The Curse of King Leoric", 3, 3, "DTYPE_CATHEDRAL", 1, false),
        ]);
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n3\t3013\n3\t3001\n",
            questTypes);

        Assert.Equal("Skeleton King's Lair", setLevels.GetDisplayName(1));
        Assert.Equal([3001U, 3013U], pools.GetPool(3).Select(entry => entry.QuestId));
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tsingle_player_only\n"
                + "3001\t0\tThe Magic Rock\t5\t-1\t\t9\t1\n",
            setLevels));
    }

    [Fact]
    public void LoadsAndValidatesWorldItemQuestAnchorCoordinates()
    {
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tsingle_player_only\tworld_item_anchor_position_x\tworld_item_anchor_position_y\n"
                + "3023\t22\tCornerstone of the World\t21\t-1\t\t0\t1\t14\t14\n");

        var definition = Assert.Single(questTypes.Definitions);
        Assert.Equal((14, 14), (
            definition.WorldItemAnchorPositionX,
            definition.WorldItemAnchorPositionY));

        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tsingle_player_only\tworld_item_anchor_position_x\tworld_item_anchor_position_y\n"
                + "3023\t22\tCornerstone of the World\t21\t-1\t\t0\t1\t14\t\n"));
    }

    [Fact]
    public void RejectsWorldItemQuestAnchorsOutsideTheirAuthoredLevel()
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 14,
                WorldItemAnchorPositionY = 14,
            },
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(21, 8, 8));

        Assert.Throws<InvalidDataException>(() => questTypes.ValidateWorldItemAnchors(
            [new AuthoritativeQuestState(3023, 21, 1)],
            world));
    }

    [Fact]
    public void RejectsWorldItemQuestAnchorsOnBlockedCells()
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 2,
                WorldItemAnchorPositionY = 2,
            },
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(21, 8, 8, blockedCells: [18]));

        Assert.Throws<InvalidDataException>(() => questTypes.ValidateWorldItemAnchors(
            [new AuthoritativeQuestState(3023, 21, 1)],
            world));
    }

    [Fact]
    public void RejectsDuplicateWorldItemQuestAnchors()
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3023,
                22,
                "Cornerstone of the World",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 2,
                WorldItemAnchorPositionY = 2,
            },
            new AuthoritativeQuestTypeDefinition(
                3024,
                21,
                "Another Anchored Quest",
                21,
                -1,
                string.Empty,
                0,
                true) {
                WorldItemAnchorPositionX = 2,
                WorldItemAnchorPositionY = 2,
            },
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(21, 8, 8));

        Assert.Throws<InvalidDataException>(() => questTypes.ValidateWorldItemAnchors(
            [
                new AuthoritativeQuestState(3023, 21, 1),
                new AuthoritativeQuestState(3024, 21, 1),
            ],
            world));
    }

    [Fact]
    public void ResolvesStableSetLevelSymbolsIncludingNativeZero()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_NONE", 0),
            new StableContentId("set_level", "SL_SKELKING", 1),
            new StableContentId("set_level", "SL_BONECHAMB", 2),
            new StableContentId("set_level", "SL_MAZE", 3),
            new StableContentId("set_level", "SL_POISONWATER", 4),
            new StableContentId("set_level", "SL_VILEBETRAYER", 5),
            new StableContentId("set_level", "SL_ARENA_CHURCH", 6),
            new StableContentId("set_level", "SL_ARENA_HELL", 7),
            new StableContentId("set_level", "SL_ARENA_CIRCLE_OF_LIFE", 8),
        ]);
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            "set_level_id\tsymbol\tdisplay_name\n"
                + "0\tSL_NONE\tnone\n"
                + "1\tSL_SKELKING\tSkeleton King's Lair\n"
                + "2\tSL_BONECHAMB\tChamber of Bone\n"
                + "3\tSL_MAZE\tMaze\n"
                + "4\tSL_POISONWATER\tPoisoned Water Supply\n"
                + "5\tSL_VILEBETRAYER\tArchbishop Lazarus' Lair\n"
                + "6\tSL_ARENA_CHURCH\tChurch Arena\n"
                + "7\tSL_ARENA_HELL\tHell Arena\n"
                + "8\tSL_ARENA_CIRCLE_OF_LIFE\tCircle of Life Arena\n",
            stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tquest_symbol\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tset_level_symbol\tsingle_player_only\n"
                + "3001\tmagic_rock\t0\tThe Magic Rock\t5\t-1\t\t0\tSL_NONE\t1\n"
                + "3013\tking_leoric\t12\tThe Curse of King Leoric\t3\t3\tDTYPE_CATHEDRAL\t1\tSL_SKELKING\t0\n",
            setLevels,
            new StableContentIdCatalog([
                new StableContentId("quest", "magic_rock", 3001),
                new StableContentId("quest", "king_leoric", 3013),
                new StableContentId("set_level", "SL_NONE", 0),
                new StableContentId("set_level", "SL_SKELKING", 1),
            ]));

        Assert.Equal(0, questTypes.Definitions[0].SetLevel);
        Assert.Equal(1, questTypes.Definitions[1].SetLevel);
        Assert.Equal(string.Empty, setLevels.GetDisplayName(0));
    }

    [Fact]
    public void RejectsQuestPoolReferencesOutsideTheQuestCatalog()
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n1\t3002\n",
            questTypes));
    }

    [Fact]
    public void SelectsNativeQuestPoolExclusionsDeterministically()
    {
        var pools = new AuthoritativeQuestPoolCatalog([
            new AuthoritativeQuestPoolEntry(1, 3001),
            new AuthoritativeQuestPoolEntry(1, 3002),
            new AuthoritativeQuestPoolEntry(2, 3003),
            new AuthoritativeQuestPoolEntry(2, 3004),
        ]);

        Assert.Equal(
            pools.SelectUnavailableQuestIds(42),
            pools.SelectUnavailableQuestIds(42));
        Assert.Equal(2, pools.SelectUnavailableQuestIds(42).Count);
        Assert.Single(pools.SelectUnavailableQuestIds(988045466), questId => questId is 3003 or 3004);
    }

    [Fact]
    public void AppliesNativePoolExclusionsToServerOwnedQuestState()
    {
        var pools = new AuthoritativeQuestPoolCatalog([
            new AuthoritativeQuestPoolEntry(1, 3001),
            new AuthoritativeQuestPoolEntry(1, 3002),
            new AuthoritativeQuestPoolEntry(2, 3003),
            new AuthoritativeQuestPoolEntry(2, 3004),
        ]);
        var quests = new[] {
            new AuthoritativeQuestState(3001, 1, 1),
            new AuthoritativeQuestState(3002, 1, 1),
            new AuthoritativeQuestState(3003, 1, 1),
            new AuthoritativeQuestState(3004, 1, 1),
            new AuthoritativeQuestState(3999, 1, 1),
        };

        AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(quests, pools, 42);

        Assert.Equal(2, quests.Count(quest => quest.Unavailable));
        Assert.False(quests.Single(quest => quest.QuestId == 3999).Unavailable);
        Assert.Equal(
            pools.SelectUnavailableQuestIds(42).OrderBy(id => id),
            quests.Where(quest => quest.Unavailable).Select(quest => quest.QuestId).OrderBy(id => id));
    }

    [Fact]
    public void SelectsNativeTsvRowsAgainstLiteralNativeSeedVectorsAndKeepsPublicViewsSorted()
    {
        var shipped = ReadShippedQuestMetadata();
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds,
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);

        var expectedNativeQuestTypeIds = new (uint NativeQuestId, uint StableQuestId)[] {
            (0, 3001), (1, 3002), (2, 3003), (3, 3004), (4, 3005), (5, 3006), (6, 3007),
            (7, 3008), (8, 3009), (9, 3010), (10, 3011), (11, 3012), (12, 3013), (13, 3014),
        };
        Assert.Equal(
            expectedNativeQuestTypeIds,
            shipped.QuestTypes.Definitions
                .Where(definition => definition.NativeQuestId <= 13U)
                .OrderBy(definition => definition.NativeQuestId)
                .Select(definition => (definition.NativeQuestId, definition.QuestId)));

        var poolRows = TsvTable.Parse("quest_pools.tsv", shipped.QuestPoolsText).Rows;
        var sourceOrderedEntries = new List<AuthoritativeQuestPoolEntry>(poolRows.Count);
        foreach (var row in poolRows) {
            var stableQuestId = shipped.StableIds.Resolve("quest", row.Required("quest_symbol"));
            Assert.Equal(row.RequiredUInt32("quest_id"), stableQuestId);
            sourceOrderedEntries.Add(new AuthoritativeQuestPoolEntry(row.RequiredInt32("pool_id"), stableQuestId));
        }
        var constructorPools = new AuthoritativeQuestPoolCatalog(
            sourceOrderedEntries,
            shipped.QuestTypes,
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);
        AssertPublicPoolViewsAreSorted(constructorPools);
        Assert.Equal(
            new (int PoolId, uint QuestId)[] {
                (1, 3013), (1, 3014),
                (2, 3003), (2, 3007), (2, 3008),
                (3, 3001), (3, 3009), (3, 3010),
                (4, 3002), (4, 3004), (4, 3011),
                (5, 3005), (5, 3012),
            },
            pools.Definitions.Select(entry => (entry.PoolId, entry.QuestId)));
        Assert.Equal(new uint[] { 3013, 3014 }, pools.GetPool(1).Select(entry => entry.QuestId));
        Assert.Equal(new uint[] { 3003, 3007, 3008 }, pools.GetPool(2).Select(entry => entry.QuestId));
        Assert.Equal(new uint[] { 3001, 3009, 3010 }, pools.GetPool(3).Select(entry => entry.QuestId));
        Assert.Equal(new uint[] { 3002, 3004, 3011 }, pools.GetPool(4).Select(entry => entry.QuestId));
        Assert.Equal(new uint[] { 3005, 3012 }, pools.GetPool(5).Select(entry => entry.QuestId));

        // These expected IDs are independently translated native seed vectors, not outputs
        // generated by the managed selector under test.
        foreach (var vector in NativeSeedVectors) {
            Assert.Equal(vector.QuestIds, pools.SelectUnavailableQuestIds(vector.Seed));
            Assert.Equal(vector.QuestIds, constructorPools.SelectUnavailableQuestIds(vector.Seed));
        }
    }

    [Fact]
    public void ExistingOverloadsRetainLegacyQuestIdSortedSelectionAndExplicitLegacyPolicy()
    {
        var shipped = ReadShippedQuestMetadata();
        var defaultLoader = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds);
        var explicitLegacyLoader = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds,
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1);
        var sourceOrderedEntries = new[] {
            new AuthoritativeQuestPoolEntry(1, 3013),
            new AuthoritativeQuestPoolEntry(1, 3014),
            new AuthoritativeQuestPoolEntry(2, 3007),
            new AuthoritativeQuestPoolEntry(2, 3008),
            new AuthoritativeQuestPoolEntry(2, 3003),
            new AuthoritativeQuestPoolEntry(3, 3009),
            new AuthoritativeQuestPoolEntry(3, 3001),
            new AuthoritativeQuestPoolEntry(3, 3010),
            new AuthoritativeQuestPoolEntry(4, 3002),
            new AuthoritativeQuestPoolEntry(4, 3004),
            new AuthoritativeQuestPoolEntry(4, 3011),
            new AuthoritativeQuestPoolEntry(5, 3005),
            new AuthoritativeQuestPoolEntry(5, 3012),
        };
        var defaultConstructor = new AuthoritativeQuestPoolCatalog(
            sourceOrderedEntries,
            shipped.QuestTypes);
        var explicitLegacyConstructor = new AuthoritativeQuestPoolCatalog(
            sourceOrderedEntries,
            shipped.QuestTypes,
            AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1);

        AssertPublicPoolViewsAreSorted(defaultLoader);
        AssertPublicPoolViewsAreSorted(explicitLegacyLoader);
        AssertPublicPoolViewsAreSorted(defaultConstructor);
        AssertPublicPoolViewsAreSorted(explicitLegacyConstructor);

        foreach (var vector in LegacySeedVectors) {
            Assert.Equal(vector.QuestIds, defaultLoader.SelectUnavailableQuestIds(vector.Seed));
            Assert.Equal(vector.QuestIds, explicitLegacyLoader.SelectUnavailableQuestIds(vector.Seed));
            Assert.Equal(vector.QuestIds, defaultConstructor.SelectUnavailableQuestIds(vector.Seed));
            Assert.Equal(vector.QuestIds, explicitLegacyConstructor.SelectUnavailableQuestIds(vector.Seed));
        }
    }

    [Fact]
    public void NativeSelectionPreservesInterleavedRowsAndMaterializesCallerInputOnce()
    {
        var sourceRows = new List<AuthoritativeQuestPoolEntry> {
            new(2, 3004),
            new(1, 3002),
            new(2, 3003),
            new(1, 3001),
        };
        var enumerationCount = 0;
        IEnumerable<AuthoritativeQuestPoolEntry> EnumerateRows()
        {
            enumerationCount++;
            foreach (var row in sourceRows)
                yield return row;
        }

        var pools = new AuthoritativeQuestPoolCatalog(
            EnumerateRows(),
            questTypes: null,
            selectionOrder: AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);

        Assert.Equal(1, enumerationCount);
        Assert.Equal(
            new (int PoolId, uint QuestId)[] { (1, 3001), (1, 3002), (2, 3003), (2, 3004) },
            pools.Definitions.Select(entry => (entry.PoolId, entry.QuestId)));
        Assert.Equal(new uint[] { 3002, 3004 }, pools.SelectUnavailableQuestIds(0));

        sourceRows.Clear();
        sourceRows.Add(new AuthoritativeQuestPoolEntry(1, 3999));

        Assert.Equal(1, enumerationCount);
        Assert.Equal(new uint[] { 3002, 3004 }, pools.SelectUnavailableQuestIds(0));
        Assert.Equal(
            new uint[] { 3001, 3002, 3003, 3004 },
            pools.Definitions.Select(entry => entry.QuestId));
    }

    [Theory]
    [InlineData(AuthoritativeQuestPoolSelectionOrder.LegacyQuestIdSortedV1)]
    [InlineData(AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1)]
    public void BothSelectionPoliciesRetainCatalogValidation(
        AuthoritativeQuestPoolSelectionOrder selectionOrder)
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeQuestPoolCatalog(
            Array.Empty<AuthoritativeQuestPoolEntry>(), questTypes, selectionOrder));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeQuestPoolCatalog(
            [new AuthoritativeQuestPoolEntry(0, 3001)], questTypes, selectionOrder));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeQuestPoolCatalog(
            [new AuthoritativeQuestPoolEntry(1, 3001), new AuthoritativeQuestPoolEntry(1, 3001)],
            questTypes,
            selectionOrder));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeQuestPoolCatalog(
            [new AuthoritativeQuestPoolEntry(1, 3002)], questTypes, selectionOrder));
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n1\t3002\n",
            questTypes,
            stableIds: null,
            selectionOrder: selectionOrder));
    }

    [Fact]
    public void RejectsUndefinedQuestPoolSelectionPolicyAtBothEntryPoints()
    {
        var undefinedOrder = (AuthoritativeQuestPoolSelectionOrder)12345;
        var validRows = new[] { new AuthoritativeQuestPoolEntry(1, 3001) };
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthoritativeQuestPoolCatalog(
            validRows,
            questTypes: null,
            selectionOrder: undefinedOrder));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n1\t3001\n",
            questTypes: null,
            stableIds: null,
            selectionOrder: undefinedOrder));
    }

    [Fact]
    public void AppliesNativeTsvPoolVectorsToShippedQuestStates()
    {
        var shipped = ReadShippedQuestMetadata();
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds,
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);
        var contentRoot = FindBaseContentDirectory();
        var stateText = File.ReadAllText(Path.Combine(contentRoot, "quest_states.tsv"));
        var poolQuestIds = pools.Definitions.Select(entry => entry.QuestId).ToHashSet();
        // Literal native vectors pin selected IDs and ordering; assertions below cover managed state application.
        Assert.DoesNotContain(3006U, poolQuestIds);
        Assert.DoesNotContain(3016U, poolQuestIds);

        foreach (var vector in NativeSeedVectors) {
            var quests = AuthoritativeQuestCatalog.LoadTsv(
                "quest_states.tsv",
                stateText,
                shipped.QuestTypes,
                shipped.StableIds);
            Assert.Equal(
                new uint[] { 3003U, 3004U, 3006U, 3007U, 3008U, 3012U, 3013U, 3014U, 3016U },
                quests.Select(quest => quest.QuestId).OrderBy(questId => questId));
            var initial = quests.ToDictionary(
                quest => quest.QuestId,
                quest => (Completed: quest.Completed, Active: quest.Active, Logged: quest.Logged));
            var stateQuestIds = quests.Select(quest => quest.QuestId).ToHashSet();
            var expectedUnavailableStateIds = vector.QuestIds
                .Where(stateQuestIds.Contains)
                .OrderBy(questId => questId)
                .ToArray();

            AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(quests, pools, vector.Seed);

            Assert.Equal(
                expectedUnavailableStateIds,
                quests.Where(quest => quest.Unavailable).Select(quest => quest.QuestId).OrderBy(questId => questId));
            foreach (var quest in quests) {
                var before = initial[quest.QuestId];
                Assert.Equal(before.Completed, quest.Completed);
                var expectedUnavailable = !before.Completed && vector.QuestIds.Contains(quest.QuestId);
                Assert.Equal(expectedUnavailable, quest.Unavailable);
                if (expectedUnavailable) {
                    Assert.False(quest.Active);
                    Assert.False(quest.Logged);
                } else {
                    Assert.Equal(before.Active, quest.Active);
                    Assert.Equal(before.Logged, quest.Logged);
                }
            }

            Assert.Contains(quests, quest => !poolQuestIds.Contains(quest.QuestId));
            foreach (var nonPoolQuest in quests.Where(quest => !poolQuestIds.Contains(quest.QuestId))) {
                var before = initial[nonPoolQuest.QuestId];
                Assert.False(nonPoolQuest.Unavailable);
                Assert.Equal(before.Active, nonPoolQuest.Active);
                Assert.Equal(before.Logged, nonPoolQuest.Logged);
            }
        }
    }

    [Fact]
    public void ClearsActiveAndLoggedFlagsForExcludedShippedQuest()
    {
        var shipped = ReadShippedQuestMetadata();
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds,
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);
        var contentRoot = FindBaseContentDirectory();
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "quest_states.tsv")),
            shipped.QuestTypes,
            shipped.StableIds);
        var excludedQuest = Assert.Single(quests, quest => quest.QuestId == 3013U);
        var nativeVector = NativeSeedVectors.Single(vector => vector.Seed == 0);

        // The literal native vector pins the selected quest; this setup exercises managed flag clearing.
        Assert.Contains(excludedQuest.QuestId, nativeVector.QuestIds);
        Assert.False(excludedQuest.Completed);
        Assert.False(excludedQuest.Active);
        // The shipped initial row loads inactive; setters exercise clearing both flags.
        excludedQuest.Active = true;
        excludedQuest.Logged = true;

        AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(quests, pools, nativeVector.Seed);

        Assert.True(excludedQuest.Unavailable);
        Assert.False(excludedQuest.Active);
        Assert.False(excludedQuest.Logged);
    }
    [Fact]
    public void CompletedSelectedShippedQuestRetainsAvailabilityAndActivityFlags()
    {
        var shipped = ReadShippedQuestMetadata();
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            shipped.QuestPoolsPath,
            shipped.QuestPoolsText,
            shipped.QuestTypes,
            shipped.StableIds,
            AuthoritativeQuestPoolSelectionOrder.NativeTsvRowsV1);
        var contentRoot = FindBaseContentDirectory();
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "quest_states.tsv")),
            shipped.QuestTypes,
            shipped.StableIds);
        var completedQuest = Assert.Single(quests, quest => quest.QuestId == 3013U);

        Assert.Contains(3013U, NativeSeedVectors.Single(vector => vector.Seed == 0).QuestIds);
        Assert.False(completedQuest.Completed);
        // The constructor normalizes Active=false for completed quests.
        // These setters deliberately exercise the managed completed=true, Active=true edge.
        completedQuest.Completed = true;
        completedQuest.Active = true;
        completedQuest.Logged = true;

        AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(quests, pools, 0);

        Assert.True(completedQuest.Completed);
        Assert.False(completedQuest.Unavailable);
        Assert.True(completedQuest.Active);
        Assert.True(completedQuest.Logged);
    }

    private static readonly SeedVector[] NativeSeedVectors = [
        new(0, [3013, 3008, 3009, 3011, 3005]),
        new(1, [3013, 3007, 3010, 3004, 3012]),
        new(1457187811, [3013, 3008, 3009, 3004, 3005]),
        new(988045466, [3007, 3001, 3002, 3005]),
        new(4203210069, [3013, 3008, 3009, 3004, 3012]),
        new(2557708932, [3013, 3008, 3001, 3002, 3012]),
        new(1272442071, [3013, 3003, 3001, 3004, 3005]),
        new(509604, [3014, 3003, 3001, 3004, 3005]),
    ];

    private static readonly SeedVector[] LegacySeedVectors = [
        new(1457187811, [3013, 3007, 3001, 3004, 3005]),
        new(988045466, [3003, 3009, 3002, 3005]),
        new(4203210069, [3013, 3007, 3001, 3004, 3012]),
        new(2557708932, [3013, 3007, 3009, 3002, 3012]),
        new(1272442071, [3013, 3008, 3009, 3004, 3005]),
        new(509604, [3014, 3008, 3009, 3004, 3005]),
        new(0, [3013, 3007, 3001, 3011, 3005]),
        new(1, [3013, 3003, 3010, 3004, 3012]),
    ];

    private static void AssertPublicPoolViewsAreSorted(AuthoritativeQuestPoolCatalog pools)
    {
        var expected = new (int PoolId, uint QuestId)[] {
            (1, 3013), (1, 3014),
            (2, 3003), (2, 3007), (2, 3008),
            (3, 3001), (3, 3009), (3, 3010),
            (4, 3002), (4, 3004), (4, 3011),
            (5, 3005), (5, 3012),
        };
        Assert.Equal(expected, pools.Definitions.Select(entry => (entry.PoolId, entry.QuestId)));
        foreach (var poolId in Enumerable.Range(1, 5)) {
            Assert.Equal(
                expected.Where(entry => entry.PoolId == poolId).Select(entry => entry.QuestId),
                pools.GetPool(poolId).Select(entry => entry.QuestId));
        }
    }
    private static (AuthoritativeQuestTypeCatalog QuestTypes, StableContentIdCatalog StableIds, string QuestPoolsPath, string QuestPoolsText)
        ReadShippedQuestMetadata()
    {
        var contentRoot = FindBaseContentDirectory();
        var stableIdsPath = Path.Combine(contentRoot, "content_ids.tsv");
        var setLevelsPath = Path.Combine(contentRoot, "set_level_names.tsv");
        var questTypesPath = Path.Combine(contentRoot, "quest_types.tsv");
        var questPoolsPath = Path.Combine(contentRoot, "quest_pools.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv("content_ids.tsv", File.ReadAllText(stableIdsPath));
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            File.ReadAllText(setLevelsPath),
            stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            File.ReadAllText(questTypesPath),
            setLevels,
            stableIds);
        return (questTypes, stableIds, "quest_pools.tsv", File.ReadAllText(questPoolsPath));
    }

    private static string FindBaseContentDirectory()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent) {
                var candidate = Path.Combine(directory.FullName, "server", "content", "base");
                if (File.Exists(Path.Combine(candidate, "quest_pools.tsv")))
                    return candidate;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository's base content directory.");
    }

    private sealed record SeedVector(uint Seed, uint[] QuestIds);
}

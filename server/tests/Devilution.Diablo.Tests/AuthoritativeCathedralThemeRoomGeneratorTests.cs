using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable contract tests for detached CreateThemeRooms state ownership. */
public sealed class AuthoritativeCathedralThemeRoomGeneratorTests
{
    [Fact]
    public void EmptyOrderedThemePassDetachesEveryNativeChannel()
    {
        var state = State(corpsesValue: 37, staleObjectValue: 901, staleMonsterValue: 902);
        var request = Request(state, []) with {
            InitialThemeVar1 = 2,
            InitialThemeOrigin = new(29, 31),
        };

        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(request);

        Assert.Equal(request.RandomState, result.FinalRandomState);
        Assert.Empty(result.ThemeOrigins);
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(29, 31), result.FinalThemeOrigin);
        Assert.Equal(2, result.FinalThemeVar1);
        Assert.Equal(37, result.State.Corpses[0]);
        Assert.Equal(901, result.State.ObjectSlots[127 - 1].Var4);
        Assert.Equal(902, result.State.MonsterSlots[199].Var2);
        Assert.Equal(0, result.State.ActiveObjectCount);
        Assert.Equal(0, result.State.ActiveMonsterCount);
        Assert.Equal(0, result.State.ActiveItemCount);
        Assert.NotSame(state.ObjectSlots, result.State.ObjectSlots);
        Assert.NotSame(state.Corpses, result.State.Corpses);
    }

    [Fact]
    public void UnsupportedItemPoliciesAreRejectedBeforeAnEmptyThemePass()
    {
        var policies = new[] {
            new AuthoritativeCathedralThemeItemPolicy(0),
            new AuthoritativeCathedralThemeItemPolicy(5),
            new AuthoritativeCathedralThemeItemPolicy(1, GameMode: DiabloGameMode.Multiplayer),
            new AuthoritativeCathedralThemeItemPolicy(1, Difficulty: DiabloDifficulty.Nightmare),
            new AuthoritativeCathedralThemeItemPolicy(1, Hellfire: true),
            new AuthoritativeCathedralThemeItemPolicy(1, Spawn: true),
            new AuthoritativeCathedralThemeItemPolicy(1, QuestEnabled: true),
            new AuthoritativeCathedralThemeItemPolicy(1, SetLevel: true),
            new AuthoritativeCathedralThemeItemPolicy(1, LegacyLevelItemGenerationAllowed: false),
            new AuthoritativeCathedralThemeItemPolicy(1, CurrentDungeonLevel: 2),
        };

        foreach (var policy in policies) {
            var refs = new int[112 * 112];
            refs[0] = 61;
            var state = State(objectRefs: refs);
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(
                Request(state, []) with { ItemPolicy = policy }));
            Assert.Equal(61, state.ObjectRefs[0]);
            Assert.Equal(0, state.ActiveObjectCount);
        }
    }

    [Fact]
    public void OrdinaryThemeIdsAreTheOnlyAcceptedRoomSelectors()
    {
        foreach (var unsupported in new[] { 10, 12, 16 }) {
            var request = Request(State(), [new AuthoritativeCathedralThemeEntry(unsupported, 0)]);
            Assert.Throws<InvalidDataException>(
                () => AuthoritativeCathedralThemeRoomGenerator.Generate(request));
        }

        var unsupportedNegative = Request(State(), [new AuthoritativeCathedralThemeEntry(-1, 0)]);
        Assert.Throws<InvalidDataException>(
            () => AuthoritativeCathedralThemeRoomGenerator.Generate(unsupportedNegative));
    }

    [Fact]
    public void ActiveAndAvailableObjectPrefixesAreValidatedWithoutNormalizingTails()
    {
        var active = Enumerable.Range(0, 127).ToArray();
        active[0] = 126;
        active[126] = 126;
        var available = Enumerable.Range(0, 127).ToArray();
        available[126] = 0;
        var state = State(activeObjectIds: active, availableObjectIds: available, activeObjectCount: 1);

        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(state, []));

        Assert.Equal(active, result.State.ActiveObjectIds);
        Assert.Equal(0, result.State.AvailableObjectIds[126]);
    }

    [Fact]
    public void SignedNativeChannelsAndPetrifiedProjectionAreRetainedWhenUntouched()
    {
        var monsterPetrified = Enumerable.Repeat(true, 200).ToArray();
        var playerRefs = new int[112 * 112];
        var monsterRefs = new int[112 * 112];
        var objectRefs = new int[112 * 112];
        playerRefs[0] = sbyte.MinValue;
        monsterRefs[1] = short.MinValue;
        objectRefs[2] = -127;
        var state = State(
            playerRefs: playerRefs,
            monsterRefs: monsterRefs,
            objectRefs: objectRefs,
            monsterPetrified: monsterPetrified);

        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(state, []));

        Assert.Equal(sbyte.MinValue, result.State.PlayerRefs[0]);
        Assert.Equal(short.MinValue, result.State.MonsterRefs[1]);
        Assert.Equal(-127, result.State.ObjectRefs[2]);
        Assert.All(result.State.MonsterPetrified, value => Assert.True(value));
    }

    [Fact]
    public void CatalogRejectsPartialOrOutOfOrderRawObjectTables()
    {
        var objects = Enumerable.Range(0, 108)
            .Select(index => new AuthoritativeCathedralPopulationObjectDefinition(index, 0, 0, 0, 0, 0))
            .ToArray();
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralThemeRoomCatalog(
            objects, Bestiary(), Enumerable.Repeat(true, 26).ToArray()));

        var ordered = Enumerable.Range(0, 109)
            .Select(index => new AuthoritativeCathedralPopulationObjectDefinition(index, 0, 0, 0, 0, 0))
            .ToArray();
        ordered[1] = ordered[1] with { NativeType = 2 };
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralThemeRoomCatalog(
            ordered, Bestiary(), Enumerable.Repeat(true, 26).ToArray()));
    }

    [Fact]
    public void NativeClassifierAndPlayerContextBoundariesAreValidated()
    {
        var invalidMonster = new AuthoritativeCathedralThemeRoomMonsterDefinition(
            new AuthoritativeCathedralPopulationMonsterDefinition(
                0, true, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0,
                0, 0, 0, 0, 0, 0, new int[6], Enumerable.Repeat(1, 6).ToArray()), 0, false);
        Assert.Throws<InvalidDataException>(() => CatalogWithBestiary([invalidMonster]));

        var player = new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(4, 4), new(4, 4));
        var duplicatePlayers = new[] { player, player with { Active = false } };
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(
            Request(State(), [], PlayerContexts: duplicatePlayers)));
    }

    [Fact]
    public void CatalogRetainsTheNativePlaceFlagsByteBoundaryAndRejectsOverflow()
    {
        var boundary = Bestiary()[0] with { PlaceFlags = byte.MaxValue };
        CatalogWithBestiary([boundary]);

        var overflow = boundary with { PlaceFlags = byte.MaxValue + 1U };
        Assert.Throws<InvalidDataException>(() => CatalogWithBestiary([overflow]));
    }

    [Fact]
    public void RetainedSpeechFieldsAcceptSignedShortEndpointsAndRejectOverflow()
    {
        var objects = Enumerable.Range(0, 127)
            .Select(index => new AuthoritativeCathedralPopulationObjectState { Slot = index, BookMessage = index == 0 ? short.MinValue : -1 })
            .ToArray();
        State(objectSlots: objects);
        objects[0] = objects[0] with { BookMessage = short.MaxValue + 1 };
        Assert.Throws<InvalidDataException>(() => State(objectSlots: objects));

        var monsters = Enumerable.Range(0, 200)
            .Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index, TalkMessage = index == 0 ? short.MaxValue : 0 })
            .ToArray();
        State(monsterSlots: monsters);
        monsters[0] = monsters[0] with { TalkMessage = short.MinValue - 1 };
        Assert.Throws<InvalidDataException>(() => State(monsterSlots: monsters));
    }

    [Fact]
    public void FallbackTargetPlayerIdIsSignedRangeCheckedWithoutMutatingState()
    {
        foreach (var id in new[] { -1, 4 }) {
            var refs = new int[112 * 112];
            refs[0] = 37;
            var state = State(objectRefs: refs);
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
                state,
                [],
                targetPlayer: new(id, 1, false, false, new(0, 0), new(0, 0)))));
            Assert.Equal(37, state.ObjectRefs[0]);
            Assert.Equal(0, state.ActiveObjectCount);
        }
    }

    [Fact]
    public void CheckObj5ReachedOutOfBoundsReadFailsClosedAndLeavesInputUntouched()
    {
        var refs = new int[112 * 112];
        refs[0] = 41;
        var state = State(objectRefs: refs, staleObjectValue: 901);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            state, [new AuthoritativeCathedralThemeEntry(7, 0)])));

        Assert.Equal(41, state.ObjectRefs[0]);
        Assert.Equal(901, state.ObjectSlots[126].Var4);
        Assert.Equal(0, state.ActiveObjectCount);
    }

    [Fact]
    public void FitShrineReachedOutOfBoundsReadFailsClosedAndLeavesInputUntouched()
    {
        var refs = new int[112 * 112];
        refs[0] = 43;
        var state = State(objectRefs: refs, staleObjectValue: 902);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            state, [new AuthoritativeCathedralThemeEntry(1, 0)])));

        Assert.Equal(43, state.ObjectRefs[0]);
        Assert.Equal(902, state.ObjectSlots[126].Var4);
        Assert.Equal(0, state.ActiveObjectCount);
    }

    [Fact]
    public void GoatShrineReachedOutOfBoundsReadFailsClosedAndLeavesInputUntouched()
    {
        var refs = new int[112 * 112];
        refs[0] = 47;
        var state = State(objectRefs: refs, staleObjectValue: 903);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            state, [new AuthoritativeCathedralThemeEntry(11, 0)])));

        Assert.Equal(47, state.ObjectRefs[0]);
        Assert.Equal(903, state.ObjectSlots[126].Var4);
        Assert.Equal(0, state.ActiveObjectCount);
    }

    [Fact]
    public void SameRegionReachedOutOfBoundsPlayerTransparencyFailsClosedAndLeavesInputUntouched()
    {
        var refs = new int[112 * 112];
        refs[0] = 53;
        var state = State(objectRefs: refs, solidBoundary: true);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            state,
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true),
            targetPlayer: new(0, 1, true, false, new(112, 0), new(112, 0)))));

        Assert.Equal(53, state.ObjectRefs[0]);
        Assert.Equal(0, state.ActiveObjectCount);
        Assert.Equal(0, state.ActiveMonsterCount);
    }

    [Fact]
    public void SameRegionReachedOutOfBoundsMonsterTransparencyFailsClosedAndLeavesInputUntouched()
    {
        var monsters = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index }).ToArray();
        monsters[0] = new() {
            Slot = 0, Type = 109, Tile = new(112, 0), Future = new(112, 0), HitPoints = 640,
            Flags = 1U << 5, TalkMessage = -1,
        };
        var refs = new int[112 * 112];
        refs[0] = 59;
        var state = State(monsterSlots: monsters, activeMonsterCount: 1, monsterRefs: refs, solidBoundary: true);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            state,
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true, abilityFlags: (1U << 5) | (1U << 11)))));

        Assert.Equal(new AuthoritativeCathedralPopulationPoint(112, 0), state.MonsterSlots[0].Tile);
        Assert.Equal(59, state.MonsterRefs[0]);
        Assert.Equal(1, state.ActiveMonsterCount);
    }

    [Fact]
    public void RetainedNativeWidthsAreValidatedForInactiveSlots()
    {
        var invalidMonster = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index }).ToArray();
        invalidMonster[199] = invalidMonster[199] with { GoalVar2 = 128 };
        Assert.Throws<InvalidDataException>(() => State(monsterSlots: invalidMonster));

        var invalidObject = Enumerable.Range(0, 127).Select(index => new AuthoritativeCathedralPopulationObjectState { Slot = index }).ToArray();
        invalidObject[126] = invalidObject[126] with { BreakFlag = 128 };
        Assert.Throws<InvalidDataException>(() => State(objectSlots: invalidObject));
    }

    [Fact]
    public void SkeletonRoomUsesOrderedSkeletonRowsAndNoSkeletonIsBounded()
    {
        var request = Request(
            State(solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(includeScatter: false, skeleton: true));
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(request);
        Assert.Contains(result.State.ObjectSlots, value => value.Type == 3);
        Assert.Equal(0, result.ThemeVars[0]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(includeScatter: false, skeleton: false))));
    }

    [Fact]
    public void ShrineSelectionUsesRawTwentySixEntryAvailability()
    {
        var shrineAvailability = Enumerable.Repeat(true, 26).ToArray();
        shrineAvailability[18] = false;
        shrineAvailability[25] = false;
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(shrineFit: true),
            [new AuthoritativeCathedralThemeEntry(1, 0)],
            Catalog(includeScatter: true, shrineAvailability: shrineAvailability)) with {
                InitialThemeOrigin = new(20, 20),
            });

        var shrine = Assert.Single(result.State.ObjectSlots, value => value.Type is 59 or 60);
        Assert.DoesNotContain(shrine.Var1, new[] { 18, 25 });
    }

    [Theory]
    [InlineData(34u)]
    [InlineData(9u)]
    public void ShrineRetrySeedsNeverSelectDisabledNativeEntries(uint seed)
    {
        var shrineAvailability = Enumerable.Repeat(true, 26).ToArray();
        shrineAvailability[18] = false;
        shrineAvailability[25] = false;
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(shrineFit: true),
            [new AuthoritativeCathedralThemeEntry(1, 0)],
            Catalog(includeScatter: true, shrineAvailability: shrineAvailability),
            seed));

        var shrine = Assert.Single(result.State.ObjectSlots, value => value.Type is 59 or 60);
        Assert.DoesNotContain(shrine.Var1, new[] { 18, 25 });
    }

    [Fact]
    public void UpdateEnemyPrefersPlayerOnEqualDistanceAndSkipsHoldingCell()
    {
        var monsters = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index }).ToArray();
        monsters[0] = new() {
            Slot = 0, Type = 109, Tile = new(2, 0), Future = new(2, 0), HitPoints = 640,
            Flags = 1U << 5, TalkMessage = -1,
        };
        var refs = new int[112 * 112];
        refs[2] = 1;
        var player = new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(2, 0), new(2, 0));
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(monsterSlots: monsters, activeMonsterCount: 1, monsterRefs: refs, solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true),
            targetPlayer: player));

        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8 && monster.Enemy == 0 && (monster.Flags & (1U << 4)) == 0);

        monsters[0] = monsters[0] with { Tile = new(1, 0), Future = new(1, 0) };
        refs = new int[112 * 112];
        refs[1] = 1;
        result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(monsterSlots: monsters, activeMonsterCount: 1, monsterRefs: refs, solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true)));
        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8 && (monster.Flags & (1U << 10)) != 0);
    }

    [Fact]
    public void UpdateEnemyPrefersAFartherSameRegionPlayer()
    {
        var monsters = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index }).ToArray();
        monsters[0] = new() {
            Slot = 0, Type = 109, Tile = new(0, 1), Future = new(0, 1), HitPoints = 640,
            Flags = 1U << 5, TalkMessage = -1,
        };
        var refs = new int[112 * 112];
        refs[112] = 1;
        var transparency = new sbyte[112 * 112];
        transparency[112] = 1;
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(monsterSlots: monsters, activeMonsterCount: 1, monsterRefs: refs, transparency: transparency, solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true),
            targetPlayer: new(0, 1, true, false, new(2, 0), new(2, 0))));

        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8
            && monster.Enemy == 0
            && (monster.Flags & (1U << 4)) == 0
            && monster.EnemyPosition == new AuthoritativeCathedralPopulationPoint(2, 0));
    }

    [Fact]
    public void UpdateEnemyAcceptsNonTalkerMessageAndTargetsTheGolem()
    {
        var monsters = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState { Slot = index }).ToArray();
        monsters[0] = new() {
            Slot = 0, Type = 109, Tile = new(0, 1), Future = new(0, 1), HitPoints = 640,
            Flags = 1U << 5, TalkMessage = 123,
        };
        var refs = new int[112 * 112];
        refs[112] = 1;
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(monsterSlots: monsters, activeMonsterCount: 1, monsterRefs: refs, solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true)));

        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8
            && monster.Enemy == 0
            && (monster.Flags & (1U << 4)) != 0
            && monster.EnemyPosition == new AuthoritativeCathedralPopulationPoint(0, 1));
    }

    [Fact]
    public void UpdateEnemyOrdersEqualPlayerContextTiesByNativePlayerId()
    {
        var playerOne = new AuthoritativeCathedralPopulationPlayer(1, 1, true, false, new(4, 4), new(4, 4));
        var playerZero = new AuthoritativeCathedralPopulationPlayer(0, 1, true, false, new(4, 4), new(4, 4));
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true),
            PlayerContexts: [playerOne, playerZero]));

        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8
            && monster.Enemy == 0
            && (monster.Flags & (1U << 4)) == 0
            && monster.EnemyPosition == new AuthoritativeCathedralPopulationPoint(4, 4));
    }

    [Fact]
    public void BerserkGolemCanSelectAnEligiblePlayer()
    {
        var result = AuthoritativeCathedralThemeRoomGenerator.Generate(Request(
            State(solidBoundary: true),
            [new AuthoritativeCathedralThemeEntry(3, 0)],
            Catalog(skeleton: true, abilityFlags: (1U << 5) | (1U << 11)),
            targetPlayer: new(0, 1, true, false, new(2, 0), new(2, 0))));

        Assert.Contains(result.State.MonsterSlots, monster => monster.Type == 8 && monster.Enemy == 0 && (monster.Flags & (1U << 4)) == 0);
    }

    private static AuthoritativeCathedralThemeRoomRequest Request(
        AuthoritativeCathedralThemeRoomState state,
        IReadOnlyList<AuthoritativeCathedralThemeEntry> themes,
        AuthoritativeCathedralThemeRoomCatalog? catalog = null,
        uint randomState = 0,
        IReadOnlyList<AuthoritativeCathedralPopulationPlayer>? PlayerContexts = null,
        AuthoritativeCathedralPopulationPlayer? targetPlayer = null)
        => new(state, catalog ?? Catalog(), ItemCatalog(), new(1), themes, randomState,
            targetPlayer ?? new(0, 1, false, false, new(0, 0), new(0, 0)), PlayerContexts: PlayerContexts);

    private static AuthoritativeCathedralThemeRoomState State(
        int corpsesValue = 0,
        int staleObjectValue = 0,
        int staleMonsterValue = 0,
        IReadOnlyList<int>? activeObjectIds = null,
        IReadOnlyList<int>? availableObjectIds = null,
        IReadOnlyList<int>? playerRefs = null,
        IReadOnlyList<int>? monsterRefs = null,
        IReadOnlyList<int>? objectRefs = null,
        IReadOnlyList<bool>? monsterPetrified = null,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectState>? objectSlots = null,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterState>? monsterSlots = null,
        IReadOnlyList<int>? activeMonsterIds = null,
        int activeMonsterCount = 0,
        IReadOnlyList<sbyte>? transparency = null,
        int activeObjectCount = 0,
        bool shrineFit = false,
        bool solidBoundary = false)
    {
        var objects = Enumerable.Range(0, 127).Select(index => new AuthoritativeCathedralPopulationObjectState {
            Slot = index,
            Type = -1,
            Var4 = staleObjectValue,
        }).ToArray();
        var monsters = Enumerable.Range(0, 200).Select(index => new AuthoritativeCathedralPopulationMonsterState {
            Slot = index,
            Var2 = staleMonsterValue,
        }).ToArray();
        var cells = 112 * 112;
        var pieces = new ushort[cells];
        var pieceProperties = new byte[1379];
        var transparencyValues = transparency?.ToArray() ?? new sbyte[cells];
        if (shrineFit) {
            pieceProperties[1] = 128;
            pieces[112 + 2] = 1;
            for (var i = 0; i < 112; i++) {
                transparencyValues[i] = 1;
                transparencyValues[(111 * 112) + i] = 1;
                transparencyValues[i * 112] = 1;
                transparencyValues[(i * 112) + 111] = 1;
            }
        }
        if (solidBoundary) {
            pieceProperties[1] = 1;
            for (var offset = 0; offset < 2; offset++) {
                for (var i = 0; i < 112; i++) {
                    pieces[offset * 112 + i] = 1;
                    pieces[(111 - offset) * 112 + i] = 1;
                    pieces[i * 112 + offset] = 1;
                    pieces[i * 112 + (111 - offset)] = 1;
                }
            }
        }
        return new(
            objectSlots ?? objects,
            activeObjectIds ?? Enumerable.Range(0, 127).ToArray(),
            availableObjectIds ?? Enumerable.Range(0, 127).ToArray(),
            activeObjectCount,
            monsterSlots ?? monsters,
            activeMonsterIds ?? Enumerable.Range(0, 200).ToArray(),
            activeMonsterCount,
            0,
            monsterRefs ?? new int[cells],
            playerRefs ?? new int[cells],
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(),
            0,
            new int[cells],
            new HashSet<int>(),
            pieces,
            transparencyValues,
            new byte[cells],
            pieceProperties,
            new byte[cells],
            objectRefs ?? new int[cells],
            new byte[cells],
            new byte[cells],
            Enumerable.Range(0, 32).ToArray(),
            0,
            Enumerable.Range(0, 32).Select(index => new AuthoritativeCathedralEncounterLightState(
                index, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray(),
            false,
            false,
            null,
            Enumerable.Repeat((byte)corpsesValue, cells).ToArray(),
            monsterPetrified ?? new bool[200]);
    }

    private static AuthoritativeCathedralThemeRoomCatalog Catalog(
        bool includeScatter = false,
        IReadOnlyList<bool>? shrineAvailability = null,
        bool skeleton = false,
        uint abilityFlags = 0)
        => new(
            Enumerable.Range(0, 109).Select(index => new AuthoritativeCathedralPopulationObjectDefinition(
                index, 0, 0, 0, 1, 0)).ToArray(),
            Bestiary(includeScatter, skeleton, abilityFlags),
            shrineAvailability ?? Enumerable.Repeat(true, 26).ToArray());

    private static AuthoritativeCathedralThemeRoomCatalog CatalogWithBestiary(
        IReadOnlyList<AuthoritativeCathedralThemeRoomMonsterDefinition> bestiary)
        => new(
            Enumerable.Range(0, 109).Select(index => new AuthoritativeCathedralPopulationObjectDefinition(
                index, 0, 0, 0, 1, 0)).ToArray(),
            bestiary,
            Enumerable.Repeat(true, 26).ToArray());

    private static AuthoritativeCathedralThemeItemCatalog ItemCatalog()
    {
        var items = new[] {
            new AuthoritativeCathedralNativeItemDefinition(
                NativeItemIndex: 0, NativeItemId: 0, DropRate: 1, ItemClass: 4,
                EquipLocation: 7, CursorGraphic: 4, ItemType: 11, Name: "Gold", ShortName: "Gold",
                MinimumMonsterLevel: 0, Durability: 0, MinimumDamage: 0, MaximumDamage: 0,
                MinimumArmorClass: 0, MaximumArmorClass: 0, MinimumStrength: 0,
                MinimumMagic: 0, MinimumDexterity: 0, Flags: 0, MiscId: 0, SpellId: 0,
                Usable: true, Value: 10, AnimationFrameCount: 4),
            new AuthoritativeCathedralNativeItemDefinition(
                NativeItemIndex: 1, NativeItemId: 1, DropRate: 1, ItemClass: 1,
                EquipLocation: 7, CursorGraphic: 10, ItemType: 1, Name: "Sword", ShortName: "Sword",
                MinimumMonsterLevel: 0, Durability: 0, MinimumDamage: 0, MaximumDamage: 0,
                MinimumArmorClass: 0, MaximumArmorClass: 0, MinimumStrength: 0,
                MinimumMagic: 0, MinimumDexterity: 0, Flags: 0, MiscId: 0, SpellId: 0,
                Usable: true, Value: 10, AnimationFrameCount: 4),
        };
        var spells = Enumerable.Range(0, 58).Select(index => new AuthoritativeCathedralNativeSpellDefinition(
            index, $"Spell{index}", index == 0 ? -1 : 0, index == 1 ? 0 : -1,
            1, 10, 10, 0, 1, 3)).ToArray();
        return new(
            items,
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            Array.Empty<AuthoritativeCathedralNativeUniqueDefinition>(),
            spells,
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> {
                [4] = new(4), [5] = new(4), [6] = new(4),
                [10] = new(4), [86] = new(4), [87] = new(4), [88] = new(4),
            },
            Enumerable.Range(0, 256).ToDictionary(value => value, _ => 8),
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            rules: new AuthoritativeItemGenerationRules(0, 0, 1, 15, 4, 0, 0, 0, 59, 0),
            replacementGlyphWidth: 8);
    }

    private static IReadOnlyList<AuthoritativeCathedralThemeRoomMonsterDefinition> Bestiary(
        bool includeScatter = false,
        bool skeleton = false,
        uint abilityFlags = 0)
    {
        var definition = new AuthoritativeCathedralPopulationMonsterDefinition(
            skeleton ? 8 : 0, skeleton, 1, 1, 0, 0, abilityFlags, 1, 1, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, new int[6], Enumerable.Repeat(1, 6).ToArray());
        return [new(definition, includeScatter ? 1U : 0U, false)];
    }
}

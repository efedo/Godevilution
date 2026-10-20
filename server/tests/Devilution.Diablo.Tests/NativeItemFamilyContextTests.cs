using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

/** C2 pool ownership is internal and explicit; public Cathedral shapes keep their original bounds. */
public sealed class NativeItemFamilyContextTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void ExplicitC2OwnershipRetainsEverySlotAndRoundTripsWithoutWideningC1(int depth)
    {
        var profile = Profile(41, depth);
        var contexts = new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
            [41] = AuthoritativeNativeDungeonFamilyContext.Catacombs(depth),
        };
        var runtime = new AuthoritativeCathedralItemRuntime(profile, null, null, contexts);
        contexts.Clear(); // Runtime ownership must not alias the caller's map.
        var checkpoint = runtime.CaptureCheckpoint();
        Assert.Equal(128, checkpoint.Levels.Single().Items.Count);
        Assert.Equal(127, checkpoint.Levels.Single().ActiveItemIds.Count);
        var trusted = Contexts(41, depth);
        AuthoritativeCathedralItemRuntime.ValidateCheckpointShape(checkpoint, trusted);
        var restored = new AuthoritativeCathedralItemRuntime(profile, checkpoint, null, trusted);
        Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(restored.CaptureCheckpoint()));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile));
        Assert.Throws<InvalidDataException>(() => checkpoint.ValidateShape());
        Assert.Throws<InvalidDataException>(() => checkpoint.Levels.Single().ValidateShape());
    }

    [Theory]
    [InlineData(AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor, false)]
    [InlineData(AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor, true)]
    public void DoorMutationPoliciesRequireTheirExplicitFamily(
        AuthoritativeCathedralItemObjectMutationPolicy mutation, bool catacombs)
    {
        var profile = Profile(41, catacombs ? 5 : 1);
        var level = profile.LevelProfiles.Single();
        var references = level.InitialObjectRefs.ToArray();
        references[4 * 112 + 4] = 1;
        profile = profile with { LevelProfiles = [level with {
            InitialObjectRefs = references,
            ObjectBindings = [new(0, 701, catacombs ? 1 : 42, 4, 4, false, 3,
                MutationPolicy: mutation)],
        }] };
        var contexts = catacombs ? Contexts(41, 5)
            : new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [41] = AuthoritativeNativeDungeonFamilyContext.Cathedral(1),
            };
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile, null, null, contexts));
        if (!catacombs)
            Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile));
    }

    [Fact]
    public void MixedFamiliesRetainIndependentFloorsAndOneUniqueHistory()
    {
        var c1 = Profile(1, 1);
        var c2 = Profile(41, 6);
        var initial = c1.InitialCheckpoint with {
            Levels = c1.InitialCheckpoint.Levels.Concat(c2.InitialCheckpoint.Levels).ToArray(),
            UniqueItemFlags = [2, 17],
        };
        var profile = c1 with {
            InitialCheckpoint = initial,
            LevelProfiles = c1.LevelProfiles.Concat(c2.LevelProfiles).ToArray(),
        };
        var contexts = Contexts(41, 6);
        contexts[1] = AuthoritativeNativeDungeonFamilyContext.Cathedral(1);
        var runtime = new AuthoritativeCathedralItemRuntime(profile, null, null, contexts);
        var captured = runtime.CaptureCheckpoint();
        Assert.Equal(new uint[] { 1, 41 }, captured.Levels.Select(level => level.LevelId));
        Assert.Equal(new[] { 1, 6 }, captured.Levels.Select(level => level.Depth));
        Assert.Equal(new[] { 2, 17 }, captured.UniqueItemFlags);
        Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(
            new AuthoritativeCathedralItemRuntime(profile, captured, null, contexts).CaptureCheckpoint()));
    }

    [Fact]
    public void MissingExtraDefaultAndWrongDepthContextsAreRejectedBeforeAdmission()
    {
        var profile = Profile(41, 5);
        var invalid = new[] {
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext>(),
            Contexts(42, 5),
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> { [41] = default },
            Contexts(41, 6),
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [41] = AuthoritativeNativeDungeonFamilyContext.Cathedral(1),
            },
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [41] = AuthoritativeNativeDungeonFamilyContext.Catacombs(5),
                [42] = AuthoritativeNativeDungeonFamilyContext.Catacombs(6),
            },
        };
        var before = JsonSerializer.Serialize(profile.InitialCheckpoint);
        foreach (var contexts in invalid)
            Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile, null, null, contexts));
        Assert.Equal(before, JsonSerializer.Serialize(profile.InitialCheckpoint));
    }

    [Fact]
    public void ContextDoesNotPermitDuplicateFloorsOrAlteredPoolShape()
    {
        var profile = Profile(41, 5);
        var contexts = Contexts(41, 5);
        var initial = profile.InitialCheckpoint;
        var level = initial.Levels.Single();
        foreach (var altered in new[] {
            initial with { Levels = [level, level] },
            initial with { Levels = [level with { Depth = 6 }] },
            initial with { Levels = [level with { ActiveItemIds = level.ActiveItemIds.Skip(1).ToArray() }] },
            initial with { Levels = [level with { Items = level.Items.Skip(1).ToArray() }] },
            initial with { UniqueItemFlags = [17, 2] },
        })
            Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile, altered, null, contexts));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(
            profile with { LevelProfiles = [profile.LevelProfiles.Single(), profile.LevelProfiles.Single()] },
            null, null, contexts));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    public void RuntimeDeathUsesFloorDepthAndRestoresItsCommittedPool(int depth)
    {
        var profile = Profile(41, depth, forceDrop: true);
        var contexts = Contexts(41, depth);
        var runtime = new AuthoritativeCathedralItemRuntime(profile, null, null, contexts);
        var transaction = runtime.PrepareLoot(new AuthoritativeCathedralItemDeathRequest(
            41, 0, 7, 10, 10, AuthoritativeCathedralItemRuntimeTestData.Occupancy()));
        Assert.True(transaction.Loot.Dropped);
        var committed = runtime.CommitDeath(transaction, () => 50001);
        Assert.True(committed.HasWorldItem);
        var checkpoint = runtime.CaptureCheckpoint();
        Assert.Equal(depth, checkpoint.Levels.Single().Depth);
        Assert.Equal(1, checkpoint.Levels.Single().ActiveItemCount);
        Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(
            new AuthoritativeCathedralItemRuntime(profile, checkpoint, null, contexts).CaptureCheckpoint()));
    }

    [Theory]
    [InlineData(5, 68)]
    [InlineData(7, 57)]
    [InlineData(8, 48)]
    public void C2ObjectTransactionsValidateCandidatePoolsAndRejectStaleCommit(int depth, int objectType)
    {
        var source = Profile(41, depth);
        var objectRefs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        objectRefs[50 * AuthoritativeCathedralItemRuntime.Width + 50] = 1;
        if (objectType == 48)
            objectRefs[49 * AuthoritativeCathedralItemRuntime.Width + 50] = -1;
        var mutation = objectType switch {
            57 => AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel,
            48 => AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus,
            _ => AuthoritativeCathedralItemObjectMutationPolicy.NativeChest,
        };
        var profile = source with {
            ItemBindings = source.ItemCatalog.Items.ToDictionary(item => item.NativeItemIndex,
                item => new AuthoritativeCathedralItemContentBinding(item.NativeItemIndex,
                    checked((uint)(5000 + item.NativeItemIndex)), $"item_{item.NativeItemIndex}")),
            LevelProfiles = [source.LevelProfiles.Single() with {
                InitialObjectRefs = objectRefs,
                ObjectBindings = [new(0, 900, objectType, 50, 50, objectType == 57, 1, MutationPolicy: mutation)],
            }],
        };
        var contexts = Contexts(41, depth);
        var runtime = new AuthoritativeCathedralItemRuntime(profile, null, null, contexts);
        var world = runtime.BuildWorldState(41, new(new int[AuthoritativeCathedralItemRuntime.CellCount],
            new int[AuthoritativeCathedralItemRuntime.CellCount], objectRefs,
            profile.LevelProfiles.Single().SolidTiles, profile.LevelProfiles.Single().SetPieceTiles,
            [objectType == 57], [true]));
        var policy = new AuthoritativeCathedralThemeItemPolicy(depth);
        var nextId = 50001U;
        var openCount = 0;
        Action commit;
        if (objectType == 68) {
            var transaction = runtime.PrepareCatacombsChestLoot(41, new(world, profile.ItemCatalog, policy,
                new(0, 68, 50, 50, 2, 1, 1, 1, true, 1), 51, 50));
            Assert.NotNull(transaction.Operation.TrapPlan);
            commit = () => Assert.NotNull(runtime.CommitChestLoot(transaction.Items,
                () => nextId++, _ => false, () => true, () => openCount++));
        } else if (objectType == 57) {
            var facts = new AuthoritativeCathedralNativeBarrelFacts(0, 900, 57, 41, new(50, 50), 2,
                0, 0, 1, -1, 1, 1, 0, 1000, 0, 5, true, true, 1, false, false, false, null, 0, 0);
            var transaction = runtime.PrepareBarrelOperation(41, new(world, profile.ItemCatalog, policy,
                facts, new bool[9], 123));
            commit = () => Assert.NotNull(runtime.CommitBarrelOperation(transaction,
                () => nextId++, _ => false, () => true, () => openCount++));
        } else {
            var facts = new AuthoritativeCathedralNativeSarcophagusFacts(0, 900, 48, 41, new(50, 50), 2,
                2, -1, 1, 1, 0, 1, 0, 8, false, false, null, 8, 0);
            var transaction = runtime.PrepareSarcophagusOperation(41, new(world, profile.ItemCatalog, policy,
                facts, new bool[9]));
            commit = () => Assert.NotNull(runtime.CommitSarcophagusOperation(transaction,
                () => nextId++, _ => false, () => true, () => openCount++));
        }
        commit();
        var checkpoint = runtime.CaptureCheckpoint();
        Assert.Equal(1, openCount);
        Assert.Equal(1, checkpoint.Levels.Single().ActiveItemCount);
        Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(
            new AuthoritativeCathedralItemRuntime(profile, checkpoint, null, contexts).CaptureCheckpoint()));
        var nextAfterCommit = nextId;
        Assert.Throws<InvalidOperationException>(commit);
        Assert.Equal(nextAfterCommit, nextId);
        Assert.Equal(1, openCount);
        Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(runtime.CaptureCheckpoint()));
    }

    internal static AuthoritativeCathedralItemRuntimeProfile Profile(uint levelId, int depth, bool forceDrop = false)
    {
        var source = AuthoritativeCathedralItemRuntimeTestData.Profile(forceDrop);
        return source with {
            InitialCheckpoint = source.InitialCheckpoint with {
                Levels = [source.InitialCheckpoint.Levels.Single() with { LevelId = levelId, Depth = depth }],
            },
            LevelProfiles = [source.LevelProfiles.Single() with { LevelId = levelId, Depth = depth }],
        };
    }

    internal static Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> Contexts(uint levelId, int depth)
        => new() { [levelId] = AuthoritativeNativeDungeonFamilyContext.Catacombs(depth) };
}

using System.Text.Json;
using Devilution.Diablo;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Format 13 stores each geometry level once and decodes to the four format 12 copies. */
public sealed partial class DiabloCohortCodecTests
{
    [Fact]
    public void CompactGeometryKeepsOneCopyAndOnlyChangedEffectiveLevels()
    {
        var state = CatacombsGeometryState();
        Assert.True(DiabloCohortCodec.TryCompactGeometry(state, out var compact));

        Assert.Equal(DiabloCohortState.NativeCatacombsCompactGeometryFormatVersion, compact.FormatVersion);
        Assert.Same(state.Geometry, compact.Geometry);
        Assert.Empty(compact.CathedralDoors!.BackingGeometry.Levels!);
        Assert.Empty(compact.CathedralDoors.BackingGeometry.WarpTargets!);
        Assert.Empty(compact.CathedralDoors.EffectiveLevels);
        var changed = Assert.Single(compact.WorldInfrastructure.EffectiveLevels!);
        Assert.Equal(1U, changed.LevelId);
    }

    [Fact]
    public void CompactGeometryExpandsToTheFourOriginalCopies()
    {
        var state = CatacombsGeometryState();
        Assert.True(DiabloCohortCodec.TryCompactGeometry(state, out var compact));
        var expanded = DiabloCohortCodec.ExpandCompactGeometry(compact);

        Assert.Equal(DiabloCohortState.NativeCatacombsProgressionFormatVersion, expanded.FormatVersion);
        Assert.Equal(Json(state.Geometry), Json(expanded.Geometry));
        Assert.Equal(Json(state.CathedralDoors!.BackingGeometry), Json(expanded.CathedralDoors!.BackingGeometry));
        Assert.Equal(Json(state.CathedralDoors.EffectiveLevels), Json(expanded.CathedralDoors.EffectiveLevels));
        Assert.Equal(Json(state.WorldInfrastructure.EffectiveLevels), Json(expanded.WorldInfrastructure.EffectiveLevels));
        Assert.Equal(state.CathedralDoors.DoorLevels, expanded.CathedralDoors.DoorLevels);
    }

    [Fact]
    public void CompactGeometryKeepsFormatTwelveWhenCopiesDisagree()
    {
        var state = CatacombsGeometryState();
        var doors = state.CathedralDoors!;
        var changedBacking = state with {
            CathedralDoors = doors with {
                BackingGeometry = doors.BackingGeometry with {
                    Levels = [doors.BackingGeometry.Levels![0] with { BlockedCells = [5] }, doors.BackingGeometry.Levels[1]],
                },
            },
        };
        var changedDoorEffective = state with {
            CathedralDoors = doors with { EffectiveLevels = [doors.EffectiveLevels[0], doors.EffectiveLevels[0]] },
        };
        var otherFormat = state with { FormatVersion = DiabloCohortState.NativeTownProgressionFormatVersion };

        foreach (var candidate in new[] { changedBacking, changedDoorEffective, otherFormat }) {
            Assert.False(DiabloCohortCodec.TryCompactGeometry(candidate, out var unchanged));
            Assert.Same(candidate, unchanged);
        }
    }

    [Fact]
    public void CompactGeometryRefusesNonCanonicalEncodings()
    {
        Assert.True(DiabloCohortCodec.TryCompactGeometry(CatacombsGeometryState(), out var compact));
        var geometry = compact.Geometry!;
        var changed = compact.WorldInfrastructure.EffectiveLevels![0];
        var refusals = new[] {
            compact with { CathedralDoors = compact.CathedralDoors! with { BackingGeometry = geometry } },
            compact with { CathedralDoors = compact.CathedralDoors! with { EffectiveLevels = [changed] } },
            compact with { WorldInfrastructure = compact.WorldInfrastructure with { EffectiveLevels = [geometry.Levels![0]] } },
            compact with { WorldInfrastructure = compact.WorldInfrastructure with { EffectiveLevels = [changed with { LevelId = 9 }] } },
            compact with { WorldInfrastructure = compact.WorldInfrastructure with { EffectiveLevels = [changed, changed] } },
            compact with { WorldInfrastructure = compact.WorldInfrastructure with { EffectiveLevels = null } },
        };
        foreach (var refusal in refusals)
            Assert.Throws<InvalidDataException>(() => DiabloCohortCodec.ExpandCompactGeometry(refusal));
    }

    [Fact]
    public void CompactGeometryVersionIsRefusedOutsideItsSchema()
    {
        var document = System.Text.Json.Nodes.JsonNode.Parse(codec.Serialize(CreateState()))!.AsObject();
        document["FormatVersion"] = DiabloCohortState.NativeCatacombsCompactGeometryFormatVersion;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    /** Two levels; level 1 has one opened door cell in its effective copies. */
    private static DiabloCohortState CatacombsGeometryState()
    {
        static DiabloWorldGeometryLevelState Level(uint levelId, int[] blocked) => new(
            levelId, $"level-{levelId}", 4, 4, blocked, [1], [2, 3], [], [], [4, 5],
            new Dictionary<ushort, AuthoritativeDunTileFlags> { [4] = AuthoritativeDunTileFlags.Solid },
            blocked, blocked, true, [1], [], levelId == 0, false, 1, 1);
        static DiabloWorldGeometryState Geometry() => new(
            [Level(0, [2, 3]), Level(1, [6, 7])],
            [new DiabloWorldGeometryWarpState(1, 0, 1, 1, 1, 2, 2, 0, false, 0, false, "", "stairs")]);
        IReadOnlyList<DiabloWorldGeometryLevelState> Effective() => [Level(0, [2, 3]), Level(1, [7])];
        var state = CreateState();
        return state with {
            FormatVersion = DiabloCohortState.NativeCatacombsProgressionFormatVersion,
            Geometry = Geometry(),
            WorldInfrastructure = state.WorldInfrastructure with { EffectiveLevels = Effective() },
            CathedralDoors = new DiabloCathedralDoorCheckpoint(1, "original-diablo.cathedral-doors", 1,
                new string('A', 64), Geometry(), Effective(), [new(1, [], [], [])]),
        };
    }
}

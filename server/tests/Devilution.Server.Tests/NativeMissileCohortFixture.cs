using Devilution.Diablo;

namespace Devilution.Server.Tests;

/** Synthetic empty native owners: no archive, captured map, or retail-derived metadata. */
internal static class NativeMissileCohortFixture
{
    internal static DiabloCohortState Create(int depth)
    {
        var source = DiabloCohortCodecTests.NativeTrapState(DiabloNativeTrapDisposition.Fired);
        var fingerprint = new string('a', 64);
        var cells = 112 * 112;
        var pieces = new ushort[cells];
        var properties = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            .ToDictionary(value => (ushort)value, _ => (AuthoritativeDunTileFlags)0);
        var town = new DiabloNativeTownCheckpoint(fingerprint, 0, AuthoritativeNativeTownCatalog.GeneratorId,
            AuthoritativeNativeTownCatalog.GeneratorRevision, AuthoritativeTownWarpPolicy.Version,
            new(false, false, false, 1, AuthoritativeTownWarpUnlocks.None,
                AuthoritativeTownFarmerQuestState.Other, AuthoritativeTownGraveQuestState.Other,
                AuthoritativeTownEntry.Main, 0, AuthoritativeTownWaterQuestState.NotAvailable, 0),
            new(false, false, false, false, false), 0, 0, 75, 68, 10, 10, 84, 84,
            Enumerable.Repeat(1, 10).ToArray(), [], pieces,
            new ushort[AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight],
            [], new ushort[cells], new ushort[cells], new AuthoritativeDunTileFlags[cells], [], [], [], properties);
        var levels = depth == 1 ? new[] { 1U } : new[] { 1U, (uint)depth };
        var entries = depth == 1 ? Array.Empty<DiabloNativeFloorEntry>() : new[] {
            new DiabloNativeFloorEntry((uint)depth, new(6400, 0, 5, 8, new(56, 56), true), [], fingerprint.ToUpperInvariant()) {
                CatacombsLifeQ6 = depth >= 5 ? 6400 : null,
            },
        };
        DiabloWorldGeometryLevelState Level(uint id) => new(id, $"synthetic-{id}", 112, 112,
            [], [], new ushort[cells], [], [], id == 0 ? pieces : [], id == 0 ? properties : [],
            [], [], true, [], [], id == 0, false, 56, 56);
        var geometry = new DiabloWorldGeometryState(new[] { Level(0) }.Concat(levels.Select(Level)).ToArray(), []);
        var pools = levels.Select(id => source.CathedralItems!.Levels[0] with { LevelId = id, Depth = (int)id }).ToArray();
        return source with {
            FormatVersion = DiabloCohortState.NativeCatacombsProgressionFormatVersion,
            Tick = 0, Participants = [source.Participants[0] with {
                LastAppliedTick = null, Player = new StorePlayerSnapshot(0, null, [], 0, 6400, 0, 0,
                    PlayerAttributesState.Zero, [], [], []) { EntityId = 1, LevelId = (uint)depth,
                        PositionX = 56, PositionY = 56, LifeMaximum = 6400, BaseLifeMaximum = 6400 },
            }],
            CombatTargets = [], WorldItems = [], Objects = [], Projectiles = [], Portals = [],
            Geometry = geometry, WorldInfrastructure = new(112, 112, [], [], [], null, geometry.Levels),
            CathedralItems = source.CathedralItems! with { Levels = pools }, NativeSarcophagi = [], NativeBarrels = [], NativeTraps = [],
            CathedralDoors = new(1, "original-diablo.cathedral-doors", 1, fingerprint.ToUpperInvariant(), geometry, geometry.Levels!,
                levels.Where(id => id <= 4).Select(id => new DiabloCathedralDoorLevelState(id, [], [], [])).ToArray()),
            NativeProgression = new(fingerprint.ToUpperInvariant(), entries, []), NativeTown = town, NativeCatacombs = new([], [], [], []),
        };
    }
}

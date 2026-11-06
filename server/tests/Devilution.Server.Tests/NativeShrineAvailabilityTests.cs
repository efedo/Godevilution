using System.Text.Json;
using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeShrineAvailabilityTests
{
    [Fact(Explicit = true)]
    public void MeasureNativeShrineAvailabilityAcrossOneHundredCatacombsSeeds()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_SHRINE_AVAILABILITY_METADATA");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Skip("Select the private native Catacombs profile metadata directory.");
        var pack = ContentPackLoader.LoadManifestDirectory(directory!);
        var metadata = AuthoritativeCatacombsPopulationMetadata.LoadProfileSidecar(new ContentManifest("shrine-availability", "1.0.0", [pack]))!;
        var rows = new List<object>();
        for (uint seed = 0; seed < 100; seed++) {
            for (var depth = 5; depth <= 8; depth++) {
                var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(metadata, depth, seed, 0,
                    AuthoritativeCatacombsEntry.Main, new(11776, 640, 10, 8, new(75, 68), true),
                    stableLevelSymbol: $"catacombs{depth}");
                var (snapshot, geometry) = CreateApproachSnapshot(fresh);
                var player = snapshot.Players[0];
                var shrines = snapshot.Objects.Where(value => value.NativeObjectId is 59 or 60).ToArray();
                var nearby = shrines.Where(value => NativeShrineJourneyEvidence.CanDemonstrate(value, player))
                    .Select(value => new { value.PositionX, value.PositionY, Type = value.NativeShrineType,
                        Distance = NativeObjectApproachPath.FindForShrine(snapshot, geometry, value, required: true)?.Count,
                        UnobstructedDistance = NativeObjectApproachPath.FindForShrine(snapshot, geometry, value, required: false)?.Count })
                    .Where(value => value.Distance <= 24).ToArray();
                rows.Add(new { Seed = seed, Depth = depth, EntryX = player.PositionX, EntryY = player.PositionY,
                    Shrines = shrines.Length, Nearby = nearby });
            }
        }
        var output = Environment.GetEnvironmentVariable("GODEVILUTION_SHRINE_AVAILABILITY_REPORT")
            ?? throw new InvalidOperationException("Select a private shrine availability report path.");
        File.WriteAllText(output, JsonSerializer.Serialize(rows));
        Assert.Equal(400, rows.Count);
    }

    internal static (Snapshot Snapshot, LevelGeometrySnapshot Geometry) CreateApproachSnapshot(AuthoritativeCatacombsFreshLevelResult fresh)
    {
        var state = fresh.Population.State;
        const int width = 112;
        var geometry = new LevelGeometrySnapshot { LevelId = (uint)fresh.Request.Depth, Width = width, Height = width };
        var snapshot = new Snapshot { Players = { new PlayerSnapshot {
            EntityId = 1, LevelId = geometry.LevelId,
            PositionX = fresh.Geometry.GenerationResult.EntryPosition.X, PositionY = fresh.Geometry.GenerationResult.EntryPosition.Y,
            Attributes = new PlayerAttributesSnapshot { Strength = new() { Base = 80, Current = 80 }, Magic = new() { Base = 10, Current = 10 },
                Dexterity = new() { Base = 30, Current = 30 }, Vitality = new() { Base = 65, Current = 65 } },
        } } };
        for (var cell = 0; cell < state.Pieces.Count; cell++) {
            // Match native population backing geometry after objects have changed dPiece.
            if ((fresh.Request.Metadata.EffectivePieceProperties[state.Pieces[cell]] & (byte)AuthoritativeDunTileFlags.Solid) != 0)
                geometry.BlockedCells.Add(cell);
            var reference = state.ObjectRefs[cell];
            if (reference != 0 && state.ObjectSlots[Math.Abs(reference) - 1].SolidFlag)
                geometry.StaticObjectBlockedCells.Add(cell);
            if (state.Pieces[cell] is 266 or 270 or 558)
                geometry.WarpTriggers.Add(new WarpTriggerSnapshot { TriggerX = cell % width, TriggerY = cell / width });
        }
        foreach (var slot in state.ActiveObjectIds.Take(state.ActiveObjectCount)) {
            var raw = state.ObjectSlots[slot];
            snapshot.Objects.Add(new ObjectSnapshot {
                EntityId = (uint)slot + 10, LevelId = geometry.LevelId, NativeObjectId = (uint)raw.Type,
                PositionX = raw.Position.X, PositionY = raw.Position.Y, MissilePassable = raw.MissilePassable,
                AnimationFrame = raw.AnimFrame,
                NativeThemeObjectInteractable = AuthoritativeNativePopulationBindings.IsSupportedNativeThemeItem(raw, state.MonsterSlots)
                    || AuthoritativeNativePopulationBindings.IsSupportedNativeThemeEffect(raw),
                NativeShrineType = raw.Type is 59 or 60 ? (uint)raw.Var1 : 0,
                NativeShrineInteractable = raw.Type is 59 or 60 && raw.Var1 is >= 0 and <= 25 && raw.SelectionRegion != 0,
            });
        }
        foreach (var slot in state.ActiveMonsterIds.Take(state.ActiveMonsterCount)) {
            var raw = state.MonsterSlots[slot];
            snapshot.Monsters.Add(new MonsterSnapshot {
                EntityId = (uint)slot + 1000, LevelId = geometry.LevelId, Alive = raw.HitPoints > 0,
                PositionX = raw.Tile.X, PositionY = raw.Tile.Y,
                Moving = raw.Future != raw.Tile, FuturePositionX = raw.Future.X, FuturePositionY = raw.Future.Y,
            });
        }
        return (snapshot, geometry);
    }
}

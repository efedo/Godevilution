using System.Collections;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LevelScopedCompatibilityTests
{
    [Fact]
    public void AllPhysicalCollectionsRetainTheSameZeroLevelRowsAsPresentation()
    {
        var source = LevelScopedSnapshotTests.World();
        source.Players[0].LevelId = 2;
        foreach (var name in new[] { "players", "monsters", "objects", "world_items", "projectiles", "corpses" }) {
            var field = Snapshot.Descriptor.FindFieldByName(name);
            var rows = (IList)field.Accessor.GetValue(source);
            if (name != "players") rows.Clear();
            foreach (var level in new uint[] { 0, 2, 3 }) {
                var row = (IMessage)Activator.CreateInstance(field.MessageType.ClrType)!;
                row.Descriptor.FindFieldByName("entity_id").Accessor.SetValue(row, 100U + level);
                row.Descriptor.FindFieldByName("level_id").Accessor.SetValue(row, level);
                rows.Add(row);
            }
            var scopedRows = (IList)field.Accessor.GetValue(source.ForReceivingLevel());
            Assert.Equal(rows.Cast<IMessage>().Where(row => AuthoritativeLevelVisibility.IsVisible(
                    (uint)row.Descriptor.FindFieldByName("level_id").Accessor.GetValue(row), 2))
                .Select(row => (uint)row.Descriptor.FindFieldByName("entity_id").Accessor.GetValue(row)),
                scopedRows.Cast<IMessage>().Select(row => (uint)row.Descriptor.FindFieldByName("entity_id").Accessor.GetValue(row)));
        }
    }

    [Fact]
    public void ZeroLevelMonsterStaysAdmittedVisibleAndBlockingInADungeon()
    {
        var source = LevelScopedSnapshotTests.World(); source.Players[0].LevelId = 1;
        source.Players[1].LevelId = 0;
        source.Monsters[1].PositionX = source.Monsters[1].PositionY = 7;
        var wire = Snapshot.Parser.ParseFrom(source.ForReceivingLevel().ToByteArray());
        var model = new AuthoritativeClientModel();
        model.Apply(new(Snapshot: wire) { LevelScopedSnapshots = true });
        Assert.Contains(model.Snapshot!.Monsters, value => value.EntityId == 3 && value.LevelId == 0);
        Assert.Contains(model.Snapshot.Players, value => value.EntityId == 2 && value.LevelId == 0);
        Assert.True(AuthoritativeLevelVisibility.IsVisible(model.Snapshot.Monsters[0].LevelId, 1));
        model.TrackPredictedMoveTo(1, 2, 2);
        Assert.Null(model.LocalMovementTarget);
    }

    [Fact]
    public void MovingZeroLevelMonsterCannotBecomeAnIdleReference()
    {
        var source = LevelScopedSnapshotTests.World(); source.Players[0].LevelId = 1;
        source.Monsters[0].Moving = true;
        source.Monsters[0].FuturePositionX = 2; source.Monsters[0].FuturePositionY = 3;
        source.Monsters[0].MovementStartedTick = 10; source.Monsters[0].MovementEffectTick = 18;
        var cache = new AuthoritativeSnapshotGeometryCache(true, true);
        var model = new AuthoritativeClientModel();
        model.Apply(Message(cache.Project(source)));
        source.Tick++;
        var second = cache.Project(source);
        Assert.False(second.StateUnchanged);
        model.Apply(Message(second));
        Assert.False(AuthoritativeSnapshotGeometryCache.IsIdleBody(model.Snapshot!, true));

        static AuthoritativeClientMessage Message(Snapshot value) => new(Snapshot: value) {
            LevelScopedSnapshots = true, LevelScopedIdleReferences = true };
    }
}

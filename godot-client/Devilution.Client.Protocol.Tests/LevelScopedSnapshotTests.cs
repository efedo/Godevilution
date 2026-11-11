using Devilution.Client.Acceptance;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LevelScopedSnapshotTests
{
    [Fact]
    public void StaleFullViewCannotReplaceAnAdmittedReferenceBaseline()
    {
        var source = World();
        var cache = new AuthoritativeSnapshotGeometryCache(true, true);
        var model = new AuthoritativeClientModel();
        model.Apply(Message(cache.Project(source)));
        var revision = model.AcceptedSnapshotContentRevision;
        source.Tick = 9; source.Players[0].LevelId = 1;
        model.Apply(Message(cache.Project(source)));
        Assert.Equal(0U, model.Snapshot!.Players[0].LevelId);
        Assert.Equal(revision, model.AcceptedSnapshotContentRevision);
        source.Tick = 11;
        var unknown = cache.Project(source);
        Assert.True(unknown.StateUnchanged);
        Assert.Throws<InvalidDataException>(() => model.Apply(Message(unknown)));

        static AuthoritativeClientMessage Message(Snapshot value) => new(Snapshot: value) {
            LevelScopedSnapshots = true, LevelScopedIdleReferences = true };
    }

    [Fact]
    public void ColdWorldProofAndFloorChecksDoNotMistakeHiddenRowsForMissingState()
    {
        var source = World();
        var view = source.ForReceivingLevel();
        Assert.Equal(source.StateSha256, LevelScopedSnapshotEvidence.CompleteWorldHash(view));
        Assert.Null(LevelScopedSnapshotEvidence.CompleteWorldHash(source));
        Assert.True(LevelScopedSnapshotEvidence.IncludesLevel(view, 0));
        Assert.False(LevelScopedSnapshotEvidence.IncludesLevel(view, 1));
        Assert.True(LevelScopedSnapshotEvidence.IncludesLevel(source, 1));
        var expected = LevelScopedSnapshotEvidence.ExpectedView(source, view);
        Assert.Equal(view, expected);
        Assert.Equal(2, source.Monsters.Count); // The historical baseline remains available for real revisits.
        var changed = source.Clone(); changed.StateSha256 = new string('b', 64);
        Assert.NotEqual(LevelScopedSnapshotEvidence.CompleteWorldHash(view),
            LevelScopedSnapshotEvidence.CompleteWorldHash(changed.ForReceivingLevel()));
    }

    [Fact]
    public void MovingTownAndDungeonViewsBoundWireAndClientAllocation()
    {
        var source = World(); source.Monsters.Clear(); source.Objects.Clear();
        foreach (var geometry in source.LevelGeometry) { geometry.Width = 112; geometry.Height = 112; }
        source.Players[0].CombatMode = PlayerCombatMode.WalkSouthwards;
        source.Players[0].CombatModeEndTick = 18;
        source.Players[0].CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Move,
            StartedAtTick = 10, EffectTick = 18, ReadyTick = 18, EndTick = 18, DirectionX = 1 };
        for (uint level = 1; level <= 2; level++) {
            for (uint actor = 0; actor < 101; actor++) source.Monsters.Add(new MonsterSnapshot {
                EntityId = 1000 + level * 200 + actor, LevelId = level, Alive = true, HitPoints = 64, MaxHitPoints = 64,
                PositionX = 20 + (int)actor % 10, PositionY = 20 + (int)actor / 10 });
            for (uint actor = 0; actor < 72; actor++) source.Objects.Add(new ObjectSnapshot {
                EntityId = 5000 + level * 200 + actor, ObjectId = 1, LevelId = level,
                PositionX = 40 + (int)actor % 10, PositionY = 40 + (int)actor / 10 });
        }
        var fullTown = Sample(false); var scopedTown = Sample(true);
        Assert.True(scopedTown.Bytes < fullTown.Bytes / 5);
        Assert.True(scopedTown.Allocated < fullTown.Allocated / 3);
        source.Players[0].LevelId = 1;
        var fullDungeon = Sample(false); var scopedDungeon = Sample(true);
        Assert.True(scopedDungeon.Bytes < fullDungeon.Bytes * 0.75);
        Assert.True(scopedDungeon.Allocated < fullDungeon.Allocated * 0.75);

        (int Bytes, long Allocated) Sample(bool scope)
        {
            var cache = new AuthoritativeSnapshotGeometryCache(true, scope);
            var model = new AuthoritativeClientModel();
            void Admit(byte[] payload) => model.Apply(new(Snapshot: Envelope.Parser.ParseFrom(payload).Snapshot) {
                LevelScopedSnapshots = scope, LevelScopedIdleReferences = true });
            Admit(new Envelope { Snapshot = cache.Project(source) }.ToByteArray());
            var wire = cache.Project(source);
            Assert.False(wire.StateUnchanged);
            var bytes = new Envelope { Snapshot = wire }.ToByteArray();
            for (var warm = 0; warm < 16; warm++) Admit(bytes);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0; sample < 64; sample++) Admit(bytes);
            return (bytes.Length, (GC.GetAllocatedBytesForCurrentThread() - allocated) / 64);
        }
    }

    [Fact]
    public void EveryLevelCollectionIsScopedWithoutChangingSourceOrGlobalMetadata()
    {
        var source = World();
        for (uint level = 0; level <= 2; level++) {
            source.WorldItems.Add(new WorldItemSnapshot { EntityId = 20 + level, LevelId = level });
            source.Projectiles.Add(new ProjectileSnapshot { EntityId = 30 + level, LevelId = level });
            source.Corpses.Add(new CorpseSnapshot { EntityId = 40 + level, LevelId = level });
            source.QuestTownerInteractions.Add(new QuestTownerInteractionSnapshot { InteractionId = 50 + level, LevelId = level });
            source.TownerGreetings.Add(new TownerGreetingSnapshot { TownerId = (int)level, LevelId = level });
            source.NativeTownResidents.Add(new NativeTownResidentSnapshot { TownerId = (int)level, LevelId = level });
            source.SetLevelObjectOrigins.Add(new SetLevelObjectOriginSnapshot { SetLevelId = (int)level, LevelId = level });
            source.ProceduralQuestPieceOrigins.Add(new ProceduralQuestPieceOriginSnapshot { OriginId = level, LevelId = level });
        }
        source.Quests.Add(new QuestSnapshot { QuestId = 1, LevelId = 2 });
        source.ActiveWorldMutationIds.Add(77); source.MaterializedProceduralLevelIds.Add(2);
        var before = source.ToByteArray();
        var view = RoundTrip(source.ForReceivingLevel());
        Assert.Equal(before, source.ToByteArray());
        Assert.Equal(source.StateSha256, view.StateSha256);
        Assert.Equal(source.RandomState, view.RandomState);
        Assert.Equal(source.LevelGeometry, view.LevelGeometry);
        Assert.Equal(source.Quests, view.Quests);
        Assert.Equal(source.ActiveWorldMutationIds, view.ActiveWorldMutationIds);
        Assert.Equal(source.MaterializedProceduralLevelIds, view.MaterializedProceduralLevelIds);
        Assert.Equal(2, view.ParticipantCount); Assert.Single(view.Players);
        Assert.Equal(new uint[] { 0, 1 }, view.PlayerRoster.Select(value => value.LevelId));
        foreach (var field in new[] { "monsters", "objects", "world_items", "projectiles", "corpses",
            "quest_towner_interactions", "towner_greetings", "native_town_residents", "set_level_object_origins", "procedural_quest_piece_origins" }) {
            var rows = (System.Collections.IList)Snapshot.Descriptor.FindFieldByName(field).Accessor.GetValue(view);
            Assert.Single(rows.Cast<object>());
            Assert.Equal(0U, rows[0]!.GetType().GetProperty("LevelId")!.GetValue(rows[0]));
        }
    }

    [Fact]
    public void PortalEndpointsKeepTownReturnsAndExcludeRemoteOneWayRoutes()
    {
        var source = World();
        source.Portals.Add(new PortalSnapshot { PortalId = 1, SourceLevelId = 1, DestinationLevelId = 0,
            Bidirectional = true, OwnerEntityId = 2, RemainingTicks = 100 });
        source.Portals.Add(new PortalSnapshot { PortalId = 2, SourceLevelId = 1, DestinationLevelId = 0 });
        source.Portals.Add(new PortalSnapshot { PortalId = 3, SourceLevelId = 0, DestinationLevelId = 2 });
        source.Portals.Add(new PortalSnapshot { PortalId = 4, SourceLevelId = 1, DestinationLevelId = 2,
            Bidirectional = true, OwnerEntityId = 2, RemainingTicks = 100 });
        Assert.Equal(new uint[] { 1, 3 }, source.ForReceivingLevel().Portals.Select(value => value.PortalId));
        source.Players[0].LevelId = 1;
        Assert.Equal(new uint[] { 1, 2, 4 }, source.ForReceivingLevel().Portals.Select(value => value.PortalId));
    }

    [Fact]
    public void PortalCreationExpiryAndEndpointChangesInvalidateTheScopedBody()
    {
        var source = World();
        var cache = new AuthoritativeSnapshotGeometryCache(true, true);
        var first = cache.Project(source);
        source.Portals.Add(new PortalSnapshot { PortalId = 1, SourceLevelId = 1, DestinationLevelId = 0,
            Bidirectional = true, OwnerEntityId = 2, RemainingTicks = 100 });
        var created = cache.Project(source);
        Assert.True(created.StateRevision > first.StateRevision); Assert.Single(created.Portals);
        source.Portals[0].RemainingTicks--;
        var expiring = cache.Project(source);
        Assert.True(expiring.StateRevision > created.StateRevision);
        source.Portals[0].DestinationLevelId = 2;
        var disappeared = cache.Project(source);
        Assert.True(disappeared.StateRevision > expiring.StateRevision); Assert.Empty(disappeared.Portals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScopedViewsNegotiateIndependentlyOfReferencesAndSurviveTransitions(bool references)
    {
        var source = World();
        var cache = new AuthoritativeSnapshotGeometryCache(true, true, references);
        var model = new AuthoritativeClientModel();
        var first = Deliver(source);
        Assert.Single(model.Snapshot!.Monsters); Assert.Single(model.Snapshot.Objects);
        Assert.Equal(2, model.Snapshot.ParticipantCount);
        source.Tick++;
        var idle = Deliver(source);
        Assert.Equal(references, idle.StateUnchanged);
        source.Players[0].LevelId = 1; source.Tick++;
        var dungeon = Deliver(source);
        Assert.False(dungeon.StateUnchanged);
        Assert.Equal(references, dungeon.GeometryUnchanged);
        if (references) Assert.True(dungeon.StateRevision > first.StateRevision);
        Assert.Equal(2, model.Snapshot!.Players.Count);
        Assert.All(model.Snapshot.Monsters, value => Assert.True(AuthoritativeLevelVisibility.IsVisible(value.LevelId, 1)));
        source.Players[0].LevelId = 0; source.Tick++;
        Deliver(source);
        Assert.Single(model.Snapshot!.Players);
        model.ResetTransientPresentation();
        cache = new(true, true, references);
        var fresh = Deliver(source);
        Assert.False(fresh.GeometryUnchanged); Assert.False(fresh.StateUnchanged);
        Assert.NotEmpty(fresh.LevelGeometry);

        Snapshot Deliver(Snapshot snapshot)
        {
            var wire = RoundTrip(cache.Project(snapshot));
            model.Apply(new(Snapshot: wire) { LevelScopedIdleReferences = true, LevelScopedSnapshots = true });
            return wire;
        }
    }

    [Fact]
    public void RosterChangesAndVisibleChangesAdvanceRevisionsWhileHiddenMovementDoesNot()
    {
        var source = World();
        var cache = new AuthoritativeSnapshotGeometryCache(true, true);
        var first = cache.Project(source);
        source.Monsters[1].HitPoints = source.Monsters[1].MaxHitPoints = 2;
        source.Players[1].Life = 32; source.Tick++;
        Assert.True(cache.Project(source).StateUnchanged);
        source.Players[1].LevelId = 2; source.Tick++;
        var movedAway = cache.Project(source);
        Assert.True(movedAway.StateRevision > first.StateRevision);
        Assert.Equal(2U, movedAway.PlayerRoster[1].LevelId);
        source.Players[1].LevelId = 0; source.Tick++;
        var appeared = cache.Project(source);
        Assert.Equal(2, appeared.Players.Count);
        Assert.True(appeared.StateRevision > movedAway.StateRevision);
        source.Objects[0].Activated = true; source.Tick++;
        Assert.True(cache.Project(source).StateRevision > appeared.StateRevision);
    }

    [Fact]
    public void ClientRejectsUnnegotiatedScopeOffLevelActorsAndIncompleteRosters()
    {
        var scoped = World().ForReceivingLevel();
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new(Snapshot: scoped)));
        var offLevel = scoped.Clone(); offLevel.Monsters[0].LevelId = 1;
        Reject(offLevel);
        var missing = scoped.Clone(); missing.PlayerRoster.Clear(); Reject(missing);
        var duplicate = scoped.Clone(); duplicate.PlayerRoster.Add(duplicate.PlayerRoster[0].Clone()); Reject(duplicate);
        var omitted = scoped.Clone(); omitted.PlayerRoster[1].LevelId = 0; Reject(omitted);
        var collision = scoped.Clone(); collision.PlayerRoster[1].EntityId = collision.Objects[0].EntityId; Reject(collision);

        static void Reject(Snapshot snapshot) => Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(new(Snapshot: snapshot) { LevelScopedSnapshots = true }));
    }

    [Fact]
    public void LegacyBodiesStayCompleteAndCannotBorrowScopedReferences()
    {
        var source = World();
        var legacy = new AuthoritativeSnapshotGeometryCache(true);
        var full = legacy.Project(source);
        Assert.Equal(2, full.Monsters.Count); Assert.Equal(2, full.Objects.Count); Assert.Empty(full.PlayerRoster);
        var model = new AuthoritativeClientModel();
        var scoped = new AuthoritativeSnapshotGeometryCache(true, true);
        model.Apply(new(Snapshot: RoundTrip(scoped.Project(source))) { LevelScopedSnapshots = true, LevelScopedIdleReferences = true });
        var reference = RoundTrip(scoped.Project(source));
        Assert.True(reference.StateUnchanged);
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: reference) { LevelScopedIdleReferences = true }));
        reference.PlayerRoster.Add(new PlayerRosterEntry { EntityId = 1 });
        Assert.Throws<InvalidDataException>(() => model.Apply(new(Snapshot: reference) { LevelScopedSnapshots = true, LevelScopedIdleReferences = true }));
    }

    private static Snapshot RoundTrip(Snapshot snapshot) => Snapshot.Parser.ParseFrom(snapshot.ToByteArray());

    internal static Snapshot World() => new() {
        Tick = 10, StateSha256 = new string('a', 64), RandomState = 7,
        Players = {
            new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64, PositionX = 1, PositionY = 1 },
            new PlayerSnapshot { EntityId = 2, LevelId = 1, Life = 64, LifeMaximum = 64, PositionX = 1, PositionY = 1 },
        },
        Monsters = {
            new MonsterSnapshot { EntityId = 3, HitPoints = 1, MaxHitPoints = 1, Alive = true, PositionX = 2, PositionY = 2 },
            new MonsterSnapshot { EntityId = 4, LevelId = 1, HitPoints = 1, MaxHitPoints = 1, Alive = true, PositionX = 2, PositionY = 2 },
        },
        Objects = { new ObjectSnapshot { EntityId = 5, ObjectId = 1, PositionX = 3, PositionY = 3 },
            new ObjectSnapshot { EntityId = 6, ObjectId = 1, LevelId = 1, PositionX = 3, PositionY = 3 } },
        LevelGeometry = { new LevelGeometrySnapshot { Width = 10, Height = 10 },
            new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            new LevelGeometrySnapshot { LevelId = 2, Width = 10, Height = 10 } },
    };
}

using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Actual module missile lifecycles cross the producer, protobuf and client validators without an archive. */
public sealed class NativeTrapWireLifecycleTests
{
    [Theory]
    [InlineData(0, 53, 1, 7U)]
    [InlineData(1, 53, 1, 7U)]
    [InlineData(7, 53, 1, 7U)]
    [InlineData(0, 54, 2, 7U)]
    [InlineData(1, 54, 2, 7U)]
    [InlineData(7, 54, 2, 7U)]
    [InlineData(0, 54, 42, 7U)]
    [InlineData(1, 54, 42, 7U)]
    [InlineData(7, 54, 42, 7U)]
    [InlineData(0, 53, 43, 7U)]
    [InlineData(1, 53, 43, 7U)]
    [InlineData(7, 53, 43, 7U)]
    [InlineData(0, 53, 5, 7U)]
    [InlineData(1, 53, 5, 7U)]
    [InlineData(7, 53, 5, 7U)]
    [InlineData(0, 54, 6, 7U)]
    [InlineData(1, 54, 6, 7U)]
    [InlineData(7, 54, 6, 7U)]
    [InlineData(0, 53, 7, 7U)]
    [InlineData(1, 53, 7, 7U)]
    [InlineData(7, 53, 7, 7U)]
    [InlineData(0, 68, 68, 7U)]
    [InlineData(27, 68, 68, 7U)]
    [InlineData(0, 69, 69, 7U)]
    [InlineData(27, 69, 69, 7U)]
    [InlineData(0, 70, 70, 7U)]
    [InlineData(27, 70, 70, 7U)]
    [InlineData(0, 53, 42, 1U)]
    [InlineData(1, 54, 43, 1U)]
    [InlineData(7, 53, 42, 1U)]
    [InlineData(27, 68, 68, 1U)]
    [InlineData(0, 53, 42, 8U)]
    [InlineData(1, 54, 43, 8U)]
    [InlineData(7, 53, 42, 8U)]
    [InlineData(27, 68, 68, 8U)]
    public void EveryObjectMissileKindAndStageReachesTheClient(int type, int sourceType, int triggerType, uint level)
    {
        var intrinsic = sourceType == triggerType;
        var sourceId = 10U; var targetId = intrinsic ? sourceId : 11U;
        var world = new DiabloWorldState(fallbackWidth: 112, fallbackHeight: 112,
            fallbackMissileBlockedCells: [50 * 112 + 52]);
        var random = new DeterministicLegacyRandom(2588);
        var player = new AuthoritativePlayerState(0, 0, 6400, 0, 0, PlayerAttributesState.Zero,
            [], [], [], 60, 60, 6400, 1, level, 0) { EntityId = 1 };
        var processor = new DiabloCombatCommandProcessor(world, new AuthoritativeSpellCatalog([new(1, 1, 0, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100), random);
        var initial = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, sourceId, targetId, level, 1,
            48, 50, 56, 50, (int)DeterministicGridDirections.Toward(48, 50, 56, 50), type, intrinsic, random, (int)level);
        world.Projectiles.Add(initial.EntityId, initial);
        var model = new AuthoritativeClientModel();
        var stages = new HashSet<NativeObjectMissileStage>();
        ulong last = 0;
        for (ulong tick = 0; tick <= 256; tick++) {
            if (tick > 0) {
                var ids = world.Projectiles.Keys.Order().ToList(); var seen = ids.ToHashSet();
                for (var index = 0; index < ids.Count; index++) {
                    if (world.Projectiles.TryGetValue(ids[index], out var missile))
                        processor.ProcessObjectTrapMissile(tick, missile, (int)level, [player], []);
                    foreach (var id in world.Projectiles.Keys.Order()) if (seen.Add(id)) ids.Add(id);
                }
            }
            var projectiles = world.Projectiles.Values.OrderBy(value => value.EntityId)
                .Select(value => JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(value))!).ToArray();
            foreach (var projectile in projectiles) stages.Add(projectile.NativeTrapArrow!.Stage);
            var objects = new List<DiabloObjectState> {
                NativeObject(sourceId, (uint)sourceType, intrinsic, 48, 50, level),
            };
            if (!intrinsic) objects.Add(NativeObject(targetId, (uint)triggerType, triggerType is 5 or 6 or 7, 56, 50, level)
                with { MissilePassable = tick < 2 }); // Closing a fired door cannot invalidate its live missile.
            var source = new DiabloSimulationSnapshot(tick,
                new StorePlayerSnapshot(0, null, [], 0, 6400, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                    EntityId = 1, LevelId = level, PositionX = 60, PositionY = 60,
                }, [], [], objects.ToArray(), [], projectiles, null);
            var wire = DiabloProtocolMapper.ToProtocolSnapshot(source);
            AuthoritativeSnapshotValidator.Validate(wire);
            model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot.Parser.ParseFrom(wire.ToByteArray())));
            Assert.Equal(tick, model.Snapshot!.Tick);
            last = tick;
            if (tick > 0 && projectiles.Length == 0) break;
        }
        Assert.Empty(world.Projectiles);
        Assert.True(last > 0);
        Assert.Contains(NativeObjectMissileStage.Flight, stages);
        if (type == 1) Assert.Contains(NativeObjectMissileStage.Explosion, stages);
        if (type == 27) Assert.Contains(NativeObjectMissileStage.ElementalImpact, stages);
        if (type == 7) Assert.Contains(NativeObjectMissileStage.LightningSegment, stages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ForgedTrapMarkerDoesNotBypassPayloadValidation(int corruption)
    {
        var random = new DeterministicLegacyRandom(2588);
        var missile = DiabloCombatCommandProcessor.AddObjectTrapMissile(800, 10, 11, 7, 1, 48, 50, 56, 50,
            (int)DeterministicGridDirections.Toward(48, 50, 56, 50), 1, false, random, 7);
        var state = JsonSerializer.Deserialize<DiabloProjectileState>(JsonSerializer.Serialize(missile))!;
        state = corruption switch {
            0 => state with { NativeMissileId = "FireArrow" },
            1 => state with { NativeTrapArrow = null },
            2 => state with { WallLeftX = 1 },
            3 => state with { SpellId = 1 },
            _ => state with { NativeTrapArrow = state.NativeTrapArrow! with { MissileTypeId = 100 } },
        };
        var source = new DiabloSimulationSnapshot(1,
            new StorePlayerSnapshot(0, null, [], 0, 6400, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                EntityId = 1, LevelId = 7,
            }, [], [], [], [], [state], null);
        Assert.Throws<InvalidDataException>(() => DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    private static DiabloObjectState NativeObject(uint entity, uint type, bool activated, int x, int y, uint level)
        => new(entity, type, level, x, y, activated, 0, AuthoritativeObjectEffectKind.None, 0) {
            NativeObjectId = type, MissilePassable = true, HideWhenActivated = false,
        };
}

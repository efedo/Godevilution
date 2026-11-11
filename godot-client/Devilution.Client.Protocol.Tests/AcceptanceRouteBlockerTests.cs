using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceRouteBlockerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedCompletePathClearsItsDistantBlockerBeforeRetrying(bool futureCell)
    {
        var state = RefusedStairBoundary();
        var snapshot = state.Snapshot!;
        var blocker = snapshot.Monsters[0];
        if (futureCell) {
            blocker.Moving = true;
            blocker.FuturePositionX = blocker.PositionX; blocker.FuturePositionY = blocker.PositionY;
            blocker.PositionX--; // The reserved destination, rather than the current cell, blocks the route.
        }
        Assert.Empty(NativePath(snapshot));
        var original = snapshot.Players[0].Clone();
        var clears = 0;
        Assert.False(await AcceptanceRouteRecovery.TryAsync(
            () => throw new InvalidOperationException("Travel stopped: Rejected / InvalidTarget."), () => state,
            async _ => Assert.True(await AcceptanceRouteRecovery.ClearBlockedRouteAsync(state,
                Command.IntentOneofCase.MoveRequested, 7, target => {
                    Assert.Same(blocker, target);
                    Assert.Equal(8, Math.Max(Math.Abs(original.PositionX - (target.Moving ? target.FuturePositionX : target.PositionX)),
                        Math.Abs(original.PositionY - (target.Moving ? target.FuturePositionY : target.PositionY))));
                    Assert.Equal(original, snapshot.Players[0]); // Planning never teleports or applies combat.
                    target.Alive = false; // The caller's existing real-controls combat loop owns this effect.
                    clears++;
                    return Task.CompletedTask;
                }))));
        Assert.Equal(1, clears);
        Assert.True(await AcceptanceRouteRecovery.TryAsync(() => {
            Assert.NotEmpty(NativePath(snapshot));
            return Task.CompletedTask;
        }, () => state, _ => throw new InvalidOperationException("The cleared route must not repeat the same refusal.")));
        var travel = new AuthoritativeRouteTravel();
        Assert.True(travel.Start(snapshot, 1006, true));
        var command = travel.Next(snapshot, state.Receipt, true, 0)!;
        Assert.Equal((66, 29), (command.MoveRequested.TargetX, command.MoveRequested.TargetY));
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("not-allowed")]
    [InlineData("too-late")]
    [InlineData("unadmitted-receipt")]
    [InlineData("stale-snapshot")]
    [InlineData("pending")]
    [InlineData("moving")]
    [InlineData("queued-walk")]
    [InlineData("dead")]
    [InlineData("potion")]
    [InlineData("unavailable-route")]
    [InlineData("other-level")]
    public async Task RecoveryRequiresTheSettledRefusedMovementAndItsCurrentRoute(string change)
    {
        var state = RefusedStairBoundary();
        var intent = Command.IntentOneofCase.MoveRequested;
        switch (change) {
        case "accepted": state.Receipt!.Status = CommandStatus.Accepted; break;
        case "not-allowed": state.Receipt!.RejectReason = CommandRejectReason.NotAllowed; break;
        case "too-late": state.Receipt!.RejectReason = CommandRejectReason.TooLate; break;
        case "unadmitted-receipt": state.Receipt!.ClientSequence--; break;
        case "stale-snapshot": state.Snapshot!.Tick--; break;
        case "pending": state = state with { PendingCount = 1 }; break;
        case "moving": state.Player!.CombatMode = PlayerCombatMode.WalkSouthwards; break;
        case "queued-walk": state.Player!.NativeWalkPathPending = true; break;
        case "dead": state.Player!.Life = 0; break;
        case "potion": intent = Command.IntentOneofCase.UseConsumableRequested; break;
        case "unavailable-route": state.Snapshot!.LevelGeometry[0].WarpTriggers[0].Available = false; break;
        case "other-level": state.Player!.LevelId = 5; break;
        }
        Assert.False(await AcceptanceRouteRecovery.ClearBlockedRouteAsync(state, intent, 7,
            _ => throw new InvalidOperationException("This boundary must not start combat.")));
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("minion")]
    [InlineData("off-level")]
    [InlineData("unrelated-position")]
    [InlineData("static-wall")]
    public async Task OnlyALiveRouteBlockerCanStartTheRecoveryFight(string change)
    {
        var state = RefusedStairBoundary(); var snapshot = state.Snapshot!;
        var monster = snapshot.Monsters[0];
        switch (change) {
        case "dead": monster.Alive = false; break;
        case "minion": monster.IsPlayerMinion = true; break;
        case "off-level": monster.LevelId = 5; break;
        case "unrelated-position": monster.PositionX = 90; monster.PositionY = 85; break;
        case "static-wall": snapshot.LevelGeometry[0].StaticObjectBlockedCells.Add(84 * 112 + 83); break;
        }
        Assert.False(await AcceptanceRouteRecovery.ClearBlockedRouteAsync(state, Command.IntentOneofCase.MoveRequested, 7,
            _ => throw new InvalidOperationException("An unrelated monster must not be hunted.")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnreachableShooterDoesNotSkipTheReachableDistantStairBlocker(bool throws)
    {
        var state = RefusedStairBoundary(); var snapshot = state.Snapshot!; var geometry = snapshot.LevelGeometry[0];
        var blocker = snapshot.Monsters[0];
        var shooter = new MonsterSnapshot { EntityId = 800, LevelId = 6, PositionX = 110, PositionY = 110, Alive = true };
        geometry.BlockedCells.Remove(110 * 112 + 110); // An isolated room: confirmed damage does not imply a walking route.
        snapshot.Monsters.Add(shooter);
        snapshot.Projectiles.Add(new ProjectileSnapshot { EntityId = 801, SourceEntityId = shooter.EntityId,
            TargetEntityId = 1, LevelId = 6, RemainingTicks = 20, NativeMissileId = "Arrow" });
        var threats = new JourneyCombatThreats();
        var pressure = threats.Observe(snapshot, snapshot.Players[0], [], 0, 0);
        Assert.Equal(shooter.EntityId, pressure.RangedTarget);
        Assert.Empty(NativePath(snapshot));
        var attempts = new List<uint>(); var skipped = new HashSet<uint>();
        Assert.True(await AcceptanceRouteRecovery.ClearBlockedRouteAsync(state, Command.IntentOneofCase.MoveRequested, 7, async target => {
            Assert.Same(blocker, target);
            var attack = new NativeCombatAttackTarget(target.EntityId);
            attack.PreferRanged(pressure.RangedTarget, threats.IsRangedAttacker(attack.Current));
            var approached = await attack.ApproachAsync(id => {
                attempts.Add(id);
                var monster = snapshot.Monsters.Single(value => value.EntityId == id);
                var path = NativeObjectApproachPath.Find(snapshot, geometry, new ObjectSnapshot {
                    LevelId = 6, PositionX = monster.PositionX, PositionY = monster.PositionY,
                }, pickup: true);
                if (path is null) {
                    if (throws) throw new InvalidOperationException("No ordinary reachable native class encounter within the movement budget.");
                    return Task.FromResult(new MonsterSnapshot { EntityId = 999, PositionX = 110, PositionY = 110 });
                }
                Assert.True(path.Count > 4); // The requested blocker remains outside ordinary local clearing.
                return Task.FromResult(monster);
            }, (monster, id) => monster.EntityId == id
                || Math.Max(Math.Abs(monster.PositionX - 90), Math.Abs(monster.PositionY - 84)) <= 1, () => true);
            if (approached is null) skipped.Add(target.EntityId);
            else {
                Assert.Same(blocker, approached);
                blocker.Alive = false; // The normal combat callback supplies the authoritative kill in the journey.
            }
            attack.PreferRanged(pressure.RangedTarget, threats.IsRangedAttacker(attack.Current));
            Assert.Equal(blocker.EntityId, attack.Current);
        }));
        Assert.Equal(new[] { shooter.EntityId, blocker.EntityId }, attempts);
        Assert.Empty(skipped);
        Assert.False(blocker.Alive);
        Assert.NotEmpty(NativePath(snapshot));
    }

    private static AcceptanceCommandBoundary.Observation RefusedStairBoundary()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 6, Width = 112, Height = 112,
            WarpTriggers = { new WarpTriggerSnapshot { WarpId = 1006, TriggerX = 66, TriggerY = 29,
                Available = true, TargetLevelId = 7 } } };
        // Synthetic corridor, not captured retail geometry. The observed player/stair/blocker
        // positions reproduce a pack outside both the four-tile clear and six-tile safety radii.
        var corridor = new HashSet<(int X, int Y)>();
        for (var x = 82; x <= 90; x++) corridor.Add((x, 84));
        for (var y = 80; y <= 84; y++) corridor.Add((82, y));
        for (var x = 66; x <= 82; x++) corridor.Add((x, 80));
        for (var y = 29; y <= 80; y++) corridor.Add((66, y));
        corridor.Add((90, 85));
        for (var y = 0; y < 112; y++)
            for (var x = 0; x < 112; x++)
                if (!corridor.Contains((x, y))) geometry.BlockedCells.Add(y * 112 + x);
        var snapshot = new Snapshot { Tick = 12546,
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = 90, PositionY = 84,
                Life = 11776, LifeMaximum = 11776, CombatMode = PlayerCombatMode.Stand } },
            LevelGeometry = { geometry },
            Monsters = { new MonsterSnapshot { EntityId = 151326761, LevelId = 6, PositionX = 82, PositionY = 80,
                Alive = true, MonsterId = 46, HitPoints = 20, MaxHitPoints = 20 } },
        };
        return new(snapshot, new CommandResult { ClientSequence = 190, AppliedTick = 12546,
            Status = CommandStatus.Rejected, RejectReason = CommandRejectReason.InvalidTarget }, 190, 0);
    }

    private static int[] NativePath(Snapshot snapshot)
    {
        var player = snapshot.Players[0]; var geometry = snapshot.LevelGeometry[0];
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        bool Open(int x, int y) => (uint)x < geometry.Width && (uint)y < geometry.Height && !blocked.Contains(y * geometry.Width + x);
        return AuthoritativeNativePlayerPathfinder.FindPath(player.PositionX, player.PositionY, 66, 29, 100,
            (x, y) => Open(x, y) && !snapshot.Monsters.Any(monster => monster.Alive
                && (monster.PositionX == x && monster.PositionY == y
                    || monster.Moving && monster.FuturePositionX == x && monster.FuturePositionY == y)),
            (x, y, nx, ny) => x == nx || y == ny || Open(nx, y) && Open(x, ny));
    }
}

using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Fact]
    public void NativeCombatVersionFiveRetainsPendingArrowAndAnimationWithoutChangingOlderSchemas()
    {
        var source = NativeCombatState();
        var bytes = codec.Serialize(source);
        var restored = codec.Deserialize(bytes);
        Assert.Equal(bytes, codec.Serialize(restored));
        Assert.Equal(source.Projectiles, restored.Projectiles);
        Assert.Equal(source.CombatTargets, restored.CombatTargets);
        foreach (var prior in new[] { CreateState(), NativeState(0, 1), ItemState() }) {
            var oldBytes = codec.Serialize(prior);
            Assert.DoesNotContain("NativeWeaponArrow", oldBytes);
            Assert.DoesNotContain("NativeMeleeCursor", oldBytes);
            Assert.DoesNotContain("NativeWalkCursor", oldBytes);
            Assert.DoesNotContain("NativeWalkFrameCount", oldBytes);
            Assert.DoesNotContain("NativeAttackFrameCount", oldBytes);
            Assert.DoesNotContain("NativeHitRecoveryFrameCount", oldBytes);
            Assert.Equal(oldBytes, codec.Serialize(codec.Deserialize(oldBytes)));
        }
        Assert.Throws<InvalidDataException>(() => codec.Serialize(source with { FormatVersion = 4 }));
    }

    [Fact]
    public void NativeWalkingCohortRetainsPhaseAndRefusesOlderFormat()
    {
        var walking = NativeWalkingState();
        var bytes = codec.Serialize(walking);
        Assert.Equal(bytes, codec.Serialize(codec.Deserialize(bytes)));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(walking with {
            FormatVersion = 4, Projectiles = [],
        }));
    }

    [Theory]
    [InlineData("missing-phase")]
    [InlineData("bad-frame")]
    [InlineData("bad-tick")]
    [InlineData("stationary")]
    [InlineData("dead")]
    [InlineData("mixed-mode")]
    [InlineData("impossible-elapsed")]
    public void NativeWalkingCohortRejectsLostOrInvalidPhase(string defect)
    {
        var json = JsonNode.Parse(codec.Serialize(NativeWalkingState()))!.AsObject();
        var monster = json["CombatTargets"]![0]!["Monster"]!.AsObject();
        switch (defect) {
        case "missing-phase": monster.Remove("NativeWalkCursor"); break;
        case "bad-frame": monster["NativeWalkCursor"]!["FrameIndex"] = -2; break;
        case "bad-tick": monster["NativeWalkCursor"]!["TickCounter"] = 1; break;
        case "stationary": monster["Moving"] = false; break;
        case "impossible-elapsed": json["Tick"] = 0; monster["NativeWalkCursor"]!["FrameIndex"] = 5; break;
        case "dead": monster["HitPointsQ6"] = 0; break;
        case "mixed-mode": monster["NativeMeleeCursor"] = new JsonObject {
            ["FrameIndex"] = 2, ["TickCounter"] = 1, ["HitRecovery"] = false,
        }; break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    private static DiabloCohortState NativeWalkingState()
    {
        var state = NativeCombatState();
        var target = Assert.Single(state.CombatTargets);
        return state with {
            CombatTargets = [target with { Monster = target.Monster with {
                Moving = true, FuturePositionX = target.Monster.PositionX + 1,
                FuturePositionY = target.Monster.PositionY,
                MovementStartedTick = 1, MovementEffectTick = 9,
                NativeMeleeCursor = null, NativeWalkCursor = new(2, 0),
            } }],
        };
    }

    [Theory]
    [InlineData("missing-pending-field")]
    [InlineData("distance-overflow")]
    [InlineData("resolved-pending-hit")]
    [InlineData("foreign-owner")]
    [InlineData("wrong-level")]
    [InlineData("bad-frame")]
    [InlineData("bad-frame-tick")]
    [InlineData("missing-timing")]
    public void NativeCombatVersionFiveRejectsLostOrConflictingContinuation(string defect)
    {
        var json = JsonNode.Parse(codec.Serialize(NativeCombatState()))!.AsObject();
        var arrow = json["Projectiles"]![0]!.AsObject();
        var monster = json["CombatTargets"]![0]!["Monster"]!.AsObject();
        switch (defect) {
        case "missing-pending-field": arrow.Remove("NativeWeaponArrow"); break;
        case "distance-overflow": arrow["NativeWeaponArrow"]!["MissileDistance"] = uint.MaxValue; break;
        case "resolved-pending-hit": arrow["AttackHit"] = true; break;
        case "foreign-owner": arrow["SourceEntityId"] = 999; break;
        case "wrong-level": arrow["LevelId"] = 2; break;
        case "bad-frame": monster["NativeMeleeCursor"]!["FrameIndex"] = 6; break;
        case "bad-frame-tick": monster["NativeMeleeCursor"]!["TickCounter"] = 2; break;
        case "missing-timing": json["CombatTargets"]![0]!["NativeInitialization"]!.AsObject().Remove("NativeAttackFrameCount"); break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    private static DiabloCohortState NativeCombatState()
    {
        var original = ItemState();
        var target = Assert.Single(original.CombatTargets);
        return original with {
            FormatVersion = DiabloCohortState.NativeCombatFormatVersion,
            CombatTargets = [target with {
                Monster = target.Monster with { NativeMeleeCursor = new(2, 1) },
                NativeInitialization = target.NativeInitialization! with {
                    NativeAttackFrameCount = 6, NativeAttackTicksPerFrame = 2,
                    NativeWalkFrameCount = 8, NativeWalkTicksPerFrame = 1,
                    NativeHitRecoveryFrameCount = 4, NativeHitRecoveryTicksPerFrame = 1,
                },
            }],
            Projectiles = [new(0x80000000, original.Participants[0].EntityId, target.Monster.EntityId,
                0, 1, 1, 1, 2, 2, 0, AuthoritativeDamageType.Physical, 0, 254) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                NativeWeaponArrow = new(2), Speed = 32, VelocityX = 65536,
            }],
        };
    }
}

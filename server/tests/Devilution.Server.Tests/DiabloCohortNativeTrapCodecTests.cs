using System.Text.Json.Nodes;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Theory]
    [InlineData(DiabloNativeTrapDisposition.Armed)]
    [InlineData(DiabloNativeTrapDisposition.Disarmed)]
    [InlineData(DiabloNativeTrapDisposition.Fired)]
    public void NativeTrapV8RetainsOneShotOwnerAndFrozenArrow(DiabloNativeTrapDisposition disposition)
    {
        var source = NativeTrapState(disposition);
        var encoded = codec.Serialize(source);
        var restored = codec.Deserialize(encoded);
        Assert.Equal(encoded, codec.Serialize(restored));
        Assert.Equal(source.NativeTraps, restored.NativeTraps);
        Assert.Equal(source.Projectiles, restored.Projectiles);
        Assert.Throws<InvalidDataException>(() => codec.Serialize(source with { FormatVersion = 7 }));
        var v7 = codec.Serialize(NativeBarrelState(false));
        Assert.DoesNotContain("NativeTraps", v7);
        Assert.DoesNotContain("NativeTrapArrow", v7);
        Assert.Equal(v7, codec.Serialize(codec.Deserialize(v7)));
    }

    [Theory]
    [InlineData("missing-owner")]
    [InlineData("null-facts")]
    [InlineData("duplicate-owner")]
    [InlineData("wrong-source")]
    [InlineData("changed-link")]
    [InlineData("rearmed-trigger")]
    [InlineData("rearmed-owner")]
    [InlineData("wrong-type")]
    [InlineData("foreign-arrow")]
    [InlineData("duplicate-arrow")]
    [InlineData("changed-aim")]
    [InlineData("changed-velocity")]
    [InlineData("changed-flight")]
    [InlineData("changed-position")]
    [InlineData("changed-distance")]
    [InlineData("changed-hash")]
    [InlineData("resolved-payload")]
    [InlineData("dual-arrow-owner")]
    public void NativeTrapV8RejectsLostOrForgedContinuation(string defect)
    {
        var json = JsonNode.Parse(codec.Serialize(NativeTrapState(DiabloNativeTrapDisposition.Fired)))!.AsObject();
        var traps = json["NativeTraps"]!.AsArray();
        var trap = traps[0]!.AsObject();
        var arrow = json["Projectiles"]![0]!.AsObject();
        switch (defect) {
        case "missing-owner": json.Remove("NativeTraps"); break;
        case "null-facts": trap["Facts"] = null; break;
        case "duplicate-owner": traps.Add(trap.DeepClone()); break;
        case "wrong-source": trap["Facts"]!["SourceX"] = 31; break;
        case "changed-link": trap["Facts"]!["LinkX"] = 41; break;
        case "rearmed-trigger": json["Objects"]!.AsArray().Single(value => value!["NativeObjectId"]!.GetValue<uint>() == 5)!["TrapArmed"] = true; break;
        case "rearmed-owner": trap["Disposition"] = 0; trap["TrapVar4"] = 0; trap["TriggerTrapArmed"] = true; break;
        case "wrong-type": trap["Facts"]!["MissileTypeId"] = 1; break;
        case "foreign-arrow": arrow["SourceEntityId"] = 91; break;
        case "duplicate-arrow": var duplicate = arrow.DeepClone(); duplicate["EntityId"] = 0xE0000011U; json["Projectiles"]!.AsArray().Add(duplicate); break;
        case "changed-aim": arrow["TargetX"] = 42; break;
        case "changed-velocity": arrow["VelocityX"] = 1; break;
        case "changed-flight": arrow["TraveledY"] = 1; break;
        case "changed-position": arrow["PositionY"] = 50; break;
        case "changed-distance": arrow["NativeTrapArrow"]!["MissileDistance"] = 2; break;
        case "changed-hash": arrow["NativeTrapArrow"]!["LastCollisionTargetHash"] = 300; break;
        case "resolved-payload": arrow["Damage"] = 1; break;
        case "dual-arrow-owner": arrow["NativeWeaponArrow"] = new JsonObject { ["MissileDistance"] = 1 }; break;
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(json.ToJsonString()));
    }

    private static DiabloCohortState NativeTrapState(DiabloNativeTrapDisposition disposition)
    {
        var source = NativeBarrelState(false);
        var armed = disposition == DiabloNativeTrapDisposition.Armed;
        var facts = new AuthoritativeCathedralNativeTrapFacts(10, 0x09003010, 53, 1, 9,
            30, 30, 0x12345678, 40, 40, 0, 0, 11, 0x09003011, 5, 40, 40, true);
        var trap = new DiabloObjectState(facts.TrapEntityId, 0x09000036, 1, 30, 30, false, 0,
            AuthoritativeObjectEffectKind.None, 0) { NativeObjectId = 53 };
        var trigger = new DiabloObjectState(facts.TriggerEntityId, 0x09000006, 1, 40, 40,
            disposition == DiabloNativeTrapDisposition.Fired, 0,
            AuthoritativeObjectEffectKind.None, 0) { NativeObjectId = 5, TrapArmed = armed };
        var velocity = DiabloCombatCommandProcessor.NativeTrapArrowVelocity(30, 30, 40, 40);
        var position = DiabloCombatCommandProcessor.NativeTrapArrowTile(30, 30, velocity.X, velocity.Y);
        var arrow = new DiabloProjectileState(0xE0000010, facts.TrapEntityId, facts.TriggerEntityId,
            0, 1, position.X, position.Y, 40, 40, 0, AuthoritativeDamageType.Physical, 0, 255) {
            Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
            StartX = 30, StartY = 30, Speed = 32,
            VelocityX = velocity.X, VelocityY = velocity.Y, TraveledX = velocity.X, TraveledY = velocity.Y,
            OriginalDirection = (int)DeterministicGridDirections.Toward(30, 30, 40, 40),
            NativeTrapArrow = new(10, 1, 0),
        };
        return source with {
            FormatVersion = 8,
            Objects = source.Objects.Concat([trap, trigger]).OrderBy(value => value.EntityId).ToArray(),
            NativeTraps = [new(facts, armed ? 0 : 1, armed, disposition)],
            Projectiles = disposition == DiabloNativeTrapDisposition.Fired ? [arrow] : [],
        };
    }
}

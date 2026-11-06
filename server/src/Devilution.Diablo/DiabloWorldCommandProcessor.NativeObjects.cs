using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    /** Native objects.cpp OperateObject: direct family handlers; add each later operation here and in the owner's capability check. */
    private bool TryOperateNativeObject(AuthoritativePlayerState player, AuthoritativeWorldObject value,
        IReadOnlyList<AuthoritativePlayerState> participants, bool applyEffects, out DiabloWorldIntentResult result)
    {
        result = DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!world.OwnsNativePopulationLevel(value.LevelId))
            return false;
        switch (value.NativeObjectId) {
        case 66: case 76: case 81: case 82:
            result = applyEffects ? OperateFountains(player, value, participants) : DiabloWorldIntentResult.Accepted;
            return true;
        case 79: case 80:
            result = applyEffects ? OperateRandomShrine(player, value, participants) : DiabloWorldIntentResult.Accepted;
            return true;
        case 59: case 60:
            result = applyEffects ? OperateShrine(player, value, participants) : DiabloWorldIntentResult.Accepted;
            return true;
        case 1: case 2:
            result = !applyEffects || world.TryOperateCathedralDoor(value.EntityId, participants)
                ? DiabloWorldIntentResult.Accepted : DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            return true;
        case 42: case 43:
            result = !applyEffects || world.TryOperateNativeCatacombsDoor(value.EntityId, participants)
                ? DiabloWorldIntentResult.Accepted : DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            return true;
        case 48:
            if (!world.CanOperateNativeSarcophagus(value)) return false;
            if (!applyEffects) { result = DiabloWorldIntentResult.Accepted; return true; }
            var sarcophagus = world.TryOperateNativeCathedralSarcophagus(value, player, participants,
                () => AllocateWorldItemEntityId(participants));
            if (sarcophagus is not null) {
                random.Restore(sarcophagus.FinalRandomState);
                result = DiabloWorldIntentResult.Accepted;
            }
            return true;
        case 5: case 6: case 7: case 68: case 69: case 70:
            if (!world.CanOperateNativeChest(value)) return false;
            if (!applyEffects) { result = DiabloWorldIntentResult.Accepted; return true; }
            result = OperateChest(player, value, participants);
            return true;
        default:
            return false;
        }
    }

    /** Native objects.cpp OperateChest: reseed, ordered loot, then the intrinsic AddMissile (wall traps remain ProcessObjects work). */
    private DiabloWorldIntentResult OperateChest(AuthoritativePlayerState player, AuthoritativeWorldObject chest,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var mutation = world.TryOperateNativeCathedralChest(chest, player, participants,
            () => AllocateWorldItemEntityId(participants), out var trapPlan);
        if (mutation is null)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        random.Restore(mutation.FinalRandomState);
        if (trapPlan is not null) {
            var owner = world.NativePopulationForLevel(chest.LevelId)!;
            if (!owner.TryGetNativeChestFacts(chest, out var facts))
                throw new InvalidDataException("An opened native chest lost its generated slot.");
            var missile = DiabloCombatCommandProcessor.AddObjectTrapMissile(
                AllocateAuthoredProjectileEntityId(), chest.EntityId, chest.EntityId,
                chest.LevelId, facts!.ObjectSlot, trapPlan.SourceX, trapPlan.SourceY,
                trapPlan.TargetX, trapPlan.TargetY, trapPlan.Direction,
                trapPlan.Kind == AuthoritativeCatacombsChestTrapKind.Arrow ? 0 : 27,
                intrinsicChest: true, random);
            world.Projectiles.Add(missile.EntityId, missile);
        }
        return DiabloWorldIntentResult.Accepted;
    }
}

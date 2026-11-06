using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    /** Native objects.cpp FindValidShrine: the global stream chooses; the effect uses a fresh object-seeded generator. */
    private int FindValidShrine()
    {
        var availability = gameMode == DiabloGameMode.Multiplayer
            ? AuthoritativeNativeShrineAvailability.Multiplayer : AuthoritativeNativeShrineAvailability.SinglePlayer;
        for (;;) {
            var type = random.GenerateRnd(AuthoritativeNativeShrineAvailability.OriginalShrineCount);
            // All owned floors are Cathedral/Catacombs, so Enchanted is eligible in both families.
            if (type == 8 || !availability[type]) continue;
            return type;
        }
    }

    /** Native objects.cpp OperateGoatShrine/OperateCauldron. Their sounds do not draw the shrine sound's extra random value. */
    private DiabloWorldIntentResult OperateRandomShrine(AuthoritativePlayerState player,
        AuthoritativeWorldObject source, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var owner = world.NativePopulationForLevel(source.LevelId)!;
        if (!owner.TryGetNativeThemeEffectFacts(source, out var raw))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        random.Restore(raw.RndSeed);
        var type = FindValidShrine();
        source.Activated = true;
        if (raw.Type == 80) source.AnimationFrame = 3;
        return ApplyNativeShrineEffect(player, source, type, raw.RndSeed, participants);
    }

    /** Native objects.cpp OperateFountains. Resource fountains retain their selection region even at full resources. */
    private DiabloWorldIntentResult OperateFountains(AuthoritativePlayerState player,
        AuthoritativeWorldObject fountain, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var owner = world.NativePopulationForLevel(fountain.LevelId)!;
        if (!owner.TryGetNativeThemeEffectFacts(fountain, out var raw))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        player.EnableNativeResourceBases();
        switch (raw.Type) {
        case 66:
            player.DrinkNativeFountain(life: true);
            break;
        case 76:
            player.DrinkNativeFountain(life: false);
            break;
        case 81:
            fountain.Activated = true;
            nativeShrineMissile!(player, AuthoritativeProjectileBehavior.Infravision,
                2 * NativeShrineLevelType(fountain.LevelId), participants);
            break;
        case 82:
            fountain.Activated = true;
            var value = (raw.RndSeed >> 16) % 12;
            var from = value / 3;
            var to = value % 3;
            if (to >= from) to++;
            player.ModifyNativeAttribute((DiabloPlayerAttribute)(from + 1), -1);
            player.ModifyNativeAttribute((DiabloPlayerAttribute)(to + 1), 1);
            player.RecalculateEquipmentResources();
            break;
        }
        return DiabloWorldIntentResult.Accepted;
    }
}

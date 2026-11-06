using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** The journey selects a lasting native effect so a later fresh-host resume can prove its retention. */
public static class NativeShrineJourneyEvidence
{
    public static bool IsSeededMysterious(ObjectSnapshot shrine)
        => shrine.NativeObjectId is 59 or 60 && shrine.NativeShrineType == 0
            && shrine.LevelId == 5 && shrine.PositionX == 81 && shrine.PositionY == 65;

    public static bool CanDemonstrate(ObjectSnapshot shrine, PlayerSnapshot player)
        => shrine.NativeShrineInteractable && shrine.NativeShrineType switch {
            0 => true,
            9 => Level(player, 1) < 15,
            13 => player.Attributes.Magic.Base < 50,
            16 => Level(player, 4) < 15,
            19 => player.Attributes.Dexterity.Base < 60,
            20 => player.Attributes.Strength.Base < 250,
            21 => player.Attributes.Vitality.Base < 100,
            22 => !player.FullyExploredLevelIds.Contains(shrine.LevelId),
            23 => Level(player, 22) < 15,
            _ => false,
        };

    public static bool Changed(uint type, uint level, PlayerSnapshot before, PlayerSnapshot after)
        => type switch {
            0 or 13 or 19 or 20 or 21 => !Attributes(before).SequenceEqual(Attributes(after)),
            9 or 16 or 23 => !Spells(before).SequenceEqual(Spells(after)),
            22 => !before.FullyExploredLevelIds.Contains(level) && after.FullyExploredLevelIds.Contains(level),
            _ => false,
        };

    public static bool Retained(uint type, uint level, PlayerSnapshot after, PlayerSnapshot current)
        => type switch {
            0 or 13 or 19 or 20 or 21 => Attributes(after).SequenceEqual(Attributes(current)),
            9 or 16 or 23 => Spells(after).SequenceEqual(Spells(current)),
            22 => current.FullyExploredLevelIds.Contains(level),
            _ => false,
        };

    private static int[] Attributes(PlayerSnapshot player)
        => [player.Attributes.Strength.Base, player.Attributes.Magic.Base, player.Attributes.Dexterity.Base, player.Attributes.Vitality.Base];
    private static (uint, uint)[] Spells(PlayerSnapshot player)
        => player.MemorizedSpells.Concat(player.ZeroLevelMemorizedSpells).OrderBy(spell => spell.SpellId)
            .Select(spell => (spell.SpellId, spell.Level)).ToArray();
    private static uint Level(PlayerSnapshot player, uint spell)
        => player.MemorizedSpells.Concat(player.ZeroLevelMemorizedSpells).FirstOrDefault(value => value.SpellId == spell)?.Level ?? 0;
}

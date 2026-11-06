using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    private readonly List<DiabloDirectedGameplayEvent> pendingDirectedEvents = [];
    private Action<AuthoritativePlayerState, AuthoritativeProjectileBehavior, int, IReadOnlyList<AuthoritativePlayerState>>? nativeShrineMissile;

    internal void SetNativeShrineMissileHandler(
        Action<AuthoritativePlayerState, AuthoritativeProjectileBehavior, int, IReadOnlyList<AuthoritativePlayerState>> handler)
        => nativeShrineMissile = handler;

    internal IReadOnlyList<DiabloDirectedGameplayEvent> DrainDirectedEvents()
    {
        var events = pendingDirectedEvents.ToArray();
        pendingDirectedEvents.Clear();
        return events;
    }

    /** Native objects.cpp OperateShrine: consume the object before dispatching its seeded effect. */
    private DiabloWorldIntentResult OperateShrine(AuthoritativePlayerState player,
        AuthoritativeWorldObject shrine, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var owner = world.NativePopulationForLevel(shrine.LevelId)!;
        if (!owner.TryGetNativeShrineFacts(shrine, out var type, out var seed, out var finalFrame))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        // Native PlaySfxLoc(OperateShrine) chooses one of two sounds even in headless mode.
        random.GenerateRnd(2);
        shrine.Activated = true;
        shrine.AnimationFrame = finalFrame;
        return ApplyNativeShrineEffect(player, shrine, type, seed, participants);
    }

    /** Native objects.cpp OperateShrine. Goat shrines and cauldrons reuse this effect boundary. */
    internal DiabloWorldIntentResult ApplyNativeShrineEffect(AuthoritativePlayerState player,
        AuthoritativeWorldObject source, int shrineType, uint nativeRandomState,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (shrineType is < 0 or > 25)
            throw new InvalidDataException("Only original-Diablo shrine effects are supported.");
        player.EnableNativeResourceBases();
        foreach (var other in participants.Where(other => other.LevelId == player.LevelId)) other.EnableNativeResourceBases();
        var rng = new DeterministicLegacyRandom(nativeRandomState);
        var message = shrineType + 12;
        switch (shrineType) {
        case 0: OperateShrineMysterious(rng, player); break;
        case 1: OperateShrineHidden(rng, player); break;
        case 2: OperateShrineGloomy(player); break;
        case 3: OperateShrineWeird(player); break;
        case 4: case 11: OperateShrineMagical(player); message = 16; break;
        case 5: OperateShrineStone(player); break;
        case 6: OperateShrineReligious(player); break;
        case 7: OperateShrineEnchanted(rng, player); break;
        case 8: OperateShrineThaumaturgic(rng, source.LevelId); break;
        case 9: OperateShrineCostOfWisdom(player, 1); break;
        case 10: OperateShrineCryptic(player, participants); break;
        case 12: OperateShrineEldritch(player); break;
        case 13: OperateShrineEerie(player); break;
        case 14: OperateShrineDivine(player, source, participants); break;
        case 15: OperateShrineHoly(player, participants); break;
        case 16: OperateShrineCostOfWisdom(player, 30); break;
        case 17: OperateShrineSpiritual(rng, player); break;
        case 18:
            message = 30;
            foreach (var other in participants.Where(other => !ReferenceEquals(other, player)
                         && other.LevelId == player.LevelId)) {
                OperateShrineSpooky(other);
                pendingDirectedEvents.Add(new(other, ShrineMessage(player, source, 31)));
            }
            break;
        case 19: OperateShrineAbandoned(player); message = 32; break;
        case 20: OperateShrineCreepy(player); message = 33; break;
        case 21: OperateShrineQuiet(player); message = 34; break;
        case 22: OperateShrineSecluded(player); message = 35; break;
        case 23: OperateShrineCostOfWisdom(player, 31); message = 36; break;
        case 24: OperateShrineGlimmering(player); message = 37; break;
        case 25:
            message = 38;
            foreach (var other in participants.Where(other => !ReferenceEquals(other, player)
                         && other.LevelId == player.LevelId)) {
                // Each native peer starts its own generator at the object's seed.
                OperateShrineTainted(new DeterministicLegacyRandom(nativeRandomState), other);
                pendingDirectedEvents.Add(new(other, ShrineMessage(player, source, 39)));
            }
            break;
        }
        return DiabloWorldIntentResult.AcceptedWith(ShrineMessage(player, source, message));
    }

    private static DiabloGameplayEvent ShrineMessage(AuthoritativePlayerState player,
        AuthoritativeWorldObject shrine, int message)
        => new(DiabloGameplayEventKind.Dialogue, player.EntityId, shrine.EntityId, 0) {
            MessageId = message,
            Text = NativeShrineMessages.Get(message),
        };

    /** Native objects.cpp OperateShrineMysterious. */
    private static void OperateShrineMysterious(DeterministicLegacyRandom rng, AuthoritativePlayerState player)
    {
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Strength, -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Magic, -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Dexterity, -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Vitality, -1);
        player.ModifyNativeAttribute((DiabloPlayerAttribute)(rng.GenerateRnd(4) + 1), 6);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineMagical. AddManaShield only sets the persistent player flag. */
    private static void OperateShrineMagical(AuthoritativePlayerState player)
        => player.ManaShieldActive = true;

    /** Native objects.cpp OperateShrineThaumaturgic. */
    private void OperateShrineThaumaturgic(DeterministicLegacyRandom rng, uint levelId)
        => world.ResetNativeChests(levelId, rng);

    /** Native objects.cpp OperateShrineEerie. */
    private static void OperateShrineEerie(AuthoritativePlayerState player)
    {
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Magic, 2);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineAbandoned. */
    private static void OperateShrineAbandoned(AuthoritativePlayerState player)
    {
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Dexterity, 2);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineCreepy. */
    private static void OperateShrineCreepy(AuthoritativePlayerState player)
    {
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Strength, 2);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineQuiet. */
    private static void OperateShrineQuiet(AuthoritativePlayerState player)
    {
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Vitality, 2);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineSpooky: the activating player is excluded by the dispatcher. */
    private static void OperateShrineSpooky(AuthoritativePlayerState player)
    {
        // Native deviation for dead peers: Spooky leaves balances unchanged; Tainted retains
        // native attributes/maxima/bases but zero life. ApplyResurrect/RestartTownLvl overwrite current bases.
        if (player.Life <= 0 || player.CombatMode == DiabloPlayerCombatMode.Death) return;
        player.Life = player.LifeMaximum;
        player.Mana = player.ManaMaximum;
    }

    /** Native objects.cpp OperateShrineTainted: every other peer draws the same attribute. */
    private static void OperateShrineTainted(DeterministicLegacyRandom rng, AuthoritativePlayerState player)
    {
        var wasDead = player.Life <= 0 || player.CombatMode == DiabloPlayerCombatMode.Death;
        var attribute = rng.GenerateRnd(4);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Strength, attribute == 0 ? 1 : -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Magic, attribute == 1 ? 1 : -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Dexterity, attribute == 2 ? 1 : -1);
        player.ModifyNativeAttribute(DiabloPlayerAttribute.Vitality, attribute == 3 ? 1 : -1);
        player.RecalculateEquipmentResources();
        if (wasDead) player.KeepNativeDeathLifeZero();
    }

    /** Native objects.cpp OperateShrineSecluded: all 40 by 40 cells receive MAP_EXP_SHRINE. */
    private static void OperateShrineSecluded(AuthoritativePlayerState player)
        => player.FullyExploredLevelIds.Add(player.LevelId);

    /** Native objects.cpp OperateShrineCryptic. */
    private void OperateShrineCryptic(AuthoritativePlayerState player, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        nativeShrineMissile!(player, AuthoritativeProjectileBehavior.NovaController,
            2 * NativeShrineLevelType(player.LevelId), participants);
        player.Mana = player.ManaMaximum;
    }

    /** Native objects.cpp OperateShrineHoly. */
    private void OperateShrineHoly(AuthoritativePlayerState player, IReadOnlyList<AuthoritativePlayerState> participants)
        => nativeShrineMissile!(player, AuthoritativeProjectileBehavior.Phasing,
            2 * NativeShrineLevelType(player.LevelId), participants);

    private int NativeShrineLevelType(uint levelId)
        => world.NativePopulationForLevel(levelId)!.Family == AuthoritativeNativePopulationFamily.Cathedral ? 1 : 2;

    /** Native objects.cpp OperateShrineEnchanted. Native SpellsData has 37 vanilla entries. */
    private void OperateShrineEnchanted(DeterministicLegacyRandom rng, AuthoritativePlayerState player)
    {
        const int spellCount = 37;
        var known = player.MemorizedSpells.Select(entry => (Entry: entry, Definition: spells!.TryGet(entry.SpellId,
            out var definition) ? definition : null)).Where(entry => entry.Definition is { NativeSpellId: >= 1 and <= spellCount }).ToArray();
        if (known.Length > 1) {
            uint spellToReduce;
            do { spellToReduce = (uint)rng.GenerateRnd(spellCount) + 1; }
            while (!known.Any(entry => entry.Definition!.NativeSpellId == spellToReduce));
            for (uint spell = 1; spell < spellCount; spell++) {
                if (!spells!.TryGetByNativeId(spell, out var definition)) continue;
                var index = player.MemorizedSpells.FindIndex(entry => entry.SpellId == definition.SpellId);
                if (index >= 0 && player.MemorizedSpells[index].Level < 15 && spell != spellToReduce)
                    player.MemorizedSpells[index] = player.MemorizedSpells[index] with {
                        Level = player.MemorizedSpells[index].Level + 1,
                    };
            }
            var reduced = known.Single(entry => entry.Definition!.NativeSpellId == spellToReduce).Entry;
            var reducedIndex = player.MemorizedSpells.FindIndex(entry => entry.SpellId == reduced.SpellId);
            if (player.MemorizedSpells[reducedIndex].Level > 0)
                player.MemorizedSpells[reducedIndex] = player.MemorizedSpells[reducedIndex] with {
                    Level = player.MemorizedSpells[reducedIndex].Level - 1,
                };
            RefreshNativeItemStatFlags(player);
        }
    }

    /** Native objects.cpp OperateShrineCostOfWisdom. */
    private void OperateShrineCostOfWisdom(AuthoritativePlayerState player, uint nativeSpell)
    {
        if (spells?.TryGetByNativeId(nativeSpell, out var spell) != true)
            throw new InvalidDataException($"Native shrine spell {nativeSpell} is absent.");
        var index = player.MemorizedSpells.FindIndex(entry => entry.SpellId == spell.SpellId);
        if (index < 0) player.MemorizedSpells.Add(new(spell.SpellId, 2));
        else if (player.MemorizedSpells[index].Level < 15)
            player.MemorizedSpells[index] = player.MemorizedSpells[index] with {
                Level = Math.Min(15, player.MemorizedSpells[index].Level + 2),
            };
        RefreshNativeItemStatFlags(player);
        player.ApplyNativeWisdomPenalty();
    }
}

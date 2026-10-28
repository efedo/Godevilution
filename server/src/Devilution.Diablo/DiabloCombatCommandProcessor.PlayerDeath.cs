using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Player death drops, hits, blocks, resistances and damage application (native player.cpp StartPlayerKill/ApplyPlrDamage). */
public sealed partial class DiabloCombatCommandProcessor
{
    private void StartPlayerDeath(
        AuthoritativePlayerState player,
        uint sourceEntityId,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        PlayerDeathReason deathReason)
    {
        player.NativeWalkPath = null;
        player.NativeQueuedRangedAttack = null;
        DiabloPlayerActionLifecycle.Cancel(player);
        DropHeldItemOnDeath(player, players);
        if (!SuppressesDeathDrops(player.LevelId)) {
            DropDeathGold(player, players);
            if (deathReason == PlayerDeathReason.MonsterOrTrap)
                DropEquippedItemsOnDeath(player, players);
            else if (deathReason == PlayerDeathReason.Player)
                DropPvpEarOnDeath(player, players);
        }
        player.CombatMode = DiabloPlayerCombatMode.Death;
        player.CombatModeEndTick = 0;
        if (!useNativePlayerActionTiming)
            return;

        var deathFrames = playerAnimations!
            .GetRequired(player.CharacterClassId)
            .DeathFrames;
        var endTick = checked(tick + (ulong)(deathFrames * 2 - 1));
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Death,
                tick,
                tick,
                endTick,
                endTick) {
                EffectApplied = true,
                TargetEntityId = sourceEntityId,
            },
            DiabloPlayerCombatMode.Death);
    }

    private bool SuppressesDeathDrops(uint levelId)
    {
        return gameMode == DiabloGameMode.Multiplayer
            && world.SuppressMultiplayerDeathDrops(levelId);
    }

    private void DropHeldItemOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (player.HeldItem is not { } heldItem)
            return;
        player.HeldItem = null;
        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var entityId = AllocateWorldItemEntityId(players);
        world.AddWorldItem(new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            heldItem.ItemSeed,
            heldItem.Price,
            heldItem.State));
    }

    private void DropDeathGold(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var droppedGold = player.Gold / checked((uint)rules.DeathGoldDivisor);
        if (DiabloGoldInventory.TryGetTopologyTotal(
                player,
                rules,
                allowTruncatedWalletRemainder: false,
                out _,
                items)) {
            var remainingGold = DropInventoryGold(
                player,
                players,
                droppedGold,
                skipFullStacks: true);
            if (remainingGold > 0) {
                DropInventoryGold(
                    player,
                    players,
                    remainingGold,
                    skipFullStacks: false);
            }
            player.Gold /= checked((uint)rules.DeathGoldDivisor);
            return;
        }

        player.Gold /= checked((uint)rules.DeathGoldDivisor);
        while (droppedGold > 0) {
            var stackValue = Math.Min(
                droppedGold,
                checked((uint)rules.DeathGoldMaximumStack));
            var itemSeed = AllocateWorldItemSeed();
            DropGoldWorldItem(
                player,
                players,
                itemSeed,
                stackValue,
                DiabloGoldInventory.CreateState(stackValue, rules, items));
            droppedGold -= stackValue;
        }
    }

    private void DropEquippedItemsOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var equipment = player.Equipment.ToArray();
        player.Equipment.Clear();
        for (uint slot = 0; slot < 7; slot++) {
            var item = equipment.FirstOrDefault(candidate => candidate.Slot == slot);
            if (item is null)
                continue;
            var preferredDirection = (DiabloPlayerDirection)DeterministicGridDirections.RotateLeft(
                (DeterministicGridDirection)player.FacingDirection,
                checked((int)slot + 1));
            var position = FindDeathDropPosition(player, players, preferredDirection);
            if (!position.HasValue)
                continue;
            var entityId = AllocateWorldItemEntityId(players);
            world.AddWorldItem(new AuthoritativeWorldItem(
                entityId,
                player.LevelId,
                position.Value.X,
                position.Value.Y,
                item.ItemSeed,
                checked((uint)Math.Max(0, item.State.IdentifiedValue)),
                item.State));
        }
    }

    private void DropPvpEarOnDeath(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (items is null
            || playerClasses is null
            || string.IsNullOrEmpty(player.CharacterName)) {
            return;
        }

        var definition = items.GetRequired(rules.PvpEarItemId);
        var (createInfo, itemSeed) = DiabloPlayerName.EncodeEarIdentity(
            player.CharacterName);
        if (world.WorldItems.Values.Any(item =>
                item.ItemSeed == itemSeed
                && items.IsStateForDefinition(item.State, definition)
                && item.State.CreateInfo == createInfo)) {
            return;
        }

        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var playerClass = playerClasses.GetRequired(player.CharacterClassId);
        var state = definition.Generate(1) with {
            CreateInfo = createInfo,
            Value = checked((int)player.CharacterLevel),
            BaseName = $"Ear of {player.CharacterName}",
            IdentifiedName = player.CharacterName,
            NativeCursorGraphic = playerClass.PvpEarCursorGraphic,
        };
        var entityId = AllocateWorldItemEntityId(players);
        world.AddWorldItem(new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            checked((uint)Math.Max(0, state.IdentifiedValue)),
            state));
    }

    private uint DropInventoryGold(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint amount,
        bool skipFullStacks)
    {
        for (var index = 0; index < player.Inventory.Count && amount > 0; index++) {
            var item = player.Inventory[index];
            if (!DiabloGoldInventory.IsValidStack(item, rules, items)
                || (skipFullStacks
                    && item.Price == checked((uint)rules.DeathGoldMaximumStack))) {
                continue;
            }

            if (amount < item.Price) {
                var itemSeed = AllocateWorldItemSeed();
                DropGoldWorldItem(
                    player,
                    players,
                    itemSeed,
                    amount,
                    DiabloGoldInventory.CreateState(amount, rules, items));
                player.Inventory[index] = DiabloGoldInventory.WithValue(
                    item,
                    item.Price - amount,
                    rules,
                    items);
                return 0;
            }

            amount -= item.Price;
            DropGoldWorldItem(
                player,
                players,
                item.ItemSeed,
                item.Price,
                item.State);
            DiabloPlayerItemAccess.Remove(
                player,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    checked((uint)index)));
            index = -1;
        }
        return amount;
    }

    private void DropGoldWorldItem(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint itemSeed,
        uint stackValue,
        AuthoritativeItemState state)
    {
        var position = FindDeathDropPosition(player, players);
        if (!position.HasValue)
            return;
        var entityId = AllocateWorldItemEntityId(players);
        world.AddWorldItem(new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            stackValue,
            state));
    }

    private (int X, int Y)? FindDeathDropPosition(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        DiabloPlayerDirection? preferredDirection = null)
    {
        if (preferredDirection.HasValue) {
            var displacement = DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)preferredDirection.Value);
            var preferredX = checked(player.PositionX + displacement.X);
            var preferredY = checked(player.PositionY + displacement.Y);
            if (IsDeathDropPositionAvailable(player, players, preferredX, preferredY))
                return (preferredX, preferredY);
        }
        return DeterministicGridCrawl.FindClosestValidPosition(
            player.PositionX,
            player.PositionY,
            1,
            rules.DeathDropSearchRadius,
            (positionX, positionY) => IsDeathDropPositionAvailable(
                player,
                players,
                positionX,
                positionY));
    }

    private bool IsDeathDropPositionAvailable(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        int positionX,
        int positionY)
    {
        if (world.HasNativeItemLevel(player.LevelId))
            return world.CanPutNativeItem(player.LevelId, positionX, positionY);
        return world.IsWalkable(player.LevelId, positionX, positionY)
            && !world.IsCathedralDoorItemBlocked(player.LevelId, positionX, positionY)
            && !world.IsOccupied(player.LevelId, positionX, positionY, players)
            && !world.WorldItems.Values.Any(item =>
                item.LevelId == player.LevelId
                && item.PositionX == positionX
                && item.PositionY == positionY);
    }

    private void UpdateFacingForCast(
        AuthoritativePlayerState player,
        DiabloCastIntent intent,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (spells.TryGet(intent.SpellId, out var spell)
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Phasing) {
            return;
        }
        if (intent.TargetEntityId != 0) {
            var playerTarget = players.FirstOrDefault(candidate =>
                candidate.EntityId == intent.TargetEntityId);
            if (playerTarget is not null) {
                UpdateFacingToward(player, playerTarget.PositionX, playerTarget.PositionY);
                return;
            }
            if (world.CombatTargets.TryGetValue(intent.TargetEntityId, out var combatTarget)) {
                UpdateFacingToward(player, combatTarget.PositionX, combatTarget.PositionY);
                return;
            }
        }
        UpdateFacingToward(player, intent.TargetX, intent.TargetY);
    }

    private static void UpdateFacingToward(
        AuthoritativePlayerState player,
        int targetX,
        int targetY)
    {
        if (targetX == player.PositionX && targetY == player.PositionY)
            return;
        player.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.Toward(
            player.PositionX,
            player.PositionY,
            targetX,
            targetY);
    }

    private uint AllocateWorldItemSeed()
    {
        uint itemSeed;
        do {
            itemSeed = unchecked((uint)random.AdvanceRndSeed());
        } while (itemSeed == 0);
        return itemSeed;
    }

    private uint AllocateWorldItemEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return worldItemEntityIds.Allocate(entityId =>
            players.Any(player => player.EntityId == entityId)
            || world.IsEntityIdOccupied(entityId));
    }

    private void StartBlock(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        ulong tick)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        var blockingFrames = GetPlayerAnimation(player)?.BlockingFrames ?? 2;
        var skippedFrames = HasUsableEquipmentFlag(player, FastBlockFlag)
            ? blockingFrames - rules.EquipmentFastBlockFrameSkip
            : IsNativeFastBlockingClass(player, playerClass)
                ? blockingFrames - playerClass!.FastBlockFrameSkip
                : 0;
        var duration = checked((blockingFrames - 1 - skippedFrames) * 3 + 1);
        player.CombatMode = DiabloPlayerCombatMode.Block;
        player.CombatModeEndTick = checked(tick + (ulong)duration);
    }

    private static bool ShouldStartHit(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        int damageQ6)
    {
        var threshold = playerClass?.Flags.HasFlag(
                DiabloPlayerClassFlags.HeavyHitThreshold) == true
            ? playerClass.CalculateHeavyHitThreshold(player.CharacterLevel)
            : checked((int)player.CharacterLevel);
        return (damageQ6 >> 6) >= threshold;
    }

    private void StartHit(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        ulong tick)
    {
        DiabloPlayerActionLifecycle.Cancel(player);
        var recoveryFrames = GetPlayerAnimation(player)?.RecoveryFrames ?? 6;
        var flags = GetUsableEquipmentFlags(player);
        var skippedFrames = (flags & FastestHitRecoveryFlag) != 0
            ? rules.FastestHitRecoveryFrameSkip
            : (flags & FasterHitRecoveryFlag) != 0
                ? rules.FasterHitRecoveryFrameSkip
                : (flags & FastHitRecoveryFlag) != 0
                    ? rules.FastHitRecoveryFrameSkip
                    : 0;
        var duration = Math.Max(1, recoveryFrames - skippedFrames);
        player.CombatMode = DiabloPlayerCombatMode.GotHit;
        player.CombatModeEndTick = checked(tick + (ulong)duration);
    }

    private AuthoritativePlayerClassDefinition? GetPlayerClass(
        AuthoritativePlayerState player)
    {
        return playerClasses is not null
            && playerClasses.TryGet(player.CharacterClassId, out var playerClass)
            ? playerClass
            : null;
    }

    private AuthoritativePlayerAnimationDefinition? GetPlayerAnimation(
        AuthoritativePlayerState player)
    {
        return playerAnimations is not null
            && playerAnimations.TryGet(player.CharacterClassId, out var animation)
            ? animation
            : null;
    }

    private DiabloPlayerAttackAnimationProfile GetAttackAnimationProfile(
        AuthoritativePlayerState player)
    {
        var hands = player.Equipment
            .Where(item => item.Slot is 4 or 5 && IsUsable(item.State))
            .OrderBy(item => item.Slot)
            .ToArray();
        var hasShield = hands.Any(item => AuthoritativeItemClassification.IsShield(item.State, items));
        var weapon = hands.FirstOrDefault(item => AuthoritativeItemClassification.IsWeapon(item.State, items)
            && !AuthoritativeItemClassification.IsShield(item.State, items));
        if (weapon is null)
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.UnarmedShield
                : DiabloPlayerAttackAnimationProfile.Unarmed;

        var state = weapon.State;
        if (AuthoritativeItemClassification.IsSword(state, items))
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.SwordShield
                : DiabloPlayerAttackAnimationProfile.Sword;
        if (AuthoritativeItemClassification.IsAxe(state, items))
            return DiabloPlayerAttackAnimationProfile.Axe;
        if (AuthoritativeItemClassification.IsBow(state, items))
            return DiabloPlayerAttackAnimationProfile.Bow;
        if (AuthoritativeItemClassification.IsMace(state, items))
            return hasShield
                ? DiabloPlayerAttackAnimationProfile.MaceShield
                : DiabloPlayerAttackAnimationProfile.Mace;
        if (AuthoritativeItemClassification.IsStaff(state, items))
            return DiabloPlayerAttackAnimationProfile.Staff;
        return hasShield
            ? DiabloPlayerAttackAnimationProfile.UnarmedShield
            : DiabloPlayerAttackAnimationProfile.Unarmed;
    }

    private int GetAttackSkippedFrames(
        AuthoritativePlayerState player,
        bool ranged,
        bool includesFirstFrame)
    {
        var flags = GetUsableEquipmentFlags(player);
        if (ranged) {
            var skippedFrames = (flags & (QuickAttackFlag | FastAttackFlag)) != 0
                ? includesFirstFrame
                    ? rules.RangedAttackFrameSkipWithFirstFrame
                    : rules.RangedAttackFrameSkipWithoutFirstFrame
                : 0;
            if ((flags & FastAttackFlag) != 0)
                skippedFrames += rules.RangedFastAttackFrameSkipBonus;
            return skippedFrames;
        }
        if ((flags & FastestAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FastestAttackFrameSkipWithFirstFrame
                : rules.FastestAttackFrameSkipWithoutFirstFrame;
        if ((flags & FasterAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FasterAttackFrameSkipWithFirstFrame
                : rules.FasterAttackFrameSkipWithoutFirstFrame;
        if ((flags & FastAttackFlag) != 0)
            return includesFirstFrame
                ? rules.FastAttackFrameSkipWithFirstFrame
                : rules.FastAttackFrameSkipWithoutFirstFrame;
        if ((flags & QuickAttackFlag) != 0)
            return includesFirstFrame
                ? rules.QuickAttackFrameSkipWithFirstFrame
                : rules.QuickAttackFrameSkipWithoutFirstFrame;
        return 0;
    }

    private int GetLightningResistance(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass)
    {
        return GetPlayerResistance(
            player,
            playerClass,
            AuthoritativeDamageType.Lightning);
    }

    private int GetPlayerResistance(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass,
        AuthoritativeDamageType damageType)
    {
        var naturalResistanceBonus = playerClass?.Flags.HasFlag(
            DiabloPlayerClassFlags.NaturalResistance) == true
            ? playerClass.CalculateNaturalResistanceBonus(player.CharacterLevel)
            : 0;
        return AuthoritativeEquipmentResistanceRules.Calculate(
            player.Equipment,
            damageType,
            naturalResistanceBonus,
            rules.MaximumResistance);
    }

    private bool CanBlock(AuthoritativePlayerState player)
    {
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        if (IsUsableShield(left) || IsUsableShield(right))
            return true;

        var playerClass = GetPlayerClass(player);
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.FlexibleBlock) != true)
            return false;
        if (IsUsableStaff(left) || IsUsableStaff(right))
            return true;
        if (left is null && right is null)
            return true;
        return left is not null
            && right is null
            && AuthoritativeItemClassification.IsWeapon(left.State, items)
            && !AuthoritativeItemClassification.IsTwoHanded(left.State, items)
            || right is not null
            && left is null
            && AuthoritativeItemClassification.IsWeapon(right.State, items)
            && !AuthoritativeItemClassification.IsTwoHanded(right.State, items);
    }

    /** Applies the native player-versus-player shield/class block boundary. */
    private bool TryResolvePlayerBlock(
        AuthoritativePlayerState attacker,
        AuthoritativePlayerState target,
        ulong tick)
    {
        if (target.CombatMode is not (DiabloPlayerCombatMode.Stand
                or DiabloPlayerCombatMode.Attack)
            || !CanBlock(target))
            return false;

        var targetClass = GetPlayerClass(target);
        var blockRoll = random.RandomIntLessThan(100);
        var blockChance = Math.Clamp(
            target.Attributes.Dexterity.Current
                + (targetClass?.BaseBlockChance ?? 0)
                + checked((int)target.CharacterLevel * rules.PvpBlockLevelScale)
                - checked((int)attacker.CharacterLevel * rules.PvpBlockLevelScale),
            0,
            100);
        if (blockRoll >= blockChance)
            return false;

        StartBlock(target, targetClass, tick);
        return true;
    }

    private static bool HasUsableEquipmentFlag(AuthoritativePlayerState player, uint flag)
    {
        return (GetUsableEquipmentFlags(player) & flag) != 0;
    }

    private static bool HasUsableHellfireDamageArmorFlag(
        AuthoritativePlayerState player,
        int flag)
    {
        return player.Equipment.Any(item => {
            var state = item.State;
            return IsUsable(state)
                && (state.Magical == 0 || state.Identified)
                && (state.HellfireDamageArmorFlags & flag) != 0;
        });
    }

    private static uint GetUsableEquipmentFlags(AuthoritativePlayerState player)
    {
        return player.Equipment
            .Where(item => IsUsable(item.State)
                && (item.State.Magical == 0 || item.State.Identified))
            .Aggregate(0U, (flags, item) => flags | item.State.Flags);
    }

    private bool IsNativeFastBlockingClass(
        AuthoritativePlayerState player,
        AuthoritativePlayerClassDefinition? playerClass)
    {
        if (playerClass?.Flags.HasFlag(DiabloPlayerClassFlags.FastBlock) != true)
            return false;
        var left = player.Equipment.FirstOrDefault(item => item.Slot == 4);
        var right = player.Equipment.FirstOrDefault(item => item.Slot == 5);
        return IsUsableStaff(left) || IsUsableStaff(right);
    }

    private bool IsUsableShield(EquippedStoreItem? item)
    {
        return item is not null
            && AuthoritativeItemClassification.IsShield(item.State, items)
            && IsUsable(item.State);
    }

    private bool IsUsableStaff(EquippedStoreItem? item)
    {
        return item is not null
            && AuthoritativeItemClassification.IsStaff(item.State, items)
            && IsUsable(item.State);
    }

    private static bool IsUsable(AuthoritativeItemState item)
    {
        return item.StatFlag;
    }

    private bool IsPlayerProtectedByFlash(AuthoritativePlayerState player)
    {
        return world.Projectiles.Values.Any(projectile =>
            projectile.Behavior is AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
            && IsProjectileOwner(projectile, player)
            && spells.TryGet(projectile.SpellId, out var spell)
            && spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash
            && projectile.RemainingTicks < spell.ProjectileLifetime);
    }

    private static bool IsPlayerEtherealized(AuthoritativePlayerState player)
    {
        return player.StatusEffects.Any(effect =>
            effect.Behavior == AuthoritativeStatusEffectBehavior.Etherealize);
    }

    private static bool IsProjectileOwner(
        AuthoritativeProjectile projectile,
        AuthoritativePlayerState player)
    {
        return player.EntityId == projectile.SourceEntityId
            || projectile.SourceEntityId == 1 && player.EntityId == 0;
    }

    private bool TryResolveChargedBoltHit(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (target.LightningResistance >= 100)
            return false;
        var owner = players.FirstOrDefault(player => player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return false;

        var hitMonsterLevelScale = 2;
        var minimumHitChance = 5;
        var maximumHitChance = 95;
        if (spells.TryGet(projectile.SpellId, out var chargedBoltSpell)
            && chargedBoltSpell.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt) {
            hitMonsterLevelScale = chargedBoltSpell.ChargedBoltHitMonsterLevelScale;
            minimumHitChance = chargedBoltSpell.ChargedBoltMinimumHitChance;
            maximumHitChance = chargedBoltSpell.ChargedBoltMaximumHitChance;
        }
        var baseMagicToHit = 50;
        if (playerClasses is not null
            && playerClasses.TryGet(owner.CharacterClassId, out var playerClass)) {
            baseMagicToHit = playerClass.BaseMagicToHit;
        }
        var hitChance = Math.Clamp(
            owner.Attributes.Magic.Current
                + baseMagicToHit
                - GetEffectiveMonsterLevel(target) * hitMonsterLevelScale,
            minimumHitChance,
            maximumHitChance);
        var hitRoll = random.RandomIntLessThan(100);
        if (!target.IsPetrified && hitRoll >= hitChance)
            return false;

        var damageQ6 = DeterministicFixedPointQ6.FromWhole(
            random.RandomIntBetween(projectile.Damage, projectile.Damage));
        if (target.LightningResistance > 0)
            damageQ6 /= projectile.ResistantDamageDivisor;
        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyResolvedDamageQ6(owner, target, damageQ6, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
        return true;
    }

    private static int GetDirection(
        int startX,
        int startY,
        int destinationX,
        int destinationY)
        => (int)DeterministicGridDirections.Toward(
            startX,
            startY,
            destinationX,
            destinationY);

    private static (int X, int Y) CalculateVelocity(
        int startX,
        int startY,
        int destinationX,
        int destinationY,
        int speed)
        => DeterministicGridVelocity.CalculateNative(
            startX,
            startY,
            destinationX,
            destinationY,
            speed);

    private void ResolveProjectileDamage(
        AuthoritativeProjectile projectile,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> players,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        var owner = players.FirstOrDefault(player => player.EntityId == projectile.SourceEntityId
            || (projectile.SourceEntityId == 1 && player.EntityId == 0));
        if (owner is null)
            return;

        var gameplayEvents = new List<DiabloGameplayEvent>();
        ApplyDamage(owner, target, projectile.Damage, projectile.DamageType, gameplayEvents);
        foreach (var gameplayEvent in gameplayEvents)
            events.Add(new DiabloDirectedGameplayEvent(owner, gameplayEvent));
    }

    private void ApplyDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int baseDamage,
        AuthoritativeDamageType damageType,
        ICollection<DiabloGameplayEvent> events)
    {
        var damage = rules.ResolveDamage(
            baseDamage,
            GetEffectiveMonsterArmorClass(target),
            damageType,
            target);
        ApplyResolvedDamage(player, target, damage, events);
    }

    private void ApplyResolvedDamage(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damage,
        ICollection<DiabloGameplayEvent> events)
    {
        ApplyResolvedDamageQ6(
            player,
            target,
            DeterministicFixedPointQ6.FromWhole(damage),
            events);
    }

    private void ApplyResolvedDamageQ6(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        int damageQ6,
        ICollection<DiabloGameplayEvent> events,
        bool nativeTrap = false,
        bool nativeTrapResisted = false)
    {
        var wasAlive = target.HitPointsQ6 > 0;
        // Native startHit(player, damage) tags every admitted hit, including a
        // zero-damage resisted hit. Trap hits never add the barrel breaker.
        if (wasAlive && !nativeTrap && world.OwnsNativePopulationLevel(target.LevelId))
            target.RecordNativePlayerHit();
        var nativeFatalTrap = nativeTrap && wasAlive && target.HitPointsQ6 - damageQ6 < 64;
        var lootPlayers = activePlayers.Count == 0 ? new[] { player } : activePlayers;
        AuthoritativeCathedralItemDeathTransaction? nativeLoot = null;
        uint? nativeExperienceReward = null;
        if (wasAlive && (damageQ6 >= target.HitPointsQ6 || nativeFatalTrap)
            && world.HasNativeItemLevel(target.LevelId)) {
            nativeExperienceReward = target.IsPlayerMinion
                || nativeTrap && (!target.NativePlayerHitRecorded || player.Life < 64)
                ? 0 : nativeTrap ? GetNativeTrapExperienceReward(player, target)
                    : GetEffectiveMonsterExperienceReward(target);
            var resources = DiabloPlayerProgression.PreviewExperienceResources(player, nativeExperienceReward.Value, experience);
            nativeLoot = world.PrepareCathedralDeathLoot(target, player, lootPlayers,
                new(true, false, resources.Mana, resources.Life));
            // Native MonsterDeath grants experience before HP0, reseed and SpawnLoot.
            // The prepared context previews level-up resources while validation remains non-mutating.
            DiabloPlayerProgression.GrantExperience(player, nativeExperienceReward.Value, experience);
        }
        target.ApplyDamageQ6(damageQ6);
        if (nativeFatalTrap && target.HitPointsQ6 > 0)
            target.ApplyDamageQ6(target.HitPointsQ6);
        if (OwnsNativeMonsterTiming(target)) {
            if (target.HitPointsQ6 <= 0) {
                world.CancelNativeCatacombsRangedAction(target.EntityId);
                InterruptNativeMelee(target);
                world.InterruptNativeInitialSpecialStand(target.EntityId);
                world.InterruptNativeCatacombsGargoyle(target.EntityId);
                world.InterruptNativeSarcophagusSpecialStand(target);
                world.InterruptNativeBarrelSpecialStand(target);
            } else if (!nativeTrapResisted && damageQ6 > 0 && !target.IsPetrified
                && (DeterministicFixedPointQ6.ToWholeTruncated(damageQ6) >= GetEffectiveMonsterLevel(target) + 3
                    || target.NativeAi is "Sneak" or "Stalker" or "Unseen" or "IllWeav")) {
                target.CancelMovement();
                BeginNativeHitRecovery(target);
                world.CancelNativeCatacombsRangedAction(target.EntityId);
                world.InterruptNativeInitialSpecialStand(target.EntityId);
                world.InterruptNativeCatacombsGargoyle(target.EntityId);
                world.InterruptNativeSarcophagusSpecialStand(target);
                world.InterruptNativeBarrelSpecialStand(target);
            }
        }
        if (damageQ6 > 0) {
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Damage,
                player.EntityId,
                target.EntityId,
                DeterministicFixedPointQ6.ToWholeTruncated(damageQ6)) {
                AmountQ6 = damageQ6,
            });
        }
        if (wasAlive
            && target.HitPointsQ6 > 0
            && damageQ6 > 0 && !nativeTrapResisted
            && GetSneakTuning(target) is { } sneakTuning) {
            target.StartSneakRetreat(sneakTuning.RetreatTicks);
        }
        if (!wasAlive || target.HitPointsQ6 != 0)
            return;

        // Native MonsterDeath restores rndItemSeed and spawns loot before
        // clearing moving reservations or changing the monster's tile.
        if (nativeLoot is not null) {
            random.Restore(world.CommitCathedralDeathLoot(nativeLoot,
                () => AllocateWorldItemEntityId(lootPlayers)));
            // MonsterDeath randomizes its death sound after SpawnLoot, even muted.
            if (nativeTrap && !target.IsDiablo)
                random.AdvanceRndSeed();
        }

        target.CancelMovement();
        target.BeginDeathAnimation();
        TriggerFallenFear(target);

        AdvanceQuestForMonsterDefeat(player, target);
        if (target.IsDiablo) {
            world.VictoryTriggered = true;
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Victory,
                player.EntityId,
                target.EntityId,
                checked((int)player.DiabloKillLevel)));
        }

        var experienceReward = nativeExperienceReward ?? GetEffectiveMonsterExperienceReward(target);
        if (nativeExperienceReward is null)
            DiabloPlayerProgression.GrantExperience(player, experienceReward, experience);
        if (!nativeTrap || experienceReward != 0)
            events.Add(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                player.EntityId,
                player.EntityId,
                checked((int)experienceReward)));
        if (nativeLoot is null && target.Drop is not null
            && FindAvailableWorldItemPosition(
                target.LevelId,
                target.PositionX,
                target.PositionY,
                target.EntityId) is { } dropPosition) {
            world.SetWorldItem(new AuthoritativeWorldItem(
                target.Drop.EntityId,
                target.LevelId,
                dropPosition.X,
                dropPosition.Y,
                target.Drop.ItemSeed,
                target.Drop.Price,
                target.Drop.State));
        }
        if (!target.IsPlayerMinion && target.DeathAnimationTicks == 0)
            ReplaceDeadMonsterWithCorpse(target);
    }

    private void TriggerFallenFear(AuthoritativeCombatTarget deceased)
    {
        var deceasedTuning = GetFallenFearTuning(deceased);
        if (!deceasedTuning.HasValue)
            return;
        foreach (var fallen in world.CombatTargets.Values
                     .Where(candidate => {
                         var tuning = GetFallenFearTuning(candidate);
                         return candidate.EntityId != deceased.EntityId
                             && tuning.HasValue
                             && candidate.HitPointsQ6 > 0
                             && (candidate.LevelId == 0 || candidate.LevelId == deceased.LevelId)
                              && DeterministicGridMetrics.ChebyshevDistance(
                                  candidate.PositionX,
                                  candidate.PositionY,
                                  deceased.PositionX,
                                  deceased.PositionY)
                                  <= deceasedTuning.Value.Radius;
                     })
                     .OrderBy(candidate => candidate.EntityId)) {
            var tuning = GetFallenFearTuning(fallen)!.Value;
            fallen.StartFallenFear(
                Math.Max(
                    tuning.LevelOffset - GetEffectiveMonsterLevel(fallen),
                    tuning.MinimumTicks),
                GetDirection(
                    deceased.PositionX,
                    deceased.PositionY,
                    fallen.PositionX,
                    fallen.PositionY));
        }
    }

    private void TriggerFallenRally(AuthoritativeCombatTarget leader)
    {
        var radius = checked((2 * leader.Intelligence) + 4);
        var rallyTicks = checked((30 * leader.Intelligence) + 105);
        foreach (var fallen in world.CombatTargets.Values
                     .Where(candidate =>
                         UsesNativeAi(candidate, "Fallen")
                         && candidate.HitPointsQ6 > 0
                         && (candidate.LevelId == 0 || candidate.LevelId == leader.LevelId)
                          && DeterministicGridMetrics.ChebyshevDistance(
                              candidate.PositionX,
                              candidate.PositionY,
                              leader.PositionX,
                              leader.PositionY) <= radius)
                     .OrderBy(candidate => candidate.EntityId)) {
            fallen.StartFallenRally(rallyTicks);
        }
    }

    private (int Radius, int LevelOffset, int MinimumTicks)? GetFallenFearTuning(
        AuthoritativeCombatTarget target)
    {
        if (monsterTypes?.TryGet(target.MonsterId, out var definition) == true) {
            if (definition.FallenFearRadius > 0
                && definition.FallenFearLevelOffset > 0
                && definition.FallenFearMinimumTicks > 0) {
                return (
                    definition.FallenFearRadius,
                    definition.FallenFearLevelOffset,
                    definition.FallenFearMinimumTicks);
            }
        }
        return UsesNativeAi(target, "Fallen")
            || UsesNativeAi(target, "Snotspill")
            ? (4, 8, 2)
            : null;
    }

    /** Applies an optional external quest kill trigger at the authoritative death boundary. */
    private void AdvanceQuestForMonsterDefeat(
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target)
    {
        if (target.MonsterId == 0)
            return;
        if (target.IsDiablo && gameMode == DiabloGameMode.SinglePlayer)
            player.DiabloKillLevel = Math.Max(
                player.DiabloKillLevel,
                checked((uint)difficulty + 1));
        foreach (var quest in world.Quests.Values
                     .Where(quest => quest.Active
                         && !quest.Unavailable
                         && !quest.Completed
                         && ((quest.CompletionMonsterId != 0
                                 && quest.CompletionMonsterId == target.MonsterId)
                             || (!string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity)
                                 && target.IsUnique
                                 && string.Equals(
                                     quest.CompletionUniqueIdentity,
                                     target.UniqueIdentity,
                                     StringComparison.Ordinal))
                             || (quest.CompletionDiablo && target.IsDiablo))
                         && (quest.LevelId == 0 || quest.LevelId == target.LevelId))) {
            quest.Progress = Math.Min(
                quest.RequiredProgress,
                checked(quest.Progress + 1));
            quest.Logged = true;
            if (quest.Progress == quest.RequiredProgress) {
                quest.Completed = true;
                quest.Active = false;
                quest.Variable1 = Math.Max(
                    quest.Variable1,
                    quest.CompletionVariable1);
                quest.Variable2 = Math.Max(
                    quest.Variable2,
                    quest.CompletionVariable2);
                if (quest.CompletionActivateQuestId != 0
                    && world.Quests.TryGetValue(
                        quest.CompletionActivateQuestId,
                        out var nextQuest)
                    && !nextQuest.Unavailable
                    && !nextQuest.Completed) {
                    nextQuest.Active = true;
                }
                SpawnQuestCompletionReward(quest, target);
            }
        }
    }

    /** Spawns a content-owned quest reward at the native monster-defeat position. */
    private void SpawnQuestCompletionReward(
        AuthoritativeQuestState quest,
        AuthoritativeCombatTarget target)
    {
        if (quest.CompletionRewardItemId == 0 || items is null)
            return;
        var position = FindAvailableWorldItemPosition(
            target.LevelId,
            target.PositionX,
            target.PositionY,
            target.EntityId);
        if (!position.HasValue)
            return;
        var itemSeed = AllocateWorldItemSeed();
        var state = spellBooks is not null
            && spellBooks.IsBookTemplate(items, quest.CompletionRewardItemId)
            ? spellBooks.GenerateRandom(
                items,
                itemSeed,
                quest.CompletionRewardItemLevel,
                gameMode,
                quest.CompletionRewardItemId)
            : items.Generate(
                quest.CompletionRewardItemId,
                itemSeed,
                quest.CompletionRewardItemLevel);
        var entityId = AllocateWorldItemEntityId(activePlayers);
        world.SetWorldItem(new AuthoritativeWorldItem(
            entityId,
            target.LevelId,
            position.Value.X,
            position.Value.Y,
            itemSeed,
            checked((uint)Math.Max(0, state.IdentifiedValue)),
            state));
    }

    /** Finds a deterministic open tile for a monster-origin world item. */
    private (int X, int Y)? FindAvailableWorldItemPosition(
        uint levelId,
        int originX,
        int originY,
        uint excludedEntityId)
    {
        if (!world.HasWorldItemCapacity(levelId))
            return null;
        return DeterministicGridCrawl.FindClosestValidPosition(
            originX,
            originY,
            0,
            rules.DeathDropSearchRadius,
            (positionX, positionY) =>
                world.IsWalkable(levelId, positionX, positionY)
                && !world.IsCathedralDoorItemBlocked(levelId, positionX, positionY)
                && !world.IsOccupied(
                    levelId,
                    positionX,
                    positionY,
                    activePlayers,
                    excludedEntityId)
                && !world.WorldItems.Values.Any(item =>
                    item.LevelId == levelId
                    && item.PositionX == positionX
                    && item.PositionY == positionY));
    }

    private uint AllocateProjectileEntityId(bool reuseReleasedId = false)
    {
        if (reuseReleasedId) {
            // Compact native replay lanes reuse the first available missile ID
            // after an earlier autonomous projectile expires.
            return DeterministicEntityIdAllocator.FindFirstAvailable(
                0x80000000,
                entityId =>
                    activePlayers.Any(player => player.EntityId == entityId)
                    || world.CombatTargets.ContainsKey(entityId)
                    || world.WorldItems.ContainsKey(entityId)
                    || world.Objects.ContainsKey(entityId)
                    || world.Projectiles.ContainsKey(entityId)
                    || world.Portals.ContainsKey(entityId)
                    || world.Corpses.ContainsKey(entityId));
        }
        var allocated = projectileEntityIds.Allocate(entityId =>
            activePlayers.Any(player => player.EntityId == entityId)
            || world.CombatTargets.ContainsKey(entityId)
            || world.WorldItems.ContainsKey(entityId)
            || world.Objects.ContainsKey(entityId)
            || world.Projectiles.ContainsKey(entityId)
            || world.Portals.ContainsKey(entityId)
            || world.Corpses.ContainsKey(entityId));
        return allocated;
    }
}

using Devilution.Protocol.V1;
using Devilution.Protocol;
using Devilution.Domain.Protocol;
using System.Collections.Generic;

namespace Devilution.Server.Protocol;

/** Validates adapter-produced snapshots before they cross the wire. */
public static class AuthoritativeSnapshotValidator
{
    public const int Sha256HexLength = 64;
    private const int NativeMaximumPlayerCount = AuthoritativeProtocolLimits.NativeMaximumPlayerCount;
    private const int NativeMaximumInventoryCount = AuthoritativeProtocolLimits.NativeMaximumInventoryCount;
    private const int NativeMaximumEquipmentCount = AuthoritativeProtocolLimits.NativeMaximumEquipmentCount;
    private const int NativeMaximumBeltCount = AuthoritativeProtocolLimits.NativeMaximumBeltCount;
    private const int NativeMaximumInventoryGridCount = AuthoritativeProtocolLimits.NativeMaximumInventoryGridCount;
    private const int NativeInventoryWidth = AuthoritativeProtocolLimits.NativeInventoryWidth;
    private const int NativeInventoryHeight = AuthoritativeProtocolLimits.NativeInventoryHeight;
    private const int NativeStashPageCount = AuthoritativeProtocolLimits.NativeStashPageCount;
    private const int NativeStashCellCount = AuthoritativeProtocolLimits.NativeStashCellCount;
    private const int NativeStashGridWidth = AuthoritativeProtocolLimits.NativeStashGridWidth;
    private const int NativeStashGridHeight = AuthoritativeProtocolLimits.NativeStashGridHeight;
    private const int NativeMaximumMonsterCount = AuthoritativeProtocolLimits.NativeMaximumMonsterCount;
    private const int NativeMaximumObjectCount = AuthoritativeProtocolLimits.NativeMaximumObjectCount;
    private const int NativeMaximumItemCount = AuthoritativeProtocolLimits.NativeMaximumItemCount;
    private const int NativeMaximumWarpTriggerCount = AuthoritativeProtocolLimits.NativeMaximumWarpTriggerCount;
    private const int NativeMaximumPortalCount = AuthoritativeProtocolLimits.NativeMaximumPortalCount;
    private const int MaximumNativeMonsterDeathAnimationTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterDeathAnimationTicks;
    private const uint MaximumNativeMonsterExperienceReward = AuthoritativeProtocolLimits.MaximumNativeMonsterExperienceReward;
    private const int MaximumNativeMonsterToHit = AuthoritativeProtocolLimits.MaximumNativeMonsterToHit;
    private const int MaximumNativeMonsterIntelligence = AuthoritativeProtocolLimits.MaximumNativeMonsterIntelligence;
    private const int MaximumNativeMonsterDirection = AuthoritativeProtocolLimits.MaximumNativeMonsterDirection;
    private const int MaximumNativeMonsterPackSize = AuthoritativeProtocolLimits.MaximumNativeMonsterPackSize;
    private const int MaximumNativeMonsterActiveTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterActiveTicks;
    private const int MinimumNativeMonsterMovementMode = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementMode;
    private const int MaximumNativeMonsterMovementMode = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementMode;
    private const int MinimumNativeMonsterMovementTurn = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementTurn;
    private const int MaximumNativeMonsterMovementTurn = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTurn;
    private const int MaximumNativeMonsterAiTimerTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks;
    private const int MaximumNativeMonsterShortTimerTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterShortTimerTicks;
    private const int MaximumNativeMonsterRetreatTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterRetreatTicks;
    private const int MaximumNativeMonsterMovementTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks;
    private const int MaximumNativeMonsterFallenStandAnimationFrame = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationFrame;
    private const int MaximumNativeMonsterFallenStandAnimationTick = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationTick;
    private const int MaximumNativeMonsterFallenRallyTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenRallyTicks;
    private const int MinimumNativeMonsterResistance = AuthoritativeProtocolLimits.MinimumNativeMonsterResistance;
    private const int MaximumNativeMonsterResistance = AuthoritativeProtocolLimits.MaximumNativeMonsterResistance;
    private const int MaximumNativeQuestByte = AuthoritativeProtocolLimits.MaximumNativeQuestByte;
    private const int MinimumNativeOptionalQuestByte = AuthoritativeProtocolLimits.MinimumNativeOptionalQuestByte;
    private const int MinimumNativeRewardOffset = AuthoritativeProtocolLimits.MinimumNativeRewardOffset;
    private const int MaximumNativeRewardOffset = AuthoritativeProtocolLimits.MaximumNativeRewardOffset;
    private const int MinimumNativeObjectOffset = AuthoritativeProtocolLimits.MinimumNativeObjectOffset;
    private const int MaximumNativeObjectOffset = AuthoritativeProtocolLimits.MaximumNativeObjectOffset;
    private const int MinimumNativeDirection = AuthoritativeProtocolLimits.MinimumNativeDirection;
    private const int MaximumNativeDirection = AuthoritativeProtocolLimits.MaximumNativeDirection;
    private const uint MaximumNativeObjectId = AuthoritativeProtocolLimits.NativeMaximumObjectId;
    private const int MaximumObjectEffectKind = AuthoritativeProtocolLimits.MaximumObjectEffectKind;
    private const int MaximumObjectPresentationKind = AuthoritativeProtocolLimits.MaximumObjectPresentationKind;
    private const int MaximumObjectQuestTransitionKind = AuthoritativeProtocolLimits.MaximumObjectQuestTransitionKind;
    private const int MaximumObjectRewardKind = AuthoritativeProtocolLimits.MaximumObjectRewardKind;
    private const int MaximumObjectRewardOriginKind = AuthoritativeProtocolLimits.MaximumObjectRewardOriginKind;
    private const int MaximumObjectQuestSequenceFollowUpKind = AuthoritativeProtocolLimits.MaximumObjectQuestSequenceFollowUpKind;
    private const int MaximumObjectProjectileSourceKind = AuthoritativeProtocolLimits.MaximumObjectProjectileSourceKind;
    private const int MaximumQuestTownerInteractionKind = AuthoritativeProtocolLimits.MaximumQuestTownerInteractionKind;
    private const int MaximumProjectileDamageType = AuthoritativeProtocolLimits.MaximumProjectileDamageType;
    private const int MaximumProjectileBehavior = AuthoritativeProtocolLimits.MaximumProjectileBehavior;
    private const int MaximumProjectilePresentationKind = AuthoritativeProtocolLimits.MaximumProjectilePresentationKind;
    private const int MaximumNativeProjectileVisual = AuthoritativeProtocolLimits.MaximumNativeProjectileVisual;
    private const int MaximumStatusEffectBehavior = AuthoritativeProtocolLimits.MaximumStatusEffectBehavior;
    private const int MaximumStatusEffectCount = AuthoritativeProtocolLimits.MaximumStatusEffectCount;
    private const int MaximumItemPresentationKind = AuthoritativeProtocolLimits.MaximumItemPresentationKind;
    private const int MaximumItemSpellSourceKind = AuthoritativeProtocolLimits.MaximumItemSpellSourceKind;
    private const int MaximumItemActionKind = AuthoritativeProtocolLimits.MaximumItemActionKind;
    private const int MaximumSpellTargetingKind = AuthoritativeProtocolLimits.MaximumSpellTargetingKind;
    private const int MaximumProjectilePhase = AuthoritativeProtocolLimits.MaximumProjectilePhase;
    private const int NativeMaximumWidth = AuthoritativeProtocolLimits.NativeMaximumWidth;
    private const int NativeMaximumHeight = AuthoritativeProtocolLimits.NativeMaximumHeight;
    private const int NativeMaximumMegaWidth = AuthoritativeProtocolLimits.NativeMaximumMegaWidth;
    private const int NativeMaximumMegaHeight = AuthoritativeProtocolLimits.NativeMaximumMegaHeight;
    private const uint NativeMaximumDunObjectLayerId = AuthoritativeProtocolLimits.NativeMaximumDunObjectLayerId;
    private const uint NativeMaximumDunPieceId = AuthoritativeProtocolLimits.NativeMaximumDunPieceId;
    private const uint NativeMaximumQuestId = AuthoritativeProtocolLimits.NativeMaximumQuestId;
    private const int MaximumQuestCount = AuthoritativeProtocolLimits.MaximumSnapshotQuestCount;
    private const int MaximumQuestTownerInteractionCount = AuthoritativeProtocolLimits.MaximumQuestTownerInteractionCount;
    private const int MaximumSetLevelObjectOriginCount = AuthoritativeProtocolLimits.MaximumSetLevelObjectOriginCount;
    private const int MaximumMaterializedProceduralLevelCount = AuthoritativeProtocolLimits.MaximumMaterializedProceduralLevelCount;
    private const int MaximumWorldMutationCount = AuthoritativeProtocolLimits.MaximumWorldMutationCount;
    private const int MaximumNativeSetLevelId = AuthoritativeProtocolLimits.MaximumNativeSetLevelId;
    private const int MaximumMonsterLeaderRelation = AuthoritativeProtocolLimits.MaximumMonsterLeaderRelation;
    private const uint MaximumNativeSpellLevel = AuthoritativeProtocolLimits.MaximumNativeSpellLevel;
    private const int MaximumMemorizedSpellCount = AuthoritativeProtocolLimits.MaximumMemorizedSpellCount;
    private const uint MaximumNativeCharacterLevel = AuthoritativeProtocolLimits.MaximumNativeCharacterLevel;
    private const uint MaximumNativeDiabloKillLevel = AuthoritativeProtocolLimits.MaximumNativeDiabloKillLevel;
    private const uint MaximumNativeReflectCharges = AuthoritativeProtocolLimits.MaximumNativeReflectCharges;
    private const int MinimumNativeLightRadius = AuthoritativeProtocolLimits.MinimumNativeLightRadius;
    private const int MaximumNativeLightRadius = AuthoritativeProtocolLimits.MaximumNativeLightRadius;

    public static void Validate(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSessionMetadata(snapshot);
        ValidatePlayerNames(snapshot);
        ValidatePlayerPoolCapacity(snapshot);
        ValidatePlayerNativeRanges(snapshot);
        ValidatePlayerCoreState(snapshot);
        ValidatePlayerItemProjections(snapshot);
        ValidateQuests(snapshot);
        ValidateObjects(snapshot);
        ValidateQuestTownerInteractions(snapshot);
        ValidateTownerGreetings(snapshot);
        ValidateLevelGeometry(snapshot);
        ValidateSetLevelObjectOrigins(snapshot);
        ValidateMaterializedProceduralLevels(snapshot);
        ValidateEntityPositions(snapshot);
        ValidatePlayerActions(snapshot);
        ValidateMonsters(snapshot);
        ValidatePlayerMinionOwners(snapshot);
        ValidateScavengerCorpseReferences(snapshot);
        ValidateMonsterMovementState(snapshot);
        ValidateWorldItems(snapshot);
        ValidateProjectiles(snapshot);
        ValidateEntityIds(snapshot);
        ValidateMonsterPoolCapacity(snapshot);
        ValidateObjectPoolCapacity(snapshot);
        ValidateWorldItemPoolCapacity(snapshot);
        if (snapshot.StateSha256.Length == 0 || IsSha256Hex(snapshot.StateSha256))
            return;

        throw new InvalidDataException(
            "The authoritative snapshot contains an invalid SHA-256 state hash.");
    }

    private static void ValidatePlayerNames(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            if (!AuthoritativePlayerNameRules.IsValidShape(player.CharacterName)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid character name.");
            }
        }
    }

    private static void ValidatePlayerNativeRanges(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            if (!AuthoritativeLevelIdRules.IsSupported(player.LevelId)
                || !Enum.IsDefined(player.FacingDirection)
                || !Enum.IsDefined(player.CombatMode)
                || player.CombatMode == PlayerCombatMode.Stand
                    && player.CombatModeEndTick != 0
                || player.CombatMode is PlayerCombatMode.Block
                    or PlayerCombatMode.GotHit
                    && player.CombatModeEndTick == 0
                || !Enum.IsDefined(player.AbilityKind)
                || !Enum.IsDefined(player.AbilityTargetingKind)
                || player.DiabloKillLevel > byte.MaxValue)
                throw new InvalidDataException(
                    "The authoritative snapshot contains player state outside the native range.");
        }
    }

    private static void ValidatePlayerItemProjections(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            ValidateInventoryTopology(player);
            ValidatePlayerItemFootprints(player);
            if (player.HeldItem is { } heldItem
                && (!HasValidItemIdentity(heldItem.ItemSeed, heldItem.State)
                    || heldItem.State.InventoryWidth > NativeInventoryWidth
                    || heldItem.State.InventoryHeight > NativeInventoryHeight)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid held item.");
            }
        }

        ValidateActiveStore(snapshot);
    }

    private static void ValidateInventoryTopology(PlayerSnapshot player)
    {
        if (player.Inventory.Count > NativeMaximumInventoryCount
            || player.Equipment.Count > NativeMaximumEquipmentCount
            || player.Belt.Count > NativeMaximumBeltCount
            || player.InventoryGrid.Count != 0 && player.InventoryGrid.Count != NativeMaximumInventoryGridCount
            || player.InventoryGrid.Any(cell => cell < -1 || cell >= player.Inventory.Count)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid inventory topology.");
        }

        var equipmentSlots = new HashSet<uint>();
        foreach (var item in player.Equipment) {
            if (item is null || item.Slot >= NativeMaximumEquipmentCount || !equipmentSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate equipment slots.");
            }
        }

        var beltSlots = new HashSet<uint>();
        foreach (var item in player.Belt) {
            if (item is null || item.Slot >= NativeMaximumBeltCount || !beltSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate belt slots.");
            }
        }

        if (player.InventoryGrid.Count == 0)
            return;

        var expected = Enumerable.Repeat(-1, player.InventoryGrid.Count).ToArray();
        for (var itemIndex = 0; itemIndex < player.Inventory.Count; itemIndex++) {
            var anchor = player.InventoryGrid.IndexOf(itemIndex);
            var state = player.Inventory[itemIndex]?.State;
            var width = (int)Math.Clamp(state?.InventoryWidth ?? 1U, 1U, NativeInventoryWidth);
            var height = (int)Math.Clamp(state?.InventoryHeight ?? 1U, 1U, NativeInventoryHeight);
            if (anchor < 0
                || !HasExactInventoryFootprint(
                    expected,
                    anchor,
                    itemIndex,
                    width,
                    height)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid inventory footprint topology.");
            }
        }
        if (!player.InventoryGrid.SequenceEqual(expected)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid inventory-grid projection.");
        }
    }

    private static bool HasExactInventoryFootprint(
        int[] expected,
        int anchor,
        int itemIndex,
        int width,
        int height)
    {
        var column = anchor % NativeInventoryWidth;
        var row = anchor / NativeInventoryWidth;
        if (column + width > NativeInventoryWidth || row + height > NativeInventoryHeight)
            return false;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * NativeInventoryWidth + column + x;
                if (expected[cell] >= 0)
                    return false;
                expected[cell] = itemIndex;
            }
        }
        return true;
    }

    private static void ValidateActiveStore(Snapshot snapshot)
    {
        if (snapshot.Players.Count == 0) {
            if (snapshot.ActiveStore is not null)
                throw new InvalidDataException(
                    "The authoritative snapshot contains an active store without a local player.");
            return;
        }

        var player = snapshot.Players[0];
        var store = snapshot.ActiveStore;
        if ((store is null) != (player.ActiveStoreId == 0)
            || store is not null
                && (store.StoreId == 0 || store.StoreId != player.ActiveStoreId)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an inconsistent active store.");
        }
        if (store is null)
            return;

        var slots = new HashSet<uint>();
        foreach (var item in store.Items) {
            if (!slots.Add(item.StoreSlot)
                || item.Price > int.MaxValue
                || !HasValidItemIdentity(item.ItemSeed, item.State)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid active-store stock.");
            }
            ValidateItemState(item.State);
        }
    }

    private static void ValidatePlayerItemFootprints(PlayerSnapshot player)
    {
        var itemSeeds = new HashSet<uint>();
        foreach (var item in player.Inventory) {
            ValidatePlayerItemPrice(item?.Price ?? 0);
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        }
        foreach (var item in player.Belt)
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        foreach (var item in player.Equipment)
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        foreach (var item in player.Stash)
            if (item?.Item is { } stashItem) {
                ValidatePlayerItemPrice(stashItem.Price);
                ValidatePlayerItem(stashItem.ItemSeed, stashItem.State, itemSeeds);
            }
        if (player.HeldItem is { } heldItem) {
            ValidatePlayerItemPrice(heldItem.Price);
            ValidatePlayerItem(heldItem.ItemSeed, heldItem.State, itemSeeds);
        }
    }

    private static void ValidatePlayerItemPrice(uint price)
    {
        if (price > int.MaxValue)
            throw new InvalidDataException(
                "The authoritative snapshot contains a player-item price outside the native range.");
    }

    private static void ValidatePlayerItem(
        uint itemSeed,
        ItemStateSnapshot? state,
        ISet<uint> itemSeeds)
    {
        if (itemSeed != 0 && !itemSeeds.Add(itemSeed))
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate player item references.");
        if (state is not null && !HasValidItemIdentity(itemSeed, state))
            throw new InvalidDataException(
                "The authoritative snapshot contains a player item with an invalid identity.");
        ValidateInventoryFootprint(state);
        ValidateItemState(state);
    }

    private static void ValidateInventoryFootprint(ItemStateSnapshot? state)
    {
        if (state is null)
            return;
        if (state.InventoryWidth > NativeInventoryWidth || state.InventoryHeight > NativeInventoryHeight) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an item footprint outside the native inventory grid.");
        }
    }

    private static void ValidateSessionMetadata(Snapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.Difficulty))
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid game difficulty.");

        var mutationIds = new HashSet<uint>();
        if (snapshot.ActiveWorldMutationIds.Count > MaximumWorldMutationCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many active world mutations.");
        foreach (var mutationId in snapshot.ActiveWorldMutationIds) {
            if (mutationId == 0 || !mutationIds.Add(mutationId))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate world-mutation identities.");
        }

        var portalIds = new HashSet<uint>();
        if (snapshot.Portals.Count > NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"The authoritative snapshot contains more than {NativeMaximumPortalCount} portals, exceeding the native portal capacity.");
        foreach (var portal in snapshot.Portals) {
            if (portal.PortalId == 0
                || !portalIds.Add(portal.PortalId)
                || portal.Bidirectional
                    && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0)
                || !portal.Bidirectional
                    && (portal.OwnerEntityId != 0 || portal.RemainingTicks != 0)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid portal route state.");
            }
        }
    }

    private static void ValidateEntityIds(Snapshot snapshot)
    {
        var entityIds = new HashSet<uint>();
        foreach (var player in snapshot.Players)
            ValidateEntityId(player.EntityId, entityIds);
        foreach (var monster in snapshot.Monsters)
            ValidateEntityId(monster.EntityId, entityIds);
        foreach (var worldItem in snapshot.WorldItems)
            ValidateEntityId(worldItem.EntityId, entityIds);
        foreach (var @object in snapshot.Objects)
            ValidateEntityId(@object.EntityId, entityIds);
        foreach (var projectile in snapshot.Projectiles)
            ValidateEntityId(projectile.EntityId, entityIds);
        foreach (var corpse in snapshot.Corpses)
            ValidateEntityId(corpse.EntityId, entityIds);
    }

    private static void ValidatePlayerPoolCapacity(Snapshot snapshot)
    {
        if (snapshot.Players.Count > NativeMaximumPlayerCount)
            throw new InvalidDataException(
                $"The authoritative snapshot contains more than {NativeMaximumPlayerCount} "
                + "players, exceeding the native player pool capacity.");
    }

    private static void ValidateQuests(Snapshot snapshot)
    {
        if (snapshot.Quests.Count > MaximumQuestCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quests.");

        var questIds = new HashSet<uint>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var quest in snapshot.Quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
                || quest.HasNativeQuestId
                    && (quest.NativeQuestId > NativeMaximumQuestId
                        || !nativeQuestIds.Add(quest.NativeQuestId))
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || quest.Unavailable && (quest.Completed || quest.Progress != 0)
                || quest.Variable1 is < 0 or > MaximumNativeQuestByte
                || quest.Variable2 is < 0 or > MaximumNativeQuestByte
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !NativeTextLimits.IsIdentityWithinLimit(quest.CompletionUniqueIdentity)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid quest state.");
            }
        }
    }

    private static void ValidatePlayerCoreState(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            ValidatePlayerResources(player);
            ValidatePlayerAttributes(player);
            ValidateVisitedLevels(player);
            ValidatePlayerSpells(player);
            ValidateStatusEffects(player);
            ValidateStash(player);
        }
    }

    private static void ValidatePlayerResources(PlayerSnapshot player)
    {
        if (player.Life < 0
            || player.LifeMaximum < 0
            || player.LifeMaximum > 0 && player.Life > player.LifeMaximum
            || player.Mana < 0
            || player.ManaMaximum < 0
            || player.ManaMaximum > 0 && player.Mana > player.ManaMaximum
            || player.BaseLifeMaximum < 0
            || player.BaseManaMaximum < 0
            || player.Gold > int.MaxValue
            || player.StashGold > int.MaxValue
            || player.StatPoints > int.MaxValue
            || player.ReflectCharges > MaximumNativeReflectCharges
            || player.LightRadius != 0
                && (player.LightRadius is < MinimumNativeLightRadius or > MaximumNativeLightRadius)
            || player.CharacterLevel > MaximumNativeCharacterLevel
            || player.DiabloKillLevel > MaximumNativeDiabloKillLevel) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player resources.");
        }
    }

    private static void ValidatePlayerAttributes(PlayerSnapshot player)
    {
        var attributes = player.Attributes;
        if (attributes is null)
            return;

        if (attributes.Strength is null
            || attributes.Magic is null
            || attributes.Dexterity is null
            || attributes.Vitality is null
            || attributes.Strength.Base < 0
            || attributes.Strength.Current < 0
            || attributes.Magic.Base < 0
            || attributes.Magic.Current < 0
            || attributes.Dexterity.Base < 0
            || attributes.Dexterity.Current < 0
            || attributes.Vitality.Base < 0
            || attributes.Vitality.Current < 0) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player attributes.");
        }
    }

    private static void ValidateStatusEffects(PlayerSnapshot player)
    {
        if (player.StatusEffects.Count > MaximumStatusEffectCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many status effects.");

        var effectIds = new HashSet<uint>();
        foreach (var effect in player.StatusEffects) {
            if (effect is null
                || effect.EffectId == 0
                || effect.RemainingTicks == 0
                || (int)effect.Behavior is < 0 or > MaximumStatusEffectBehavior
                || !Enum.IsDefined(effect.Behavior)
                || (effect.Behavior is StatusEffectBehavior.Infravision
                        or StatusEffectBehavior.Search
                        or StatusEffectBehavior.Etherealize)
                    && effect.Magnitude != 0
                || !effectIds.Add(effect.EffectId)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid status-effect state.");
            }
        }
    }

    private static void ValidateStash(PlayerSnapshot player)
    {
        if (player.StashOpen && player.ActiveStoreId != 0)
            throw new InvalidDataException(
                "The authoritative snapshot opens a stash and store simultaneously.");
        if (player.Stash.Count > NativeStashPageCount * NativeStashCellCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many stash items.");

        foreach (var item in player.Stash) {
            if (item is null
                || item.Page >= NativeStashPageCount
                || item.Item is null) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid stash metadata.");
            }
        }

        foreach (var page in player.Stash.Select(item => item.Page).Distinct()) {
            var occupied = new bool[NativeStashCellCount];
            foreach (var item in player.Stash.Where(item => item.Page == page)) {
                var state = item.Item?.State
                    ?? throw new InvalidDataException(
                        "The authoritative snapshot contains a stash item without state.");
                var widthValue = Math.Max(1U, state.InventoryWidth);
                var heightValue = Math.Max(1U, state.InventoryHeight);
                var column = item.Cell % NativeStashGridWidth;
                var row = item.Cell / NativeStashGridWidth;
                if (item.Cell >= NativeStashCellCount
                    || widthValue > NativeStashGridWidth
                    || heightValue > NativeStashGridHeight
                    || column + widthValue > NativeStashGridWidth
                    || row + heightValue > NativeStashGridHeight) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid stash footprint.");
                }
                var width = (int)widthValue;
                var height = (int)heightValue;
                var columnIndex = (int)column;
                var rowIndex = (int)row;
                for (var y = 0; y < height; y++) {
                    for (var x = 0; x < width; x++) {
                        var cell = (rowIndex + y) * NativeStashGridWidth + columnIndex + x;
                        if (occupied[cell])
                            throw new InvalidDataException(
                                "The authoritative snapshot contains overlapping stash items.");
                        occupied[cell] = true;
                    }
                }
            }
        }
    }

    private static void ValidateVisitedLevels(PlayerSnapshot player)
    {
        if (!AuthoritativeLevelIdRules.IsSupported(player.LevelId)
            || player.VisitedLevelIds.Count > AuthoritativeProtocolLimits.MaximumNativeVisitedLevelCount
            || player.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdRules.IsRegularLevel(levelId))
            || player.VisitedLevelIds.Distinct().Count() != player.VisitedLevelIds.Count
            || player.VisitedLevelIds.Count > 0
                && AuthoritativeLevelIdRules.IsRegularLevel(player.LevelId)
                && !player.VisitedLevelIds.Contains(player.LevelId)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid visited-level state.");
        }
    }

    private static void ValidatePlayerSpells(PlayerSnapshot player)
    {
        if (player.AbilitySpellId > byte.MaxValue
            || player.MemorizedSpells.Count > MaximumMemorizedSpellCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains out-of-range player spell state.");

        var spellIds = new HashSet<uint>();
        foreach (var spell in player.MemorizedSpells) {
            if (spell is null
                || spell.SpellId == 0
                || spell.SpellId > byte.MaxValue
                || spell.Level == 0
                || spell.Level > MaximumNativeSpellLevel
                || !Enum.IsDefined(spell.TargetingKind)
                || spell.DisplayName.Contains('\0')
                || !spellIds.Add(spell.SpellId)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate memorized spells.");
            }
        }
    }

    private static void ValidateQuestTownerInteractions(Snapshot snapshot)
    {
        if (snapshot.QuestTownerInteractions.Count > MaximumQuestTownerInteractionCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quest/towner interactions.");

        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (!levels.TryAdd(level.LevelId, level))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
        }

        var interactionIds = new HashSet<uint>();
        foreach (var interaction in snapshot.QuestTownerInteractions) {
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
                || !questIds.Contains(interaction.QuestId)
                || levels.Count > 0
                    && (!levels.TryGetValue(interaction.LevelId, out var level)
                        || level.Width > 0 && interaction.PositionX >= level.Width
                        || level.Height > 0 && interaction.PositionY >= level.Height)
                || interaction.TownerId is < 0 or > MaximumNativeQuestByte
                || interaction.LevelId > ushort.MaxValue
                || interaction.RequiredVisitedLevel1 > ushort.MaxValue
                || interaction.RequiredVisitedLevel2 > ushort.MaxValue
                || interaction.RequiredItemId > ushort.MaxValue
                || interaction.PositionX < 0
                || interaction.PositionY < 0
                || interaction.Kind is < 0 or > MaximumQuestTownerInteractionKind
                || !interaction.HasMessageId
                || interaction.MessageId is <= 0 or > short.MaxValue
                || interaction.HasRequiredVariable1
                    && interaction.RequiredVariable1 is < 0 or > MaximumNativeQuestByte
                || interaction.HasRequiredVariable2
                    && interaction.RequiredVariable2 is < 0 or > MaximumNativeQuestByte
                || interaction.HasVariable1
                    && interaction.Variable1 is < 0 or > MaximumNativeQuestByte
                || interaction.HasVariable2
                    && interaction.Variable2 is < 0 or > MaximumNativeQuestByte) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid quest/towner interaction.");
            }
        }
    }

    private static void ValidateTownerGreetings(Snapshot snapshot)
    {
        if (snapshot.TownerGreetings.Count > AuthoritativeProtocolLimits.MaximumTownerGreetingCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many towner greetings.");

        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (!levels.TryAdd(level.LevelId, level))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
        }
        var townerIds = new HashSet<int>();
        foreach (var greeting in snapshot.TownerGreetings) {
            if (greeting.TownerId is < 0 or > MaximumNativeQuestByte
                || !townerIds.Add(greeting.TownerId)
                || greeting.LevelId > ushort.MaxValue
                || greeting.PositionX is < 0 or > MaximumNativeQuestByte
                || greeting.PositionY is < 0 or > MaximumNativeQuestByte
                || levels.Count > 0
                    && (!levels.TryGetValue(greeting.LevelId, out var level)
                        || level.Width > 0 && greeting.PositionX >= level.Width
                        || level.Height > 0 && greeting.PositionY >= level.Height)
                || greeting.MessageId is <= 0 or > short.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid towner greeting.");
            }
        }
    }

    private static void ValidateObjects(Snapshot snapshot)
    {
        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        foreach (var @object in snapshot.Objects) {
            var transitionKind = @object.QuestTransitionKind;
            var rewardKind = @object.RewardKind;
            var followUpKind = @object.QuestSequenceFollowUpKind;
            var effectKind = @object.EffectKind;
            var transitionSetsVariable = transitionKind is 3 or 4 or 5 or 6 or 7 or 8;
            var transitionValid = transitionKind == 0
                || @object.QuestId != 0
                    && (transitionSetsVariable
                        ? @object.QuestTransitionAmount > 0
                        : @object.QuestTransitionAmount == 0);
            var rewardValid = rewardKind == 0
                ? @object.RewardId == 0
                    && @object.RewardItemLevel == 0
                    && @object.RewardOffsetX == 0
                    && @object.RewardOffsetY == 0
                    && @object.RewardOriginKind == 0
                    && @object.RewardSetLevelId == 0
                : @object.RewardId != 0;
            var producesProjectile = effectKind is 8 or 9
                || followUpKind == 2;
            var followUpValid = followUpKind switch {
                0 => @object.QuestSequenceFollowUpAmount == 0
                    && @object.QuestSequenceFollowUpOffsetX == 0
                    && @object.QuestSequenceFollowUpOffsetY == 0,
                1 => (effectKind == 4 || transitionKind != 0)
                    && @object.QuestSequenceFollowUpAmount != 0
                    && @object.QuestSequenceFollowUpOffsetX == 0
                    && @object.QuestSequenceFollowUpOffsetY == 0,
                2 => (effectKind == 4 || effectKind == 6 || transitionKind != 0)
                    && @object.QuestSequenceFollowUpAmount != 0,
                3 => (effectKind == 4 || transitionKind != 0)
                    && @object.QuestSequenceFollowUpAmount != 0,
                _ => false,
            };

            if (@object.ObjectId == 0
                || @object.EffectAmount < 0
                || effectKind < 0
                || effectKind > MaximumObjectEffectKind
                || (int)@object.PresentationKind is < 0 or > MaximumObjectPresentationKind
                || !Enum.IsDefined(@object.PresentationKind)
                || @object.QuestId != 0 && !questIds.Contains(@object.QuestId)
                || transitionKind is < 0 or > MaximumObjectQuestTransitionKind
                || @object.QuestTransitionAmount is < 0 or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !transitionValid
                || rewardKind is < 0 or > MaximumObjectRewardKind
                || @object.RewardItemLevel is < 0 or > MaximumNativeQuestByte
                || @object.RewardOffsetX is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOffsetY is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOriginKind is < 0 or > MaximumObjectRewardOriginKind
                || @object.RewardOriginKind == 0 && @object.RewardSetLevelId != 0
                || @object.RewardOriginKind == 1 && @object.RewardSetLevelId is < 0 or > MaximumNativeSetLevelId
                || !rewardValid
                || @object.ProjectileTargetOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.ProjectileTargetOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !producesProjectile
                    && (@object.ProjectileTargetOffsetX != 0
                        || @object.ProjectileTargetOffsetY != 0)
                || followUpKind is < 0 or > MaximumObjectQuestSequenceFollowUpKind
                || @object.QuestSequenceFollowUpOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.QuestSequenceFollowUpOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !followUpValid
                || @object.ProjectileSourceKind is < 0 or > MaximumObjectProjectileSourceKind
                || !producesProjectile && @object.ProjectileSourceKind != 0
                || @object.NativeObjectId > MaximumNativeObjectId
                || @object.AnimationFrame > byte.MaxValue
                || @object.ActivatedAnimationFrame > byte.MaxValue
                || @object.Breakable && @object.MissilePassable
                || @object.Destroyed && !@object.Breakable) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid object effect metadata.");
            }
        }
    }

    private static void ValidateMonsterPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Monsters.GroupBy(monster => monster.LevelId)) {
            if (group.Count() > NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }
    }

    private static void ValidateObjectPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Objects.GroupBy(@object => @object.LevelId)) {
            if (group.Count() > NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumObjectCount} "
                    + $"objects on level {group.Key}, exceeding the native object capacity.");
        }
    }

    private static void ValidateWorldItemPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.WorldItems.GroupBy(item => item.LevelId)) {
            if (group.Count() > NativeMaximumItemCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumItemCount} "
                    + $"world items on level {group.Key}, exceeding the native item capacity.");
        }
    }

    private static void ValidateLevelGeometry(Snapshot snapshot)
    {
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            var coarseWidth = (level.Width + 1L) / 2;
            var coarseHeight = (level.Height + 1L) / 2;
            if (level.Width < 0
                || level.Height < 0
                || level.Width > NativeMaximumWidth
                || level.Height > NativeMaximumHeight
                || level.NativeTransparency.Any(value => value > byte.MaxValue)
                || level.NativeObjectLayer.Any(value => value > NativeMaximumDunObjectLayerId)
                || level.NativeMegaTileLayer.Any(value => value > byte.MaxValue)
                || level.NativePieceLayer.Any(value => value > NativeMaximumDunPieceId)
                || level.NativeMegaTileLayer.Count != 0
                    && (coarseWidth > NativeMaximumMegaWidth
                        || coarseHeight > NativeMaximumMegaHeight))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid level geometry dimensions.");

            if (level.Width > 0 && level.Height > 0) {
                var cellCount = checked((long)level.Width * level.Height);
                if (!AreValidCells(level.BlockedCells, cellCount)
                    || !AreValidCells(level.MissileBlockedCells, cellCount)
                    || !AreValidCells(level.NativeVisionBlockedCells, cellCount)
                    || level.NativeTransparency.Count != 0
                        && level.NativeTransparency.Count != cellCount
                    || level.NativeObjectLayer.Count != 0
                        && level.NativeObjectLayer.Count != cellCount
                    || level.NativeMegaTileLayer.Count != 0
                        && level.NativeMegaTileLayer.Count != coarseWidth * coarseHeight
                    || level.NativePieceLayer.Count != 0
                        && level.NativePieceLayer.Count != cellCount)
                    throw new InvalidDataException(
                        "The authoritative snapshot contains invalid level geometry cells.");
            }

            if (!levels.TryAdd(level.LevelId, level))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
        }

        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var warpIds = new HashSet<uint>();
        foreach (var level in snapshot.LevelGeometry) {
            if (level.WarpTriggers.Count > NativeMaximumWarpTriggerCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumWarpTriggerCount} "
                    + $"warp triggers on level {level.LevelId}, exceeding the native trigger capacity.");

            foreach (var warp in level.WarpTriggers) {
                var hasTargetLevel = levels.TryGetValue(warp.TargetLevelId, out var targetLevel);
                if (warp.WarpId == 0
                    || !warpIds.Add(warp.WarpId)
                    || warp.TriggerX < 0
                    || warp.TriggerY < 0
                    || level.Width > 0 && warp.TriggerX >= level.Width
                    || level.Height > 0 && warp.TriggerY >= level.Height
                    || !hasTargetLevel
                    || warp.TargetX < 0
                    || warp.TargetY < 0
                    || targetLevel!.Width > 0 && warp.TargetX >= targetLevel.Width
                    || targetLevel.Height > 0 && warp.TargetY >= targetLevel.Height
                    || warp.RequiredQuestId != 0
                        && !questIds.Contains(warp.RequiredQuestId)
                    || warp.RequiredQuestId == 0 && warp.RequiredQuestCompleted
                    || warp.RequiredQuestId == 0 && !warp.Available
                    || warp.StableSymbol.Any(character => character is < (char)0x21 or > (char)0x7e))
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid warp trigger.");
            }
        }
    }

    private static bool AreValidCells(IList<int> cells, long cellCount)
    {
        if (cells.Count > cellCount)
            return false;

        var uniqueCells = new HashSet<int>();
        foreach (var cell in cells) {
            if (cell < 0 || cell >= cellCount || !uniqueCells.Add(cell))
                return false;
        }
        return true;
    }

    private static void ValidateSetLevelObjectOrigins(Snapshot snapshot)
    {
        if (snapshot.SetLevelObjectOrigins.Count > MaximumSetLevelObjectOriginCount
            || snapshot.SetLevelObjectOrigins.Any(origin => origin.SetLevelId is < 0 or > MaximumNativeSetLevelId)
            || snapshot.SetLevelObjectOrigins
                .Select(origin => origin.SetLevelId)
                .Distinct()
                .Count() != snapshot.SetLevelObjectOrigins.Count)
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid or duplicate set-level object origins.");
        if (snapshot.LevelGeometry.Count == 0)
            return;

        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        foreach (var origin in snapshot.SetLevelObjectOrigins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains a set-level object origin outside level {origin.LevelId}.");
        }
    }

    private static void ValidateMaterializedProceduralLevels(Snapshot snapshot)
    {
        if (snapshot.MaterializedProceduralLevelIds.Count > MaximumMaterializedProceduralLevelCount
            || snapshot.MaterializedProceduralLevelIds
                .Distinct()
                .Count() != snapshot.MaterializedProceduralLevelIds.Count)
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate materialized procedural levels.");
    }

    private static void ValidateEntityPositions(Snapshot snapshot)
    {
        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        bool IsNativePosition(int x, int y)
            => x is >= 0 and <= byte.MaxValue
                && y is >= 0 and <= byte.MaxValue;

        bool IsInBounds(uint levelId, int x, int y)
        {
            if (!IsNativePosition(x, y))
                return false;
            if (!levels.TryGetValue(levelId, out var level)
                || level.Width == 0
                || level.Height == 0)
                return true;
            return x < level.Width && y < level.Height;
        }

        bool IsValidMonster(MonsterSnapshot monster)
            => IsInBounds(monster.LevelId, monster.PositionX, monster.PositionY)
                && (!monster.Moving
                    || IsInBounds(
                        monster.LevelId,
                        monster.FuturePositionX,
                        monster.FuturePositionY));

        if (snapshot.Players.Any(player =>
                !IsInBounds(player.LevelId, player.PositionX, player.PositionY))
            || snapshot.Monsters.Any(monster => !IsValidMonster(monster))
            || snapshot.WorldItems.Any(item =>
                !IsInBounds(item.LevelId, item.PositionX, item.PositionY))
            || snapshot.Objects.Any(@object =>
                !IsInBounds(@object.LevelId, @object.PositionX, @object.PositionY))
            || snapshot.Projectiles.Any(projectile =>
                !IsInBounds(projectile.LevelId, projectile.PositionX, projectile.PositionY)
                || !IsInBounds(projectile.LevelId, projectile.TargetX, projectile.TargetY))
            || snapshot.Corpses.Any(corpse =>
                !IsInBounds(corpse.LevelId, corpse.PositionX, corpse.PositionY))
            || snapshot.Portals.Any(portal =>
                !IsInBounds(
                    portal.SourceLevelId,
                    portal.SourcePositionX,
                    portal.SourcePositionY)
                || !IsInBounds(
                    portal.DestinationLevelId,
                    portal.DestinationPositionX,
                    portal.DestinationPositionY)))
            throw new InvalidDataException(
                "The authoritative snapshot contains an entity outside projected level geometry.");
    }

    private static void ValidatePlayerActions(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            var action = player.CurrentAction;
            if (action is null)
                continue;
            if (!Enum.IsDefined(action.Kind)
                || action.Kind == PlayerActionKind.Unspecified
                || action.EffectTick < action.StartedAtTick
                || action.ReadyTick < action.EffectTick
                || action.ReadyTick > action.EndTick
                || action.EndTick != player.CombatModeEndTick) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid player action timing.");
            }

            var valid = action.Kind switch {
                PlayerActionKind.Move =>
                    player.CombatMode is PlayerCombatMode.WalkNorthwards
                        or PlayerCombatMode.WalkSouthwards
                        or PlayerCombatMode.WalkSideways
                    && action.DirectionX is >= MinimumNativeDirection and <= MaximumNativeDirection
                    && action.DirectionY is >= MinimumNativeDirection and <= MaximumNativeDirection
                    && (action.DirectionX != 0 || action.DirectionY != 0)
                    && action.TargetEntityId == 0
                    && action.Cast is null
                    && IsProjectedMoveWalkable(snapshot, player, action),
                PlayerActionKind.Attack =>
                    player.CombatMode is PlayerCombatMode.Attack
                        or PlayerCombatMode.RangedAttack
                    && action.DirectionX == 0
                    && action.DirectionY == 0
                    && action.TargetEntityId != 0
                    && action.Cast is null,
                PlayerActionKind.Cast =>
                    player.CombatMode == PlayerCombatMode.Spell
                    && action.DirectionX == 0
                    && action.DirectionY == 0
                    && action.Cast is not null
                    && action.Cast.SpellId != 0
                    && action.TargetEntityId == action.Cast.TargetEntityId
                    && Enum.IsDefined(action.Cast.Source)
                    && (action.Cast.SourceItem is null
                        || Enum.IsDefined(action.Cast.SourceItem.Location)),
                PlayerActionKind.Death =>
                    player.CombatMode == PlayerCombatMode.Death
                    && player.Life == 0
                    && action.EffectApplied
                    && action.EffectTick == action.StartedAtTick
                    && action.ReadyTick == action.EndTick
                    && action.DirectionX == 0
                    && action.DirectionY == 0
                    && action.Cast is null,
                PlayerActionKind.LevelTransition =>
                    player.CombatMode == PlayerCombatMode.NewLevel
                    && player.Life > 0
                    && action.EffectApplied
                    && action.EffectTick == action.StartedAtTick
                    && action.ReadyTick == action.EndTick
                    && action.DirectionX == 0
                    && action.DirectionY == 0
                    && action.TargetEntityId != 0
                    && action.Cast is null,
                PlayerActionKind.RestartTown =>
                    player.CombatMode == PlayerCombatMode.NewLevel
                    && player.Life > 0
                    && action.EffectApplied
                    && action.EffectTick == action.StartedAtTick
                    && action.ReadyTick == action.EndTick
                    && action.DirectionX == 0
                    && action.DirectionY == 0
                    && action.TargetEntityId == 0
                    && action.Cast is null,
                _ => false,
            };
            if (!valid)
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid player action.");
        }
    }

    private static bool IsProjectedMoveWalkable(
        Snapshot snapshot,
        PlayerSnapshot player,
        PlayerActionSnapshot action)
    {
        var level = snapshot.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == player.LevelId);
        if (level is null || level.Width == 0 || level.Height == 0)
            return true;

        var targetX = (long)player.PositionX + action.DirectionX;
        var targetY = (long)player.PositionY + action.DirectionY;
        if (targetX < 0
            || targetY < 0
            || targetX >= level.Width
            || targetY >= level.Height)
            return false;

        var targetCell = targetY * level.Width + targetX;
        return targetCell <= int.MaxValue
            && !level.BlockedCells.Contains((int)targetCell);
    }

    private static void ValidateMonsters(Snapshot snapshot)
    {
        var monstersById = new Dictionary<uint, MonsterSnapshot>();
        foreach (var monster in snapshot.Monsters) {
            var hitPointsQ6 = monster.HitPointsQ6 == 0 && monster.HitPoints != 0
                ? (long)monster.HitPoints * 64
                : monster.HitPointsQ6;
            var maxHitPointsQ6 = monster.MaxHitPointsQ6 == 0 && monster.MaxHitPoints != 0
                ? (long)monster.MaxHitPoints * 64
                : monster.MaxHitPointsQ6;
            if (monster.EntityId == 0
                || monster.HitPoints < 0
                || monster.MaxHitPoints < 0
                || monster.MaxHitPoints > 0 && monster.HitPoints > monster.MaxHitPoints
                || monster.HitPointsQ6 < 0
                || monster.MaxHitPointsQ6 < 0
                || maxHitPointsQ6 < hitPointsQ6
                || hitPointsQ6 < 0
                || monster.Alive != (hitPointsQ6 > 0)
                || !NativeTextLimits.IsIdentityWithinLimit(monster.UniqueIdentity)
                || !string.IsNullOrEmpty(monster.UniqueIdentity) && !monster.IsUnique
                || monster.DialogueMessageId < 0
                || monster.DialogueMessageId > short.MaxValue
                || monster.DialogueMessageId != 0 && !monster.IsUnique
                || !Enum.IsDefined(monster.DialogueState)
                || monster.DialogueState != MonsterDialogueState.Unspecified
                    && !monster.IsUnique
                || monster.AttackDamage < 0
                || monster.MinDamage < 0
                || monster.MaxDamage < monster.MinDamage
                || monster.MinDamageSpecial < 0
                || monster.MaxDamageSpecial < monster.MinDamageSpecial
                || monster.AggroRange < 0
                || monster.MonsterLevel < 0
                || monster.DeathAnimationTicks is < 0 or > MaximumNativeMonsterDeathAnimationTicks
                || monster.DeathTicksRemaining < 0
                || monster.DeathTicksRemaining > monster.DeathAnimationTicks
                || monster.HitPointsQ6 > 0 && monster.DeathTicksRemaining != 0
                || monster.ExperienceReward > MaximumNativeMonsterExperienceReward
                || (monster.MonsterItemSeed == 0) != (monster.MonsterAiSeed == 0)
                || !AuthoritativeProtocolLimits.IsIdentityWithinLimit(monster.NativeAi)
                || monster.NativeAi.Contains('\0')
                || monster.ToHit is < 0 or > MaximumNativeMonsterToHit
                || monster.ToHitSpecial is < 0 or > MaximumNativeMonsterToHit
                || monster.Intelligence is < 0 or > MaximumNativeMonsterIntelligence
                || monster.Direction is < 0 or > MaximumNativeMonsterDirection
                || monster.FallenFearDirection is < 0 or > MaximumNativeMonsterDirection
                || monster.BatRetreatDirection is < 0 or > MaximumNativeMonsterDirection
                || monster.FireResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.LightningResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.MagicResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || monster.AcidResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
                || !monster.IsPlayerMinion && monster.OwnerPlayerId != 0
                || monster.LeaderRelation is < 0 or > MaximumMonsterLeaderRelation
                || (monster.LeaderRelation == 0
                    ? monster.LeaderEntityId != 0
                    : monster.LeaderEntityId == 0)
                || monster.LeaderRelation != 0 && monster.IsUnique
                || monster.PackSize is < 0 or > MaximumNativeMonsterPackSize
                || monster.ActiveForTicks is < 0 or > MaximumNativeMonsterActiveTicks
                || monster.BatMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RangedAvoidanceTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.RangedAvoidanceMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RhinoMovementTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementTurn is < MinimumNativeMonsterMovementTurn or > MaximumNativeMonsterMovementTurn
                || monster.AvoidanceMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.SkeletonRangedMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.FatMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.RhinoMovementMode is < MinimumNativeMonsterMovementMode or > MaximumNativeMonsterMovementMode
                || monster.FallenFearTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SneakRetreatTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
                || monster.ScavengerHealingTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
                || monster.BatRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
                || monster.BatMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RangedAvoidanceMoveTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.RangedAvoidanceMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoMovementTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.AvoidanceMovementTicks is < -1 or > MaximumNativeMonsterMovementTicks
                || monster.AvoidanceMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonMeleeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonRangedMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.FatMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
                || monster.RhinoDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SkeletonKingDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.HorkDemonDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.RangedAvoidanceDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.SnakeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.CounselorDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.AvoidanceDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.RangedDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.MegaDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
                || monster.CounselorRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
                || monster.FallenStandAnimationFrame is < 0 or > MaximumNativeMonsterFallenStandAnimationFrame
                || monster.FallenStandAnimationTick is < 0 or > MaximumNativeMonsterFallenStandAnimationTick
                || monster.FallenRallyTicks is < 0 or > MaximumNativeMonsterFallenRallyTicks
                || !monstersById.TryAdd(monster.EntityId, monster)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid monster state.");
            }
        }

        foreach (var monster in snapshot.Monsters.Where(monster => monster.LeaderRelation != 0)) {
            if (!monstersById.TryGetValue(monster.LeaderEntityId, out var leader)
                || leader.EntityId == monster.EntityId
                || !leader.Alive
                || leader.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid monster pack leader.");
            }
        }
    }

    private static void ValidateProjectiles(Snapshot snapshot)
    {
        foreach (var projectile in snapshot.Projectiles) {
            if (projectile.SourceEntityId == 0
                || projectile.SpellId == 0 && !IsWeaponProjectile(projectile.Behavior)
                || projectile.RemainingTicks == 0
                || projectile.Damage < 0
                || projectile.AreaRadius < 0
                || projectile.DamageMinimum < 0
                || projectile.DamageMaximum < projectile.DamageMinimum
                || projectile.DamageQ6 < 0
                || projectile.ElementalFireDamage < 0
                || projectile.ElementalLightningDamage < 0
                || projectile.PerilSelfDamageQ6 < 0
                || projectile.EffectiveSpellLevel < 0
                || projectile.DamageType is < 0 or > MaximumProjectileDamageType
                || projectile.Behavior is < 0 or > MaximumProjectileBehavior
                || projectile.Phase is < 0 or > MaximumProjectilePhase
                || (int)projectile.PresentationKind is < 0 or > MaximumProjectilePresentationKind
                || !Enum.IsDefined(projectile.PresentationKind)
                || (int)projectile.NativeVisual is < 0 or > MaximumNativeProjectileVisual
                || !Enum.IsDefined(projectile.NativeVisual)
                || UsesDirectionalPresentation(projectile)
                    && projectile.OriginalDirection is < 0 or > MaximumNativeMonsterDirection
                || !IsValidNativeMissileIdentity(projectile))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid projectile state.");
        }
    }

    private static bool UsesDirectionalPresentation(ProjectileSnapshot projectile)
    {
        if (projectile.NativeVisual != NativeProjectileVisual.Unspecified
            || !string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;

        return (AuthoritativeProjectileBehavior)projectile.Behavior is
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
                or AuthoritativeProjectileBehavior.ResurrectionBeam
                or AuthoritativeProjectileBehavior.Phasing
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.HolyBolt
                or AuthoritativeProjectileBehavior.BloodStar
                or AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.FireWallSegment
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaBall
                or AuthoritativeProjectileBehavior.ApocalypseBoom
                or AuthoritativeProjectileBehavior.Elemental
                or AuthoritativeProjectileBehavior.GuardianController
                or AuthoritativeProjectileBehavior.Etherealize
                or AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.HorkSpawn
                or AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;
    }

    private static bool IsWeaponProjectile(int behavior)
        => (AuthoritativeProjectileBehavior)behavior is
            AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;

    private static bool IsValidNativeMissileIdentity(ProjectileSnapshot projectile)
    {
        if (string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;
        if ((AuthoritativeProjectileBehavior)projectile.Behavior
                != AuthoritativeProjectileBehavior.MonsterMagic
            || !AuthoritativeProtocolLimits.IsIdentityWithinLimit(projectile.NativeMissileId)
            || !IsAsciiLetter(projectile.NativeMissileId[0])
                && projectile.NativeMissileId[0] != '_')
            return false;
        return projectile.NativeMissileId.Skip(1).All(character =>
            IsAsciiLetter(character)
                || character is >= '0' and <= '9'
                || character is '_' or '-');
    }

    private static bool IsAsciiLetter(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static void ValidateScavengerCorpseReferences(Snapshot snapshot)
    {
        var corpsesById = new Dictionary<uint, CorpseSnapshot>();
        foreach (var corpse in snapshot.Corpses) {
            if (!corpsesById.TryAdd(corpse.EntityId, corpse))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate corpse entity IDs.");
        }

        foreach (var monster in snapshot.Monsters) {
            if (monster.ScavengerCorpseEntityId == 0)
                continue;
            if (!corpsesById.TryGetValue(monster.ScavengerCorpseEntityId, out var corpse)
                || corpse.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid Scavenger corpse reference.");
            }
        }
    }

    private static void ValidatePlayerMinionOwners(Snapshot snapshot)
    {
        var playersById = new Dictionary<uint, PlayerSnapshot>();
        foreach (var player in snapshot.Players) {
            if (!playersById.TryAdd(player.EntityId, player))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate player entity IDs.");
        }

        foreach (var monster in snapshot.Monsters) {
            if (!monster.IsPlayerMinion || monster.OwnerPlayerId == 0)
                continue;
            if (!playersById.TryGetValue(monster.OwnerPlayerId, out var owner)
                || owner.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid player-minion owner reference.");
            }
        }
    }

    private static void ValidateMonsterMovementState(Snapshot snapshot)
    {
        foreach (var monster in snapshot.Monsters) {
            var futureDeltaX = Math.Abs((long)monster.FuturePositionX - monster.PositionX);
            var futureDeltaY = Math.Abs((long)monster.FuturePositionY - monster.PositionY);
            if (monster.Moving
                    && Math.Max(futureDeltaX, futureDeltaY) != 1
                || !monster.Moving
                    && (monster.FuturePositionX != 0 || monster.FuturePositionY != 0)
                    && (monster.FuturePositionX != monster.PositionX
                        || monster.FuturePositionY != monster.PositionY)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid monster occupancy state.");
            }
            if ((!monster.Moving
                    && (monster.MovementStartedTick != 0
                        || monster.MovementEffectTick != 0))
                || (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0)
                    && monster.MovementEffectTick <= monster.MovementStartedTick) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid monster movement timing.");
            }
        }
    }

    private static void ValidateWorldItems(Snapshot snapshot)
    {
        foreach (var item in snapshot.WorldItems) {
            if (item.Price > int.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an unrepresentable world-item price.");
            }
            ValidateItemState(item.State);
            if (!HasValidItemIdentity(item.ItemSeed, item.State)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid world item.");
            }
        }
    }

    private static void ValidateItemState(ItemStateSnapshot? state)
    {
        if (state is null)
            return;
        if (state.Value < 0
            || state.IdentifiedValue < 0
            || state.MinDamage < 0
            || state.MaxDamage < state.MinDamage
            || state.ArmorClass < 0
            || state.Charges < 0
            || state.MaxCharges < state.Charges
            || state.Durability < 0
            || state.MaxDurability < state.Durability
            || state.FireMinDamage < 0
            || state.FireMaxDamage < state.FireMinDamage
            || state.LightningMinDamage < 0
            || state.LightningMaxDamage < state.LightningMinDamage
            || !IsNativeItemStateRepresentable(state)
            || (int)state.PresentationKind is < 0 or > MaximumItemPresentationKind
            || !Enum.IsDefined(state.PresentationKind)
            || (int)state.SpellSourceKind is < 0 or > MaximumItemSpellSourceKind
            || !Enum.IsDefined(state.SpellSourceKind)
            || (int)state.ItemActionKind is < 0 or > MaximumItemActionKind
            || !Enum.IsDefined(state.ItemActionKind)
            || (int)state.SpellTargetingKind is < 0 or > MaximumSpellTargetingKind
            || !Enum.IsDefined(state.SpellTargetingKind)
            || state.SpellDisplayName.Contains('\0')) {
            throw new InvalidDataException(
                "The authoritative snapshot contains impossible item state ranges.");
        }
    }

    private static bool IsNativeItemStateRepresentable(ItemStateSnapshot state)
    {
        return state.CreateInfo <= ushort.MaxValue
            && state.ItemType is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.Magical is >= byte.MinValue and <= byte.MaxValue
            && state.EquipLocation is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemClass is >= byte.MinValue and <= byte.MaxValue
            && state.MinDamage is >= byte.MinValue and <= byte.MaxValue
            && state.MaxDamage is >= byte.MinValue and <= byte.MaxValue
            && state.ArmorClass is >= short.MinValue and <= short.MaxValue
            && state.MiscId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SpellId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemIndex is >= short.MinValue and <= short.MaxValue
            && state.PlusDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusToHit is >= short.MinValue and <= short.MaxValue
            && state.PlusArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PlusStrength is >= short.MinValue and <= short.MaxValue
            && state.PlusMagic is >= short.MinValue and <= short.MaxValue
            && state.PlusDexterity is >= short.MinValue and <= short.MaxValue
            && state.PlusVitality is >= short.MinValue and <= short.MaxValue
            && state.PlusFireResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusLightningResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMagicResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMana is >= short.MinValue and <= short.MaxValue
            && state.PlusHitPoints is >= short.MinValue and <= short.MaxValue
            && state.PlusDamageModifier is >= short.MinValue and <= short.MaxValue
            && state.PlusGetHit is >= short.MinValue and <= short.MaxValue
            && state.PlusLight is >= short.MinValue and <= short.MaxValue
            && state.SpellLevelAdd is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.FireMinDamage is >= short.MinValue and <= short.MaxValue
            && state.FireMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMinDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusEnemyArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PrefixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SuffixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumStrength is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumMagic is >= byte.MinValue and <= byte.MaxValue
            && state.MinimumDexterity is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.HellfireDamageArmorFlags is >= byte.MinValue and <= byte.MaxValue;
    }

    private static bool HasValidItemIdentity(
        uint itemSeed,
        ItemStateSnapshot? state)
    {
        if (state is null)
            return false;
        var hasPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.HasNativeCursorGraphic;
        if (!hasPresentation)
            return itemSeed != 0;
        var hasValidNames = !string.IsNullOrEmpty(state.BaseName)
            && !string.IsNullOrEmpty(state.IdentifiedName)
            && !state.BaseName.Contains('\0')
            && !state.IdentifiedName.Contains('\0')
            && System.Text.Encoding.UTF8.GetByteCount(state.BaseName) < 64
            && System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) < 64;
        return hasValidNames
            && state.HasNativeCursorGraphic
            && state.NativeCursorGraphic is >= 0 and <= byte.MaxValue;
    }

    private static void ValidateEntityId(uint entityId, HashSet<uint> entityIds)
    {
        if (entityId == 0 || !entityIds.Add(entityId))
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate or zero entity IDs.");
    }

    private static bool IsSha256Hex(string value)
    {
        if (value.Length != Sha256HexLength)
            return false;

        foreach (var character in value) {
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')
                  || (character >= 'A' && character <= 'F')))
                return false;
        }
        return true;
    }
}

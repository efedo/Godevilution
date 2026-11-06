using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Server.Snapshots;
using AuthoritativeMonsterTypeCatalog = Devilution.Diablo.AuthoritativeMonsterTypeCatalog;
using AuthoritativeProjectileBehavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Protocol;

/** Player and item mappings shared by both snapshot directions. */
internal static partial class DiabloProtocolMapper
{
    private static PlayerSnapshot ToProtocolPlayer(
        StorePlayerSnapshot state,
        ulong snapshotTick,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks,
        AuthoritativeItemCatalog? items,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels = null)
    {
        if (state.StashOpen && state.ActiveStoreId is not null) {
            throw new InvalidDataException(
                "Authoritative player projection opens a stash and store simultaneously.");
        }
        ValidateProjectedPlayerItemTopology(state);
        if (state.Inventory.Any(item => item.Price > int.MaxValue)
            || state.HeldItem is { Price: > int.MaxValue }
            || state.Stash.Any(item => item.Price > int.MaxValue)) {
            throw new InvalidDataException(
                "Authoritative item price exceeds the shared native protocol range.");
        }
        if (!string.IsNullOrEmpty(state.CharacterName)
            && !DiabloPlayerName.IsValid(state.CharacterName)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid character name.");
        }
        if (!Enum.IsDefined(state.AbilityKind)
            || !Enum.IsDefined(state.FacingDirection)
            || !Enum.IsDefined(state.CombatMode)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid enum value.");
        }
        if (state.LightRadius != 0
            && (state.LightRadius is < MinimumNativeLightRadius or > MaximumNativeLightRadius)) {
            throw new InvalidDataException(
                "Authoritative player projection contains an invalid light radius.");
        }
        if (state.CombatMode == DiabloPlayerCombatMode.Stand
                && state.CombatModeEndTick != 0
            || state.CombatMode is DiabloPlayerCombatMode.Block
                or DiabloPlayerCombatMode.GotHit
                && state.CombatModeEndTick == 0) {
            throw new InvalidDataException(
                "Authoritative player combat-mode timing is invalid.");
        }
        if (state.Attributes.Strength.Base < 0
            || state.Attributes.Strength.Current < 0
            || state.Attributes.Magic.Base < 0
            || state.Attributes.Magic.Current < 0
            || state.Attributes.Dexterity.Base < 0
            || state.Attributes.Dexterity.Current < 0
            || state.Attributes.Vitality.Base < 0
            || state.Attributes.Vitality.Current < 0) {
            throw new InvalidDataException(
                "Authoritative player attributes contain negative values.");
        }
        ValidateProjectedVisitedLevels(state);
        ValidateProjectedPlayerSpells(state);
        ValidateProjectedStatusEffects(state);
        ValidateProjectedPlayerAction(state, snapshotTick, levels);
        if (state.Life < 0
            || state.LifeMaximum < 0
            || state.LifeMaximum > 0 && state.Life > state.LifeMaximum
            || (state.Mana < 0 || state.ManaMaximum < 0)
                && (!state.NativeLifeBase.HasValue || !state.NativeManaBase.HasValue)
            || state.Mana < -(2000 << 6)
            || state.ManaMaximum < -(2000 << 6)
            || state.ManaMaximum != 0 && state.Mana > state.ManaMaximum
            || state.BaseLifeMaximum < 0
            || state.BaseManaMaximum < 0
            || state.Gold > int.MaxValue
            || state.StashGold > int.MaxValue
            || state.StatPoints > int.MaxValue
            || state.ReflectCharges > ushort.MaxValue
            || state.DiabloKillLevel > byte.MaxValue
            || state.CharacterLevel > MaximumNativeCharacterLevel) {
            throw new InvalidDataException(
                "Authoritative player resources exceed the shared protocol range.");
        }
        var player = new PlayerSnapshot {
            EntityId = state.EntityId,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            Gold = state.Gold,
            ActiveStoreId = state.ActiveStoreId ?? 0,
            Life = state.Life,
            Mana = Math.Max(0, state.Mana),
            ManaMaximum = Math.Max(0, state.ManaMaximum),
            LifeMaximum = state.LifeMaximum,
            BaseManaMaximum = state.BaseManaMaximum,
            BaseLifeMaximum = state.BaseLifeMaximum,
            CharacterLevel = state.CharacterLevel,
            CharacterClassId = state.CharacterClassId,
            CharacterName = state.CharacterName,
            ManaShieldActive = state.ManaShieldActive,
            ReflectCharges = state.ReflectCharges,
            AbilitySpellId = state.AbilitySpellId,
            AbilityKind = (PlayerAbilityKind)state.AbilityKind,
            AbilityTargetingKind = ResolveSpellTargetingKind(state.AbilitySpellId, spells),
            HostileMode = !state.FriendlyMode,
            FacingDirection = (PlayerDirection)state.FacingDirection,
            CombatMode = (PlayerCombatMode)state.CombatMode,
            CombatModeEndTick = state.CombatModeEndTick,
            LevelId = state.LevelId,
            Experience = state.Experience,
            DiabloKillLevel = state.DiabloKillLevel,
            StatPoints = state.StatPoints,
            VisitedLevelIds = { state.VisitedLevelIds.OrderBy(levelId => levelId) },
            Attributes = new PlayerAttributesSnapshot {
                Strength = ToProtocolAttribute(state.Attributes.Strength),
                Magic = ToProtocolAttribute(state.Attributes.Magic),
                Dexterity = ToProtocolAttribute(state.Attributes.Dexterity),
                Vitality = ToProtocolAttribute(state.Attributes.Vitality),
            },
        };
        player.FullyExploredLevelIds.Add(state.FullyExploredLevelIds ?? []);
        if (state.Mana < 0 || state.ManaMaximum < 0) {
            player.NativeMana = state.Mana;
            player.NativeManaMaximum = state.ManaMaximum;
        }
        foreach (var spell in state.MemorizedSpells.OrderBy(spell => spell.SpellId)) {
            (spell.Level == 0 ? player.ZeroLevelMemorizedSpells : player.MemorizedSpells).Add(new PlayerSpellLevelSnapshot {
                SpellId = spell.SpellId,
                Level = spell.Level,
                DisplayName = ResolveSpellDisplayName(spell.SpellId, spells, spellBooks),
                TargetingKind = ResolveSpellTargetingKind(spell.SpellId, spells),
            });
        }
        if (state.CurrentAction is not null)
            player.CurrentAction = ToProtocolPlayerAction(state.CurrentAction);
        foreach (var effect in state.StatusEffects) {
            player.StatusEffects.Add(new StatusEffectSnapshot {
                EffectId = effect.EffectId,
                RemainingTicks = effect.RemainingTicks,
                Magnitude = effect.Magnitude,
                Behavior = (StatusEffectBehavior)effect.Behavior,
            });
        }
        foreach (var item in state.Inventory) {
            player.Inventory.Add(new ItemSnapshot {
                StoreId = item.StoreId,
                StoreSlot = item.StoreSlot,
                ItemSeed = item.ItemSeed,
                Price = item.Price,
                PurchasedAtTick = item.PurchasedAtTick,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        if (state.HeldItem is { } heldItem) {
            player.HeldItem = new ItemSnapshot {
                StoreId = heldItem.StoreId,
                StoreSlot = heldItem.StoreSlot,
                ItemSeed = heldItem.ItemSeed,
                Price = heldItem.Price,
                PurchasedAtTick = heldItem.PurchasedAtTick,
                State = ToProtocolItemState(heldItem.ItemSeed, heldItem.State, spells, spellBooks, items),
            };
        }
        foreach (var item in state.Equipment) {
            player.Equipment.Add(new EquippedItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        foreach (var item in state.Belt) {
            player.Belt.Add(new BeltItemSnapshot {
                Slot = item.Slot,
                ItemSeed = item.ItemSeed,
                State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
            });
        }
        player.InventoryGrid.Add(state.InventoryGrid);
        foreach (var item in state.Stash.OrderBy(item => item.Page).ThenBy(item => item.Cell)) {
            player.Stash.Add(new StashItemSnapshot {
                Page = item.Page,
                Cell = item.Cell,
                Item = new ItemSnapshot {
                    StoreId = item.StoreId,
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    PurchasedAtTick = item.PurchasedAtTick,
                    State = ToProtocolItemState(item.ItemSeed, item.State, spells, spellBooks, items),
                },
            });
        }
        player.StashGold = state.StashGold;
        player.StashOpen = state.StashOpen;
        player.LightRadius = state.LightRadius;
        player.NativeWalkPathPending = state.NativeWalkPath is not null
            && state.NativeWalkPath.Directions.Count > 0;
        return player;
    }

    private static void ValidateProjectedPlayerItemTopology(StorePlayerSnapshot state)
    {
        if (state.Inventory is null
            || state.Equipment is null
            || state.Belt is null
            || state.InventoryGrid is null
            || state.Stash is null
            || state.Inventory.Any(item => item is null)
            || state.Stash.Any(item => item is null)
            || state.Inventory.Count > NativeMaximumInventoryCount
            || state.Equipment.Count > NativeMaximumEquipmentCount
            || state.Belt.Count > NativeMaximumBeltCount
            || state.Stash.Count > DiabloStashLayout.MaximumItems
            || state.InventoryGrid.Count != 0 && state.InventoryGrid.Count != NativeMaximumInventoryGridCount
            || state.InventoryGrid.Any(cell => cell < -1 || cell >= state.Inventory.Count)) {
            throw new InvalidDataException(
                "Authoritative player item collections have invalid topology.");
        }

        if (state.InventoryGrid.Count > 0) {
            var expected = Enumerable.Repeat(-1, state.InventoryGrid.Count).ToArray();
            var grid = state.InventoryGrid.ToArray();
            for (var itemIndex = 0; itemIndex < state.Inventory.Count; itemIndex++) {
                var anchor = Array.IndexOf(grid, itemIndex);
                var itemState = state.Inventory[itemIndex].State;
                var width = Math.Clamp(itemState.InventoryWidth, 1, NativeInventoryWidth);
                var height = Math.Clamp(itemState.InventoryHeight, 1, NativeInventoryHeight);
                if (anchor < 0
                    || !HasExactProjectedInventoryFootprint(
                        expected,
                        anchor,
                        itemIndex,
                        width,
                        height)) {
                    throw new InvalidDataException(
                        "Authoritative player inventory has an invalid footprint topology.");
                }
            }
            if (!grid.SequenceEqual(expected)) {
                throw new InvalidDataException(
                    "Authoritative player inventory grid does not match item footprints.");
            }
        }

        var equipmentSlots = new HashSet<uint>();
        foreach (var item in state.Equipment) {
            if (item is null || item.State is null || item.Slot >= NativeMaximumEquipmentCount
                || !equipmentSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "Authoritative player equipment slots are invalid or duplicated.");
            }
        }

        var beltSlots = new HashSet<uint>();
        foreach (var item in state.Belt) {
            if (item is null || item.State is null || item.Slot >= NativeMaximumBeltCount
                || !beltSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "Authoritative player belt slots are invalid or duplicated.");
            }
        }

        var itemSeeds = new HashSet<uint>();
        bool AddItemSeed(uint itemSeed) => itemSeed == 0 || itemSeeds.Add(itemSeed);
        foreach (var item in state.Inventory) {
            if (item is null || item.State is null || !AddItemSeed(item.ItemSeed)) {
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
            }
        }
        foreach (var item in state.Equipment)
            if (!AddItemSeed(item.ItemSeed))
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
        foreach (var item in state.Belt)
            if (!AddItemSeed(item.ItemSeed))
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
        foreach (var item in state.Stash) {
            if (item is null || item.State is null || !AddItemSeed(item.ItemSeed)) {
                throw new InvalidDataException(
                    "Authoritative player item references are invalid or duplicated.");
            }
        }
        if (state.HeldItem is { } heldItem
            && (heldItem.State is null || !AddItemSeed(heldItem.ItemSeed))) {
            throw new InvalidDataException(
                "Authoritative held-item reference is invalid or duplicated.");
        }

        foreach (var pageGroup in state.Stash.GroupBy(item => item.Page)) {
            if (pageGroup.Key >= DiabloStashLayout.PageCount)
                throw new InvalidDataException("Authoritative stash page is outside the native range.");
            var cells = DiabloStashLayout.CreateEmptyPage();
            foreach (var item in pageGroup) {
                var width = Math.Max(1, item.State.InventoryWidth);
                var height = Math.Max(1, item.State.InventoryHeight);
                if (width > DiabloStashLayout.ColumnCount
                    || height > DiabloStashLayout.RowCount
                    || !DiabloStashLayout.CanPlace(cells, item.Cell, width, height)) {
                    throw new InvalidDataException(
                        "Authoritative stash contains an invalid or overlapping footprint.");
                }
                DiabloStashLayout.Fill(cells, item.Cell, width, height);
            }
        }
    }

    private static bool HasExactProjectedInventoryFootprint(
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

    private static void ValidateProjectedVisitedLevels(StorePlayerSnapshot state)
    {
        if (state.VisitedLevelIds is null
            || !AuthoritativeLevelIdRules.IsSupported(state.LevelId)
            || state.VisitedLevelIds.Count > MaximumNativeVisitedLevelCount
            || state.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdRules.IsRegularLevel(levelId))
            || state.VisitedLevelIds.Count > 0
                && AuthoritativeLevelIdRules.IsRegularLevel(state.LevelId)
                && !state.VisitedLevelIds.Contains(state.LevelId)
            || state.VisitedLevelIds.Distinct().Count() != state.VisitedLevelIds.Count) {
            throw new InvalidDataException(
                "Authoritative player visited-level state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedPlayerSpells(StorePlayerSnapshot state)
    {
        if (state.MemorizedSpells is null
            || state.AbilitySpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
            || state.MemorizedSpells.Count > AuthoritativePlayerLoadoutCatalog.MaximumMemorizedSpellCount
            || state.MemorizedSpells.Any(spell =>
                spell is null
                || spell.SpellId == 0
                || spell.SpellId > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                || spell.Level > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
            || state.MemorizedSpells.Select(spell => spell.SpellId).Distinct().Count()
                != state.MemorizedSpells.Count) {
            throw new InvalidDataException(
                "Authoritative player spell state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedStatusEffects(StorePlayerSnapshot state)
    {
        if (state.StatusEffects is null
            || state.StatusEffects.Count > MaximumStatusEffectCount
            || state.StatusEffects.Any(effect =>
                effect is null
                || effect.EffectId == 0
                || effect.RemainingTicks == 0
                || !Enum.IsDefined(effect.Behavior)
                || ((effect.Behavior is AuthoritativeStatusEffectBehavior.Infravision
                        or AuthoritativeStatusEffectBehavior.Search
                        or AuthoritativeStatusEffectBehavior.Etherealize)
                    && effect.Magnitude != 0))
            || state.StatusEffects.Select(effect => effect.EffectId).Distinct().Count()
                != state.StatusEffects.Count) {
            throw new InvalidDataException(
                "Authoritative player status-effect state is invalid or duplicated.");
        }
    }

    private static void ValidateProjectedPlayerAction(
        StorePlayerSnapshot state,
        ulong snapshotTick,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        var action = state.CurrentAction;
        if (action is null)
            return;
        if (!Enum.IsDefined(action.Kind)
            || !AuthoritativePlayerActionTimingRules.IsValidAtSnapshot(
                snapshotTick,
                action.StartedAtTick,
                action.EffectTick,
                action.ReadyTick,
                action.EndTick,
                action.EffectApplied)
            || action.EndTick != state.CombatModeEndTick) {
            throw new InvalidDataException(
                $"Authoritative player action timing is invalid at snapshot tick {snapshotTick}.");
        }

        var valid = action.Kind switch {
            DiabloPlayerActionKind.Move =>
                state.CombatMode is DiabloPlayerCombatMode.WalkNorthwards
                    or DiabloPlayerCombatMode.WalkSouthwards
                    or DiabloPlayerCombatMode.WalkSideways
                && action.DirectionX is >= MinimumNativeDirection and <= MaximumNativeDirection
                && action.DirectionY is >= MinimumNativeDirection and <= MaximumNativeDirection
                && (action.DirectionX != 0 || action.DirectionY != 0)
                && action.TargetEntityId == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is null,
            DiabloPlayerActionKind.Attack =>
                state.CombatMode is DiabloPlayerCombatMode.Attack
                    or DiabloPlayerCombatMode.RangedAttack
                && action.DirectionX == 0
                && action.DirectionY == 0
                && (action.TargetCell
                    ? action.TargetEntityId == 0
                        && IsProjectedActionTarget(state.LevelId, action.TargetX, action.TargetY, levels)
                    : action.TargetEntityId != 0
                        && (action.TargetX == 0 && action.TargetY == 0
                            || state.CombatMode == DiabloPlayerCombatMode.RangedAttack
                                && IsProjectedActionTarget(
                                    state.LevelId,
                                    action.TargetX,
                                    action.TargetY,
                                    levels)))
                && action.CastIntent is null,
            DiabloPlayerActionKind.Cast =>
                state.CombatMode == DiabloPlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is not null
                && action.CastIntent.SpellId != 0
                && action.TargetEntityId == action.CastIntent.TargetEntityId
                && IsProjectedCastTarget(state.LevelId, action.CastIntent, levels)
                && IsValidCastSource(state, action.CastIntent, action.EffectApplied)
                && Enum.IsDefined(action.CastIntent.Source)
                && (!action.CastIntent.SourceItem.HasValue
                    || Enum.IsDefined(action.CastIntent.SourceItem.Value.Location)),
            DiabloPlayerActionKind.Death =>
                state.CombatMode == DiabloPlayerCombatMode.Death
                && state.Life == 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetEntityId != 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.CastIntent is null,
            DiabloPlayerActionKind.LevelTransition =>
                state.CombatMode == DiabloPlayerCombatMode.NewLevel
                && state.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.TargetEntityId != 0
                && action.CastIntent is null,
            DiabloPlayerActionKind.RestartTown =>
                state.CombatMode == DiabloPlayerCombatMode.NewLevel
                && state.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetX == 0
                && action.TargetY == 0
                && !action.TargetCell
                && action.TargetEntityId == 0
                && action.CastIntent is null,
            _ => false,
        };
        if (!valid)
            throw new InvalidDataException("Authoritative player action is invalid.");
    }

    private static bool IsProjectedActionTarget(
        uint levelId,
        int targetX,
        int targetY,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(targetX, targetY))
            return false;
        return levels is null
            || !levels.TryGetValue(levelId, out var level)
            || level.Width == 0
            || level.Height == 0
            || targetX < level.Width && targetY < level.Height;
    }

    private static bool IsProjectedCastTarget(
        uint levelId,
        DiabloCastIntent cast,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(
                cast.TargetX,
                cast.TargetY))
            return false;
        if (cast.TargetEntityId != 0)
            return true;
        return levels is null
            || !levels.TryGetValue(levelId, out var level)
            || level.Width == 0
            || level.Height == 0
            || cast.TargetX < level.Width && cast.TargetY < level.Height;
    }

    private static bool IsValidCastSource(
        StorePlayerSnapshot player,
        DiabloCastIntent cast,
        bool effectApplied)
    {
        var requiresItem = cast.Source is DiabloCastSourceKind.ChargedItem
            or DiabloCastSourceKind.ConsumableItem;
        if (requiresItem != cast.SourceItem.HasValue)
            return false;
        if (!cast.SourceItem.HasValue)
            return true;
        var item = cast.SourceItem.Value;
        // Consumption precedes the animation tail; its source is now history,
        // not an inventory index that must survive removal/reindexing.
        if (effectApplied && cast.Source == DiabloCastSourceKind.ConsumableItem)
            return item.Location switch {
                DiabloPlayerItemLocation.Inventory => item.Slot < NativeMaximumInventoryCount,
                DiabloPlayerItemLocation.Belt => item.Slot < NativeMaximumBeltCount,
                DiabloPlayerItemLocation.Equipment => item.Slot < NativeMaximumEquipmentCount,
                DiabloPlayerItemLocation.Stash =>
                    item.Slot < AuthoritativeProtocolLimits.NativeStashCellCount
                    && item.Page < AuthoritativeProtocolLimits.NativeStashPageCount,
                _ => false,
            };
        return item.Location switch {
            DiabloPlayerItemLocation.Inventory =>
                item.Slot < player.Inventory.Count
                && MatchesStableItem(
                    player.Inventory[(int)item.Slot].State,
                    item.StableItemId),
            DiabloPlayerItemLocation.Belt =>
                item.Slot < NativeMaximumBeltCount
                && player.Belt.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Equipment =>
                item.Slot < NativeMaximumEquipmentCount
                && player.Equipment.Any(candidate =>
                    candidate.Slot == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            DiabloPlayerItemLocation.Stash =>
                player.StashOpen
                && item.Slot < AuthoritativeProtocolLimits.NativeStashCellCount
                && item.Page < AuthoritativeProtocolLimits.NativeStashPageCount
                && player.Stash.Any(candidate =>
                    candidate.Page == item.Page
                    && candidate.Cell == item.Slot
                    && MatchesStableItem(candidate.State, item.StableItemId)),
            _ => false,
        };
    }

    private static bool MatchesStableItem(
        AuthoritativeItemState state,
        uint stableItemId)
    {
        return stableItemId == 0 || state.StableItemId == stableItemId;
    }

    private static StorePlayerSnapshot ToPlayerState(PlayerSnapshot source)
    {
        var attributes = source.Attributes ?? new PlayerAttributesSnapshot();
        return new StorePlayerSnapshot(
            source.Gold,
            source.ActiveStoreId == 0 ? null : source.ActiveStoreId,
            source.Inventory.Select(item => new OwnedStoreItem(
                item.StoreId,
                item.StoreSlot,
                item.ItemSeed,
                item.Price,
                item.PurchasedAtTick,
                FromProtocolItemState(item.State))).ToArray(),
            source.Experience,
            source.Life,
            source.HasNativeMana ? source.NativeMana : source.Mana,
            source.HasNativeManaMaximum ? source.NativeManaMaximum : source.ManaMaximum,
            new PlayerAttributesState(
                FromProtocolAttribute(attributes.Strength),
                FromProtocolAttribute(attributes.Magic),
                FromProtocolAttribute(attributes.Dexterity),
                FromProtocolAttribute(attributes.Vitality)),
            source.Equipment.Select(item => new EquippedStoreItem(
                item.Slot,
                item.ItemSeed,
                FromProtocolItemState(item.State))).ToArray(),
            source.InventoryGrid.ToArray(),
            source.Belt.Select(item => new BeltStoreItem(
                item.Slot,
                item.ItemSeed,
                FromProtocolItemState(item.State))).ToArray()) {
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            LifeMaximum = source.LifeMaximum,
            BaseLifeMaximum = source.BaseLifeMaximum == 0 && !source.HasNativeMana
                ? source.LifeMaximum
                : source.BaseLifeMaximum,
            BaseManaMaximum = source.BaseManaMaximum == 0 && !source.HasNativeMana
                ? source.ManaMaximum
                : source.BaseManaMaximum,
            NativeLifeBase = source.HasNativeMana ? checked(source.Life + source.BaseLifeMaximum - source.LifeMaximum) : null,
            NativeManaBase = source.HasNativeMana ? checked(source.NativeMana + source.BaseManaMaximum - source.NativeManaMaximum) : null,
            CharacterLevel = source.CharacterLevel,
            CharacterClassId = source.CharacterClassId,
            CharacterName = source.CharacterName,
            ManaShieldActive = source.ManaShieldActive,
            ReflectCharges = source.ReflectCharges,
            DiabloKillLevel = source.DiabloKillLevel,
            StatPoints = source.StatPoints,
            VisitedLevelIds = source.VisitedLevelIds.ToArray(),
            AbilitySpellId = source.AbilitySpellId,
            AbilityKind = (DiabloPlayerAbilityKind)source.AbilityKind,
            FullyExploredLevelIds = source.FullyExploredLevelIds.Count == 0 ? null : source.FullyExploredLevelIds.ToArray(),
            MemorizedSpells = source.MemorizedSpells.Concat(source.ZeroLevelMemorizedSpells).Select(spell =>
                new MemorizedSpellState(spell.SpellId, spell.Level)).ToArray(),
            LevelId = source.LevelId,
            EntityId = source.EntityId,
            StatusEffects = source.StatusEffects.Select(effect =>
                new AuthoritativeStatusEffect(
                    effect.EffectId,
                    effect.RemainingTicks,
                    effect.Magnitude) {
                    Behavior = (AuthoritativeStatusEffectBehavior)effect.Behavior,
                }).ToArray(),
            FriendlyMode = !source.HostileMode,
            FacingDirection = (DiabloPlayerDirection)source.FacingDirection,
            CombatMode = (DiabloPlayerCombatMode)source.CombatMode,
            CombatModeEndTick = source.CombatModeEndTick,
            CurrentAction = FromProtocolPlayerAction(source.CurrentAction),
            HeldItem = source.HeldItem is null
                ? null
                : new OwnedStoreItem(
                    source.HeldItem.StoreId,
                    source.HeldItem.StoreSlot,
                    source.HeldItem.ItemSeed,
                    source.HeldItem.Price,
                    source.HeldItem.PurchasedAtTick,
                    FromProtocolItemState(source.HeldItem.State)),
            Stash = source.Stash.Select(item => new StashStoreItem(
                item.Page,
                item.Cell,
                item.Item?.StoreId ?? 0,
                item.Item?.StoreSlot ?? 0,
                item.Item?.ItemSeed ?? 0,
                item.Item?.Price ?? 0,
                item.Item?.PurchasedAtTick ?? 0,
                FromProtocolItemState(item.Item?.State))).ToArray(),
            StashGold = source.StashGold,
            StashOpen = source.StashOpen,
        };
    }

    private static PlayerActionSnapshot ToProtocolPlayerAction(
        AuthoritativePlayerActionState source)
    {
        var result = new PlayerActionSnapshot {
            Kind = (PlayerActionKind)source.Kind,
            StartedAtTick = source.StartedAtTick,
            EffectTick = source.EffectTick,
            ReadyTick = source.ReadyTick,
            EndTick = source.EndTick,
            EffectApplied = source.EffectApplied,
            DirectionX = source.DirectionX,
            DirectionY = source.DirectionY,
            TargetEntityId = source.TargetEntityId,
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            TargetCell = source.TargetCell,
        };
        if (source.CastIntent is not null) {
            result.Cast = new CastRequested {
                SpellId = source.CastIntent.SpellId,
                TargetEntityId = source.CastIntent.TargetEntityId,
                TargetX = source.CastIntent.TargetX,
                TargetY = source.CastIntent.TargetY,
                Source = (CastSourceKind)source.CastIntent.Source,
            };
            if (source.CastIntent.SourceItem is DiabloPlayerItemReference item) {
                result.Cast.SourceItem = new PlayerItemReference {
                    Location = (PlayerItemLocation)item.Location,
                    Slot = item.Slot,
                    StableItemId = item.StableItemId,
                    Page = item.Page,
                };
            }
        }
        return result;
    }

    private static AuthoritativePlayerActionState? FromProtocolPlayerAction(
        PlayerActionSnapshot? source)
    {
        if (source is null)
            return null;
        DiabloCastIntent? castIntent = null;
        if (source.Cast is not null) {
            var sourceItem = source.Cast.SourceItem is null
                ? (DiabloPlayerItemReference?)null
                : new DiabloPlayerItemReference(
                    (DiabloPlayerItemLocation)source.Cast.SourceItem.Location,
                    source.Cast.SourceItem.Slot,
                    source.Cast.SourceItem.StableItemId,
                    source.Cast.SourceItem.Page);
            castIntent = new DiabloCastIntent(
                source.Cast.SpellId,
                source.Cast.TargetEntityId,
                source.Cast.TargetX,
                source.Cast.TargetY,
                (DiabloCastSourceKind)source.Cast.Source,
                sourceItem);
        }
        return new AuthoritativePlayerActionState(
            (DiabloPlayerActionKind)source.Kind,
            source.StartedAtTick,
            source.EffectTick,
            source.ReadyTick,
            source.EndTick) {
            EffectApplied = source.EffectApplied,
            DirectionX = source.DirectionX,
            DirectionY = source.DirectionY,
            TargetEntityId = source.TargetEntityId,
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            TargetCell = source.TargetCell,
            CastIntent = castIntent,
        };
    }

    private static int ResolveQ6(int whole, int raw)
    {
        return raw == 0 && whole != 0
            ? DeterministicFixedPointQ6.FromWhole(whole)
            : raw;
    }

    private static AttributeSnapshot ToProtocolAttribute(PlayerAttributeState attribute)
    {
        return new AttributeSnapshot { Base = attribute.Base, Current = attribute.Current };
    }

    private static PlayerAttributeState FromProtocolAttribute(AttributeSnapshot? attribute)
    {
        return new PlayerAttributeState(attribute?.Base ?? 0, attribute?.Current ?? 0);
    }

    private static AuthoritativeItemState FromProtocolItemState(ItemStateSnapshot? source)
    {
        source ??= new ItemStateSnapshot { ItemType = -1, ItemIndex = -1 };
        return AuthoritativeItemState.Empty with {
            CreateInfo = source.CreateInfo,
            ItemType = source.ItemType,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            Deleted = source.Deleted,
            Identified = source.Identified,
            Magical = source.Magical,
            EquipLocation = source.EquipLocation,
            ItemClass = source.ItemClass,
            Value = source.Value,
            IdentifiedValue = source.IdentifiedValue,
            MinDamage = source.MinDamage,
            MaxDamage = source.MaxDamage,
            ArmorClass = source.ArmorClass,
            Flags = source.Flags,
            MiscId = source.MiscId,
            SpellId = source.SpellId,
            SpellSourceKind = source.SpellSourceKind == ItemSpellSourceKind.Unspecified
                ? null
                : (DiabloItemSpellSourceKind)source.SpellSourceKind,
            ItemActionKind = source.ItemActionKind == ItemActionKind.Unspecified
                ? DiabloItemActionKind.None
                : (DiabloItemActionKind)source.ItemActionKind,
            StableItemId = source.StableItemId,
            ItemIndex = source.ItemIndex,
            Charges = source.Charges,
            MaxCharges = source.MaxCharges,
            Durability = source.Durability,
            MaxDurability = source.MaxDurability,
            PlusDamage = source.PlusDamage,
            PlusToHit = source.PlusToHit,
            PlusArmorClass = source.PlusArmorClass,
            PlusStrength = source.PlusStrength,
            PlusMagic = source.PlusMagic,
            PlusDexterity = source.PlusDexterity,
            PlusVitality = source.PlusVitality,
            PlusFireResistance = source.PlusFireResistance,
            PlusLightningResistance = source.PlusLightningResistance,
            PlusMagicResistance = source.PlusMagicResistance,
            PlusMana = source.PlusMana,
            PlusHitPoints = source.PlusHitPoints,
            ManaToLifePercent = source.ManaToLifePercent,
            LifeToManaPercent = source.LifeToManaPercent,
            PlusDamageModifier = source.PlusDamageModifier,
            PlusGetHit = source.PlusGetHit,
            PlusLight = source.PlusLight,
            SpellLevelAdd = source.SpellLevelAdd,
            UniqueId = source.UniqueId,
            FireMinDamage = source.FireMinDamage,
            FireMaxDamage = source.FireMaxDamage,
            LightningMinDamage = source.LightningMinDamage,
            LightningMaxDamage = source.LightningMaxDamage,
            PlusEnemyArmorClass = source.PlusEnemyArmorClass,
            PrefixPower = source.PrefixPower,
            SuffixPower = source.SuffixPower,
            ValueAdd1 = source.ValueAdd1,
            ValueMultiply1 = source.ValueMultiply1,
            ValueAdd2 = source.ValueAdd2,
            ValueMultiply2 = source.ValueMultiply2,
            MinimumStrength = source.MinimumStrength,
            MinimumMagic = source.MinimumMagic,
            MinimumDexterity = source.MinimumDexterity,
            StatFlag = source.StatFlag,
            HellfireDamageArmorFlags = source.HellfireDamageArmorFlags,
            Buff = source.Buff,
            InventoryWidth = source.InventoryWidth == 0 ? 1 : (int)source.InventoryWidth,
            InventoryHeight = source.InventoryHeight == 0 ? 1 : (int)source.InventoryHeight,
            BaseName = source.BaseName,
            IdentifiedName = source.IdentifiedName,
            NativeCursorGraphic = source.HasNativeCursorGraphic
                ? source.NativeCursorGraphic
                : null,
        };
    }

    private static ItemStateSnapshot ToProtocolItemState(
        uint itemSeed,
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells,
        AuthoritativeSpellBookCatalog? spellBooks,
        AuthoritativeItemCatalog? items)
    {
        ValidateProjectedItemState(itemSeed, state, spells);
        var snapshot = new ItemStateSnapshot {
            CreateInfo = state.CreateInfo,
            ItemType = state.ItemType,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            Deleted = state.Deleted,
            Identified = state.Identified,
            Magical = state.Magical,
            EquipLocation = state.EquipLocation,
            ItemClass = state.ItemClass,
            Value = state.Value,
            IdentifiedValue = state.IdentifiedValue,
            MinDamage = state.MinDamage,
            MaxDamage = state.MaxDamage,
            ArmorClass = state.ArmorClass,
            Flags = state.Flags,
            MiscId = state.MiscId,
            SpellId = state.SpellId,
            ItemIndex = state.ItemIndex,
            Charges = state.Charges,
            MaxCharges = state.MaxCharges,
            Durability = state.Durability,
            MaxDurability = state.MaxDurability,
            PlusDamage = state.PlusDamage,
            PlusToHit = state.PlusToHit,
            PlusArmorClass = state.PlusArmorClass,
            PlusStrength = state.PlusStrength,
            PlusMagic = state.PlusMagic,
            PlusDexterity = state.PlusDexterity,
            PlusVitality = state.PlusVitality,
            PlusFireResistance = state.PlusFireResistance,
            PlusLightningResistance = state.PlusLightningResistance,
            PlusMagicResistance = state.PlusMagicResistance,
            PlusMana = state.PlusMana,
            PlusHitPoints = state.PlusHitPoints,
            ManaToLifePercent = state.ManaToLifePercent,
            LifeToManaPercent = state.LifeToManaPercent,
            PlusDamageModifier = state.PlusDamageModifier,
            PlusGetHit = state.PlusGetHit,
            PlusLight = state.PlusLight,
            SpellLevelAdd = state.SpellLevelAdd,
            UniqueId = state.UniqueId,
            FireMinDamage = state.FireMinDamage,
            FireMaxDamage = state.FireMaxDamage,
            LightningMinDamage = state.LightningMinDamage,
            LightningMaxDamage = state.LightningMaxDamage,
            PlusEnemyArmorClass = state.PlusEnemyArmorClass,
            PrefixPower = state.PrefixPower,
            SuffixPower = state.SuffixPower,
            ValueAdd1 = state.ValueAdd1,
            ValueMultiply1 = state.ValueMultiply1,
            ValueAdd2 = state.ValueAdd2,
            ValueMultiply2 = state.ValueMultiply2,
            MinimumStrength = state.MinimumStrength,
            MinimumMagic = state.MinimumMagic,
            MinimumDexterity = state.MinimumDexterity,
            StatFlag = state.StatFlag,
            HellfireDamageArmorFlags = state.HellfireDamageArmorFlags,
            Buff = state.Buff,
            InventoryWidth = state.InventoryWidth > 1 ? (uint)state.InventoryWidth : 0,
            InventoryHeight = state.InventoryHeight > 1 ? (uint)state.InventoryHeight : 0,
            BaseName = state.BaseName,
            IdentifiedName = state.IdentifiedName,
            SpellSourceKind = (ItemSpellSourceKind)(state.SpellSourceKind
                ?? DiabloCastSourceRules.ClassifyItem(state, spells)),
            ItemActionKind = (ItemActionKind)state.ItemActionKind,
            SpellDisplayName = state.SpellId > 0
                ? ResolveSpellDisplayName(checked((uint)state.SpellId), spells, spellBooks)
                : string.Empty,
            SpellTargetingKind = state.SpellId > 0
                ? ResolveSpellTargetingKind(checked((uint)state.SpellId), spells)
                : SpellTargetingKind.Unspecified,
            PresentationKind = ResolveItemPresentationKind(state, items),
            StableItemId = state.StableItemId,
        };
        if (state.NativeCursorGraphic.HasValue)
            snapshot.NativeCursorGraphic = state.NativeCursorGraphic.Value;
        return snapshot;
    }

    private static void ValidateProjectedItemState(
        uint itemSeed,
        AuthoritativeItemState state,
        AuthoritativeSpellCatalog? spells)
    {
        var spellSourceKind = state.SpellSourceKind
            ?? DiabloCastSourceRules.ClassifyItem(state, spells);
        var hasPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.NativeCursorGraphic.HasValue;
        if (!hasPresentation && itemSeed == 0
            || state.Value < 0
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
            || state.InventoryWidth < 0
            || state.InventoryWidth > NativeInventoryWidth
            || state.InventoryHeight < 0
            || state.InventoryHeight > NativeInventoryHeight
            || hasPresentation
                && (string.IsNullOrEmpty(state.BaseName)
                    || string.IsNullOrEmpty(state.IdentifiedName)
                    || !state.NativeCursorGraphic.HasValue
                    || state.BaseName.Contains('\0')
                    || state.IdentifiedName.Contains('\0')
                    || System.Text.Encoding.UTF8.GetByteCount(state.BaseName) >= 64
                    || System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) >= 64
                    || state.NativeCursorGraphic is < 0 or > byte.MaxValue)
            || !Enum.IsDefined(spellSourceKind)
            || !Enum.IsDefined(state.ItemActionKind)) {
            throw new InvalidDataException(
                "Authoritative item projection contains impossible state ranges.");
        }
    }

    private static bool IsNativeItemStateRepresentable(AuthoritativeItemState state)
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

    private static ItemPresentationKind ResolveItemPresentationKind(
        AuthoritativeItemState state,
        AuthoritativeItemCatalog? items)
    {
        if (items is not null && items.TryGetByState(state, out var definition)) {
            if (definition.GenerationTags.Contains("Gold", StringComparer.OrdinalIgnoreCase))
                return ItemPresentationKind.Gold;
            return definition.GenerationTags.Contains("Ear", StringComparer.OrdinalIgnoreCase)
                ? ItemPresentationKind.Ear
                : ItemPresentationKind.Unspecified;
        }
        if (state.ItemType == 11 || state.ItemClass == 4)
            return ItemPresentationKind.Gold;
        if (state.MiscId == 44 && state.ItemClass == 3)
            return ItemPresentationKind.Ear;
        // Keep the additive field at its zero value for ordinary items so
        // legacy snapshots and clients remain byte-for-byte compatible.
        return ItemPresentationKind.Unspecified;
    }

}

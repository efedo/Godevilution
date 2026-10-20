using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private AuthoritativeCathedralItemRuntime? cathedralItems;

    public bool HasNativeCathedralItems => cathedralItems is not null;
    public string? CathedralItemMetadataFingerprint => cathedralItems?.Profile.MetadataFingerprint;
    public string? CathedralItemGenerationFingerprint => cathedralItems?.Profile.GenerationFingerprint;

    internal void ConfigureCathedralItems(AuthoritativeCathedralItemRuntimeProfile? profile,
        DiabloCathedralItemCheckpoint? checkpoint, string? monsterMetadataFingerprint,
        AuthoritativeItemCatalog? contentItems, AuthoritativeSpellCatalog spells)
        => ConfigureCathedralItemsCore(profile, checkpoint, monsterMetadataFingerprint, contentItems, spells, null, null);

    /** Mixed-family admission supplies the exact immutable owner for every item level. */
    internal void ConfigureNativeFamilyItems(AuthoritativeCathedralItemRuntimeProfile profile,
        DiabloCathedralItemCheckpoint? checkpoint, IReadOnlyList<AuthoritativeNativePopulationBindings> owners,
        AuthoritativeItemCatalog contentItems, AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(owners);
        if (owners.Count == 0 || owners.Select(owner => owner.LevelId).Distinct().Count() != owners.Count
            || !owners.Select(owner => owner.LevelId).Order()
                .SequenceEqual(profile.LevelProfiles.Select(level => level.LevelId).Order())
            || owners.Any(owner => !ReferenceEquals(NativePopulationForLevel(owner.LevelId), owner))
            || profile.GenerationFingerprint != AuthoritativeNativePopulationBindings.ComputeFamilyItemGenerationFingerprint(owners))
            throw new InvalidDataException("Native family items require every exact registered floor owner.");
        var contexts = owners.ToDictionary(owner => owner.LevelId, owner => owner.ItemFamilyContext);
        var metadata = owners.ToDictionary(owner => owner.LevelId, owner => owner.MonsterMetadataFingerprint);
        ConfigureCathedralItemsCore(profile, checkpoint, null, contentItems, spells, contexts, metadata);
    }

    private void ConfigureCathedralItemsCore(AuthoritativeCathedralItemRuntimeProfile? profile,
        DiabloCathedralItemCheckpoint? checkpoint, string? monsterMetadataFingerprint,
        AuthoritativeItemCatalog? contentItems, AuthoritativeSpellCatalog spells,
        IReadOnlyDictionary<uint, AuthoritativeNativeDungeonFamilyContext>? familyContexts,
        IReadOnlyDictionary<uint, string>? monsterMetadataByLevel)
    {
        if (profile is null) {
            if (checkpoint is not null)
                throw new InvalidDataException("Native item restore requires its explicit profile.");
            return;
        }
        if (monsterMetadataByLevel is null
            ? !AuthoritativeNativeMonsterInitialization.IsFingerprint(monsterMetadataFingerprint)
            : monsterMetadataByLevel.Values.Any(value => !AuthoritativeNativeMonsterInitialization.IsFingerprint(value)))
            throw new InvalidDataException("Native item ownership requires native monster metadata even with an empty roster.");
        if (cathedralItems is not null)
            throw new InvalidOperationException("Native item ownership is configured once on a detached world.");
        if (contentItems is null)
            throw new InvalidDataException("Native item projections require their stable content catalog.");
        if (Quests.Count != 0)
            throw new InvalidDataException("Native Cathedral death loot currently requires quests disabled.");
        var bindingMismatches = new List<string>();
        foreach (var binding in profile.ItemBindings.Values) {
            var definition = contentItems.Definitions.SingleOrDefault(value => value.ItemId == binding.StableItemId);
            if (definition is null || definition.NativeItemIndex != binding.NativeItemIndex
                || definition.InventoryWidth != binding.InventoryWidth || definition.InventoryHeight != binding.InventoryHeight
                || !MatchesStableItemSymbol(contentItems, definition, binding.StableSymbol)
                || !MatchesItemBindingSemantics(contentItems, spells, definition, binding))
                bindingMismatches.Add($"{binding.NativeItemIndex}/{binding.StableItemId}/{binding.StableSymbol} "
                    + $"binding={binding.InventoryWidth}x{binding.InventoryHeight}/{binding.SpellSourceKind}/{binding.ItemActionKind} "
                    + $"catalog={definition?.StableSymbol}/{definition?.InventoryWidth}x{definition?.InventoryHeight}/"
                    + $"{definition?.SpellSourceKind}/{definition?.ItemActionKind}");
        }
        if (bindingMismatches.Count != 0)
            throw new InvalidDataException("Native item content bindings disagree with the active item catalog: "
                + string.Join("; ", bindingMismatches));
        foreach (var binding in profile.NativeSpellBindings ?? new Dictionary<int, int>()) {
            if (binding.Key == 0 && binding.Value == 0)
                continue; // Native spell None is not a castable content definition.
            if (!spells.TryGetByNativeId(checked((uint)binding.Key), out var spell) || spell.SpellId != binding.Value)
                throw new InvalidDataException("Native item spell bindings disagree with the active spell catalog.");
        }
        var candidate = new AuthoritativeCathedralItemRuntime(
            profile, checkpoint, contentItems.NativeGoldStackMetadata, familyContexts);
        var projected = new Dictionary<uint, AuthoritativeWorldItem>();
        foreach (var level in candidate.CaptureCheckpoint().Levels) {
            foreach (var binding in level.SlotEntityBindings.Where(value => value.EntityId != 0)) {
                var raw = level.Items[binding.Slot];
                var value = candidate.ProjectNativeItem(binding.Slot, binding.EntityId, raw.Seed,
                    checked((uint)Math.Max(0, raw.Identified ? raw.IdentifiedValue : raw.Value)), raw,
                    profile.ItemBindings[raw.ItemIndex], profile.ContentInventoryDimensions);
                projected.Add(binding.EntityId, ToWorldItem(level.LevelId, value));
            }
        }
        var ownedLevels = candidate.CaptureCheckpoint().Levels.Select(value => value.LevelId).ToHashSet();
        var existingOwnedItems = WorldItems.Values.Where(value => ownedLevels.Contains(value.LevelId)).ToArray();
        var publishInitialPool = checkpoint is null && existingOwnedItems.Length == 0;
        if (!publishInitialPool
            && (existingOwnedItems.Length != projected.Count || projected.Values.Any(value =>
                !WorldItems.TryGetValue(value.EntityId, out var actual) || !SameWorldItem(actual, value))))
            throw new InvalidDataException("Native raw item pools disagree with their live world projection.");
        if (publishInitialPool) {
            foreach (var value in projected.Values)
                if (IsEntityIdOccupied(value.EntityId))
                    throw new InvalidDataException("A native item binding collides with another world entity.");
        }
        // Validate all object provenance before publishing any initial items.
        foreach (var level in profile.LevelProfiles)
            BuildCathedralItemOccupancy(profile, level.LevelId, []);
        foreach (var target in CombatTargets.Values.Where(value => candidate.HasLevel(value.LevelId))) {
            var expectedMetadata = monsterMetadataByLevel is null
                ? monsterMetadataFingerprint : monsterMetadataByLevel[target.LevelId];
            if (target.NativeInitialization is not { } native || native.MetadataFingerprint != expectedMetadata
                || target.Drop is not null)
                throw new InvalidDataException("Native item levels require initialized actors without authored drops.");
        }
        if (publishInitialPool)
            foreach (var value in projected.Values)
                WorldItems.Add(value.EntityId, value);
        cathedralItems = candidate;
    }

    public DiabloCathedralItemCheckpoint? CaptureCathedralItems()
    {
        if (cathedralItems is null)
            return null;
        ValidateCathedralItemProjection();
        foreach (var level in cathedralItems.Profile.LevelProfiles)
            BuildCathedralItemOccupancy(cathedralItems.Profile, level.LevelId, []);
        return cathedralItems.CaptureCheckpoint();
    }

    internal void AdvanceCathedralItemAnimations(
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (cathedralItems is not null) {
            ValidateCathedralItemProjection();
            foreach (var levelId in participants.Select(player => player.LevelId).Distinct())
                if (cathedralItems.HasLevel(levelId))
                    cathedralItems.AdvanceGroundAnimations(levelId);
        }
        foreach (var levelId in participants.Select(player => player.LevelId).Distinct()) {
            AdvanceNativeSarcophagusAnimations(levelId);
            AdvanceNativeBarrelAnimations(levelId);
        }
    }

    internal bool IsNativeWorldItem(uint entityId) => cathedralItems?.IsBound(entityId) == true;
    internal bool HasNativeItemLevel(uint levelId) => cathedralItems?.HasLevel(levelId) == true;
    internal bool HasWorldItemCapacity(uint levelId) => !HasNativeItemLevel(levelId) || cathedralItems!.HasFreeSlot(levelId);

    /** Native CanPut ignores actor occupancy in Cathedral, unlike monster loot placement. */
    internal bool CanPutNativeItem(uint levelId, int x, int y, bool releasingPickupSlot = false)
    {
        if (!HasNativeItemLevel(levelId))
            throw new InvalidOperationException("Native item placement requires a configured level.");
        if ((!releasingPickupSlot && !HasWorldItemCapacity(levelId)) || (uint)x >= 112 || (uint)y >= 112)
            return false;
        var context = BuildCathedralItemOccupancy(cathedralItems!.Profile, levelId, []);
        var view = cathedralItems.BuildWorldState(levelId, context);
        var index = y * 112 + x;
        if (view.SolidTiles[index] || view.ItemRefs[index] != 0)
            return false;
        int? ObjectAt(int px, int py, bool large) {
            if ((uint)px >= 112 || (uint)py >= 112) return null;
            var reference = view.ObjectRefs[py * 112 + px];
            return reference > 0 || large && reference != 0 ? Math.Abs(reference) - 1 : null;
        }
        if (ObjectAt(x, y, true) is { } direct && view.ObjectSolid[direct]) return false;
        if (ObjectAt(x + 1, y + 1, true) is { } south && view.ObjectCanInteract[south]) return false;
        return !(ObjectAt(x + 1, y, false) is { } east && view.ObjectCanInteract[east]
            && ObjectAt(x, y + 1, false) is { } west && view.ObjectCanInteract[west]);
    }

    internal void ValidateNativeCursorDrop(AuthoritativePlayerState player)
    {
        if (!HasNativeItemLevel(player.LevelId) || player.HeldItem is not { } held)
            return;
        ValidateCathedralItemProjection();
        var validationEntityId = uint.MaxValue;
        while (validationEntityId != 0
            && (WorldItems.ContainsKey(validationEntityId) || cathedralItems!.IsBound(validationEntityId)))
            validationEntityId--;
        if (validationEntityId == 0)
            throw new InvalidOperationException("No unused item identity is available for cursor validation.");
        cathedralItems!.ProjectWorldItem(new(validationEntityId, player.LevelId, player.FuturePositionX,
            player.FuturePositionY, held.ItemSeed, held.Price, held.State), resetGroundAnimation: true);
    }

    internal AuthoritativeCathedralItemDeathTransaction? PrepareCathedralDeathLoot(
        AuthoritativeCombatTarget target, AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeCathedralThemeItemPlayerContext? lootPlayer = null)
    {
        if (!HasNativeItemLevel(target.LevelId))
            return null;
        ValidateCathedralItemProjection();
        var native = target.NativeInitialization
            ?? throw new InvalidDataException("Native item death requires the actor's native identity.");
        var unique = target.IsUnique ? cathedralItems!.Profile.UniqueDefinitions.SingleOrDefault(
            value => value.NativeUniqueId == native.Binding.NativeUniqueId) : null;
        if (target.IsUnique && unique is null)
            throw new InvalidDataException("Native item death requires its exact unique definition.");
        var participants = players.Count == 0 ? new[] { player } : players;
        return cathedralItems!.PrepareDeath(new(target.LevelId, native.Binding.NativeTypeId,
            target.MonsterItemSeed, target.PositionX, target.PositionY,
            BuildCathedralItemOccupancy(cathedralItems.Profile, target.LevelId, participants),
            target.IsUnique, target.IsPlayerMinion && !target.IsBerserked, unique,
            lootPlayer ?? new(true, false, player.BaseManaMaximum, player.BaseLifeMaximum)));
    }

    internal uint CommitCathedralDeathLoot(AuthoritativeCathedralItemDeathTransaction transaction,
        Func<uint> allocateEntityId)
    {
        var mutation = cathedralItems!.CommitDeath(transaction, () => {
            var entityId = allocateEntityId();
            if (IsEntityIdOccupied(entityId))
                throw new InvalidDataException("Native loot allocation collides with a live entity.");
            return entityId;
        });
        if (mutation.HasWorldItem)
            WorldItems.Add(mutation.EntityId, ToWorldItem(mutation.LevelId, mutation.Projection!));
        return transaction.Loot.FinalRandomState;
    }

    /** All production item writes share this boundary; the legacy dictionary API remains intact. */
    internal void AddWorldItem(AuthoritativeWorldItem item) => PutWorldItem(item, replace: false);
    internal void SetWorldItem(AuthoritativeWorldItem item) => PutWorldItem(item, replace: true);

    private void PutWorldItem(AuthoritativeWorldItem item, bool replace)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!replace && WorldItems.ContainsKey(item.EntityId))
            throw new ArgumentException("A world item entity is already present.");
        if (HasNativeItemLevel(item.LevelId)) {
            ValidateCathedralItemProjection();
            var projection = cathedralItems!.ProjectWorldItem(item, resetGroundAnimation: !WorldItems.ContainsKey(item.EntityId));
            var changed = WorldItems.ContainsKey(item.EntityId)
                ? cathedralItems.TryReplaceWorldItem(item.LevelId, item.EntityId, projection)
                : cathedralItems.TryAddWorldItem(item.LevelId, projection, out _);
            if (!changed)
                throw new InvalidDataException("Native item mutation has no free slot or valid item tile.");
            item = ToWorldItem(item.LevelId, projection);
        } else if (cathedralItems?.IsBound(item.EntityId) == true) {
            throw new InvalidDataException("A native world item cannot move between levels without releasing its slot.");
        }
        if (replace) WorldItems[item.EntityId] = item;
        else WorldItems.Add(item.EntityId, item);
    }

    internal bool RemoveWorldItem(uint entityId, AuthoritativeItemState? pickedUpState = null)
    {
        if (!WorldItems.TryGetValue(entityId, out var item)) return false;
        if (HasNativeItemLevel(item.LevelId)) {
            ValidateCathedralItemProjection();
            if (!cathedralItems!.RemoveWorldItem(item, pickedUpState))
                throw new InvalidDataException("A native world item has no owned raw pool slot.");
        }
        return WorldItems.Remove(entityId);
    }

    private void ValidateCathedralItemProjection()
    {
        foreach (var level in cathedralItems!.CaptureCheckpoint().Levels) {
            var expected = level.SlotEntityBindings.Where(value => value.EntityId != 0).ToArray();
            if (WorldItems.Values.Count(value => value.LevelId == level.LevelId) != expected.Length)
                throw new InvalidDataException("The native item projection was mutated outside its owner.");
            foreach (var binding in expected) {
                var projection = cathedralItems.GetProjection(level.LevelId, binding.EntityId)
                    ?? throw new InvalidDataException("A native item binding has no raw projection.");
                if (!WorldItems.TryGetValue(binding.EntityId, out var value)
                    || !SameWorldItem(value, ToWorldItem(level.LevelId, projection)))
                    throw new InvalidDataException("The native item projection was mutated outside its owner.");
            }
        }
    }

    internal AuthoritativeItemState ResolveNativePickupState(AuthoritativeWorldItem item,
        AuthoritativePlayerState player)
    {
        ValidateCathedralItemProjection();
        var projection = cathedralItems!.GetProjection(item.LevelId, item.EntityId)
            ?? throw new InvalidDataException("Native pickup has no owned raw item.");
        var state = item.State;
        var minimumMagic = state.MinimumMagic;
        if (projection.Raw.ItemType == 0 && projection.Raw.MiscId == 24) {
            var spell = cathedralItems.Profile.ItemCatalog.Spells.Single(value => value.NativeSpellId == projection.Raw.SpellId);
            minimumMagic = spell.MinimumMagic;
            var spellLevel = player.MemorizedSpells.Where(value => value.SpellId == state.SpellId)
                .Select(value => value.Level).FirstOrDefault();
            while (spellLevel-- > 0) {
                minimumMagic = unchecked((byte)(minimumMagic + 20 * minimumMagic / 100));
                if (minimumMagic + 20 * minimumMagic / 100 > 255) { minimumMagic = 255; break; }
            }
        }
        return state with {
            CreateInfo = state.CreateInfo & ~0x8000U,
            // The owned Gold API uses equal wallet values; native ground _iIvalue remains untouched.
            IdentifiedValue = projection.Raw.ItemType == 11 ? state.Value : state.IdentifiedValue,
            MinimumMagic = minimumMagic,
            StatFlag = player.Attributes.Strength.Current >= state.MinimumStrength
                && player.Attributes.Magic.Current >= minimumMagic
                && player.Attributes.Dexterity.Current >= state.MinimumDexterity,
        };
    }

    internal AuthoritativeItemState NativeGoldPickupRemainder(AuthoritativeWorldItem item,
        AuthoritativeItemState state, uint remainder)
    {
        if (!IsNativeWorldItem(item.EntityId)) return state;
        var cursors = cathedralItems!.Profile.ItemCatalog.Cursors;
        return state with { Value = checked((int)remainder), NativeCursorGraphic = remainder >= 2500
            ? cursors.GoldLarge : remainder <= 1000 ? cursors.GoldSmall : cursors.GoldMedium };
    }

    private AuthoritativeCathedralItemOccupancyContext BuildCathedralItemOccupancy(
        AuthoritativeCathedralItemRuntimeProfile profile, uint levelId,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var definition = profile.LevelProfiles.Single(value => value.LevelId == levelId);
        var solid = definition.SolidTiles.ToArray();
        if (World is not null) {
            if (!projectedLevels.TryGetValue(levelId, out var level) || level.Width != 112 || level.Height != 112
                || level.NativePieceLayer.Count != 112 * 112)
                throw new InvalidDataException("Native item levels require their complete current d-piece projection.");
            for (var index = 0; index < solid.Length; index++) {
                if (!level.NativePieceProperties.TryGetValue(level.NativePieceLayer[index], out var flags))
                    throw new InvalidDataException("Native item placement is missing a current SOL row.");
                solid[index] = (flags & AuthoritativeDunTileFlags.Solid) != 0;
            }
        } else if (fallbackWidth != 112 || fallbackHeight != 112
            || Enumerable.Range(0, solid.Length).Any(index => solid[index] != fallbackBlockedCells.Contains(index))) {
            throw new InvalidDataException("Native item fallback geometry disagrees with its explicit placement profile.");
        }
        var objectRefs = definition.InitialObjectRefs.ToArray();
        var objectSolid = new bool[127];
        var objectInteractive = new bool[127];
        var boundEntities = new HashSet<uint>();
        foreach (var binding in definition.ObjectBindings) {
            if (!Objects.TryGetValue(binding.EntityId, out var value) || value.LevelId != levelId
                || value.NativeObjectId != binding.ObjectType || value.PositionX != binding.PositionX
                || value.PositionY != binding.PositionY || !boundEntities.Add(binding.EntityId))
                throw new InvalidDataException("Native object placement provenance changed outside its supported policy.");
            var isSolid = binding.InitialSolid;
            var isInteractive = binding.InitialSelectionRegion != 0;
            switch (binding.MutationPolicy) {
            case AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor:
                if (value.CathedralDoor is not { } door)
                    throw new InvalidDataException("Native door placement requires live door state.");
                isSolid = door.SolidFlag; isInteractive = door.SelectionRegion != 0;
                break;
            case AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor:
                ValidateNativePopulationObject(value);
                if (!TryGetNativeCatacombsDoorState(value.EntityId, out var catacombsDoor))
                    throw new InvalidDataException("Native C2 door placement requires its exact live door owner.");
                isSolid = catacombsDoor.SolidFlag; isInteractive = catacombsDoor.SelectionRegion != 0;
                break;
            case AuthoritativeCathedralItemObjectMutationPolicy.NativeChest:
            case AuthoritativeCathedralItemObjectMutationPolicy.NativeSarcophagus:
                ValidateNativePopulationObject(value);
                if (value.Destroyed || value.Breakable || value.CathedralDoor is not null)
                    throw new InvalidDataException("A native loot object has unsupported object behavior.");
                isInteractive = binding.InitialSelectionRegion != 0 && !value.Activated;
                break;
            case AuthoritativeCathedralItemObjectMutationPolicy.NativeBarrel:
                ValidateNativePopulationObject(value);
                if (value.Destroyed || value.Breakable || value.CathedralDoor is not null)
                    throw new InvalidDataException("A native barrel has unsupported object behavior.");
                isSolid = !value.Activated;
                isInteractive = binding.InitialSelectionRegion != 0 && !value.Activated;
                break;
            case AuthoritativeCathedralItemObjectMutationPolicy.Immutable:
                if (value.Activated || value.Destroyed || value.Breakable || value.HasAuthoritativeInteraction)
                    throw new InvalidDataException("An immutable native object acquired unsupported gameplay state.");
                break;
            case AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused:
                ValidateNativePopulationObject(value);
                if (value.Activated || value.Destroyed)
                    throw new InvalidDataException("An explicitly refused native object acquired unsupported gameplay state.");
                break;
            default:
                throw new NotSupportedException("Native item placement does not yet admit this object mutation policy.");
            }
            objectSolid[binding.NativeObjectSlot] = isSolid;
            objectInteractive[binding.NativeObjectSlot] = isInteractive;
        }
        if (Objects.Values.Any(value => value.LevelId == levelId && !boundEntities.Contains(value.EntityId))
            || objectRefs.Any(reference => reference != 0 && !definition.ObjectBindings.Any(
                value => value.NativeObjectSlot == Math.Abs(reference) - 1)))
            throw new InvalidDataException("Native item placement has an unbound object or signed footprint.");
        var playerRefs = new int[112 * 112];
        var monsterRefs = new int[112 * 112];
        void Mark(int[] grid, int x, int y, int marker) {
            if ((uint)x >= 112 || (uint)y >= 112)
                throw new InvalidDataException("Native actor occupancy is outside its level.");
            var index = y * 112 + x;
            if (grid[index] != 0 && Math.Abs(grid[index]) != Math.Abs(marker))
                throw new InvalidDataException("Native actor occupancy has conflicting bindings.");
            grid[index] = marker;
        }
        var localPlayers = players.Where(value => value.LevelId == levelId).ToArray();
        if (localPlayers.Length > 1)
            throw new NotSupportedException("The current native item policy is single-player only.");
        foreach (var player in localPlayers) {
            if (player.Life <= 0)
                continue;
            Mark(playerRefs, player.PositionX, player.PositionY, 1);
            if (player.IsMoving) Mark(playerRefs, player.FuturePositionX, player.FuturePositionY, -1);
        }
        foreach (var monster in CombatTargets.Values.Where(value => value.LevelId == levelId
            && (value.HitPointsQ6 > 0 || value.DeathTicksRemaining > 0 || value.IsPetrified))) {
            var native = monster.NativeInitialization
                ?? throw new InvalidDataException("Native item placement has an unbound monster.");
            var marker = native.InitialState.Slot + 1;
            Mark(monsterRefs, monster.PositionX, monster.PositionY, marker);
            if (monster.IsMoving) Mark(monsterRefs, monster.FuturePositionX, monster.FuturePositionY, -marker);
        }
        return new(playerRefs, monsterRefs, objectRefs, solid, definition.SetPieceTiles, objectSolid, objectInteractive);
    }

    private static AuthoritativeWorldItem ToWorldItem(uint levelId, AuthoritativeCathedralNativeItemProjection value)
        => new(value.EntityId, levelId, value.Raw.PositionX, value.Raw.PositionY,
            value.ItemSeed, value.Price, value.State);

    private static bool MatchesStableItemSymbol(AuthoritativeItemCatalog contentItems,
        AuthoritativeItemDefinition definition, string stableSymbol)
    {
        if (!string.IsNullOrEmpty(definition.StableSymbol))
            return string.Equals(definition.StableSymbol, stableSymbol, StringComparison.Ordinal);
        try {
            return contentItems.ResolveLegacySymbol(stableSymbol) == definition.ItemId;
        } catch (InvalidDataException) {
            return false;
        }
    }

    private static bool MatchesItemBindingSemantics(AuthoritativeItemCatalog contentItems,
        AuthoritativeSpellCatalog spells, AuthoritativeItemDefinition definition,
        AuthoritativeCathedralItemContentBinding binding)
    {
        var state = new AuthoritativeItemState(
            0,
            definition.ItemType,
            0,
            0,
            false,
            false,
            0,
            definition.EquipLocation,
            definition.ItemClass,
            definition.Value,
            definition.IdentifiedValue,
            definition.MinDamage,
            definition.MaxDamage,
            definition.MinArmorClass,
            definition.Flags,
            definition.MiscId,
            definition.SpellId,
            definition.NativeItemIndex,
            definition.Charges,
            definition.MaxCharges,
            definition.Durability,
            definition.MaxDurability) {
            StableItemId = definition.ItemId,
        };
        var expectedSpellSource = DiabloCastSourceRules.ClassifyItem(state, spells, contentItems);
        if (binding.SpellSourceKind is { } spellSource && spellSource != expectedSpellSource
            && !(expectedSpellSource == DiabloItemSpellSourceKind.None
                && IsLegacySpellTemplate(definition, spellSource)))
            return false;

        // Explicit bound scroll semantics must not inherit the legacy Misc21
        // Identify fallback when both catalog and native binding select None.
        var boundSpellAction = binding.ItemActionKind == DiabloItemActionKind.None
            && definition.ItemActionKind == DiabloItemActionKind.None
            && definition.MiscId == 21 && definition.SpellId > 0
            && definition.SpellSourceKind == DiabloItemSpellSourceKind.ConsumableItem;
        DiabloItemActionKind? expectedAction = definition.ItemActionKind != DiabloItemActionKind.None
            || boundSpellAction ? definition.ItemActionKind : null;
        if (expectedAction is null
            && contentItems.TryGetItemActionMetadataByMiscId(definition.MiscId, out var miscAction, out _))
            expectedAction = miscAction;
        if (expectedAction is null
            && (definition.MiscId == 25
                || definition.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase)
                || definition.BookTemplateMinimumItemLevel >= 0))
            expectedAction = DiabloItemActionKind.ReadBook;
        return (binding.ItemActionKind ?? DiabloItemActionKind.None)
            == (expectedAction ?? DiabloItemActionKind.None);
    }

    private static bool IsLegacySpellTemplate(
        AuthoritativeItemDefinition definition, DiabloItemSpellSourceKind spellSource)
        => definition.SpellSourceKind is null
            && (spellSource == DiabloItemSpellSourceKind.ChargedItem
                && definition.MaxCharges > 0
                && definition.Usable != true
                && (definition.MiscId == 23
                    || definition.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase))
                || spellSource == DiabloItemSpellSourceKind.ConsumableItem
                    && definition.MiscId is 21 or 22 && definition.Usable == true);

    private static bool SameWorldItem(AuthoritativeWorldItem left, AuthoritativeWorldItem right)
        => left.EntityId == right.EntityId && left.LevelId == right.LevelId
            && left.PositionX == right.PositionX && left.PositionY == right.PositionY
            && left.ItemSeed == right.ItemSeed && left.Price == right.Price && left.State == right.State;
}

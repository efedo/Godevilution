using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Tests;

/** Public-table-only, genuinely bound Cathedral/Catacombs owners; no retail geometry or detached owner substitutes. */
internal sealed class NativeShrineTestFixture
{
    private const int Cells = 112 * 112;
    private const uint CorpseId = 0x09001004;
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly FieldInfo PopulationField = typeof(DiabloWorldState)
        .GetField("nativePopulationPolicy", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly Lazy<AuthoritativeCathedralPopulationMetadata> Cathedral = new(CreateCathedralMetadata);
    private static readonly Lazy<AuthoritativeCatacombsPopulationMetadata> Catacombs = new(CreateCatacombsMetadata);
    private static readonly Lazy<AuthoritativeCathedralMonsterRuntimeCatalog> MonsterCatalog = new(() =>
        AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(OpenTables().Single(value =>
            value.SourcePath == AuthoritativeCathedralMonsterRuntimeCatalog.TableName)));
    private readonly AuthoritativeNativePopulationBindings owner;
    private readonly AuthoritativeCathedralItemRuntimeProfile items;
    private readonly AuthoritativeCathedralMonsterRuntimeAdapterResult actors;
    private readonly uint level;
    internal const uint ShrineEntityId = 0x09003002;
    internal const uint AdditionalObjectEntityId = 0x09003003;
    internal DiabloSimulation Simulation { get; }
    internal DiabloWorldState World { get; }
    internal DiabloWorldCommandProcessor Rules { get; }
    internal DeterministicLegacyRandom Random { get; }
    internal AuthoritativeItemCatalog Items { get; }
    internal AuthoritativeSpellCatalog Spells { get; }
    internal AuthoritativePlayerClassCatalog Classes { get; }
    internal AuthoritativeSpellBookCatalog SpellBooks { get; }
    internal AuthoritativeCathedralItemRuntimeProfile ItemProfile => items;
    internal AuthoritativeWorldObject Shrine => World.Objects[ShrineEntityId];

    internal NativeShrineTestFixture(int depth, int shrineType, uint seed = 2588, DiabloGameMode mode = DiabloGameMode.SinglePlayer, DiabloCathedralItemCheckpoint? restoredItems = null, int chestItemCount = 0, int nativeObjectType = 0, bool nativeObjectSelectable = true, int additionalNativeObjectType = 0, bool productionItemBindings = false)
        : this(depth, shrineType, seed, Cathedral.Value.Items.Catalog, mode, restoredItems, chestItemCount, nativeObjectType, nativeObjectSelectable, additionalNativeObjectType, productionItemBindings) { }

    internal NativeShrineTestFixture(int depth, int shrineType, uint seed,
        AuthoritativeCathedralThemeItemCatalog nativeItems, DiabloGameMode mode, DiabloCathedralItemCheckpoint? restoredItems = null, int chestItemCount = 0, int nativeObjectType = 0, bool nativeObjectSelectable = true, int additionalNativeObjectType = 0, bool productionItemBindings = false)
    {
        level = (uint)depth;
        Spells = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(Path.Combine(RepositoryRoot(), "server/content/base/spells.tsv")));
        SpellBooks = AuthoritativeSpellBookCatalog.LoadTsv("spell_books.tsv", File.ReadAllText(Path.Combine(RepositoryRoot(), "server/content/base/spell_books.tsv")));
        Classes = AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", File.ReadAllText(Path.Combine(RepositoryRoot(), "server/content/base/classes.tsv")));
        Items = new AuthoritativeItemCatalog(nativeItems.Items.Select(item => new AuthoritativeItemDefinition(
            (uint)item.NativeItemIndex + 1, item.ItemType, item.Value, item.Value, item.MinimumDamage,
            item.MaximumDamage, item.MinimumArmorClass, item.Durability, item.Durability, 1, 1, false) {
            StableSymbol = "native" + item.NativeItemIndex, NativeItemIndex = item.NativeItemIndex,
            Name = item.Name, IdentifiedName = item.Name, NativeCursorGraphic = item.CursorGraphic,
            MinArmorClass = item.MinimumArmorClass, MaxArmorClass = item.MaximumArmorClass,
            EquipLocation = item.EquipLocation, ItemClass = item.ItemClass, MiscId = item.MiscId >= 24 ? item.MiscId + 1 : item.MiscId,
            ItemActionKind = item.MiscId == 24 ? DiabloItemActionKind.ReadBook : DiabloItemActionKind.None,
            SpellId = item.SpellId, Flags = item.Flags, Usable = item.Usable,
            MinimumStrength = item.MinimumStrength, MinimumMagic = item.MinimumMagic, MinimumDexterity = item.MinimumDexterity,
            SpellSourceKind = item.MiscId is 21 or 22 && item.SpellId > 0
                ? DiabloItemSpellSourceKind.ConsumableItem : DiabloItemSpellSourceKind.None,
        }));
        var gold = Items.Definitions.Single(item => item.ItemType == 11) with { GenerationTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Gold" } };
        Items = Items.WithNativePopulationOverlay(new Dictionary<uint, AuthoritativeItemDefinition> { [gold.ItemId] = gold }, new(
            gold.NativeItemIndex, gold.ItemId, gold.ItemType, gold.EquipLocation, gold.ItemClass,
            gold.Flags, gold.MiscId, gold.SpellId, gold.Name, gold.IdentifiedName, 1, 1, 4, 5, 6));
        var production = productionItemBindings ? (depth <= 4 ? CathedralItemContent.Value : CatacombsItemContent.Value) : null;
        if (production is not null) Items = production.Items;
        IReadOnlyDictionary<int, AuthoritativeCathedralItemContentBinding> itemBindings = production is not null
            ? new Dictionary<int, AuthoritativeCathedralItemContentBinding>() : Items.Definitions.ToDictionary(item => item.NativeItemIndex, item =>
            new AuthoritativeCathedralItemContentBinding(item.NativeItemIndex, item.ItemId, item.StableSymbol,
                1, 1, item.SpellSourceKind, item.ItemActionKind));
        var objectType = nativeObjectType != 0 ? (uint)nativeObjectType : depth % 2 == 0 ? 60U : 59U;
        var initialFrame = nativeObjectType is 61 or 63 ? 4 : nativeObjectType == 62 ? 3 : nativeObjectType == 67 ? 8 : 1;
        if (!nativeObjectSelectable && nativeObjectType is 61 or 64) initialFrame += 2;
        var initial = new[] {
            new AuthoritativeWorldObject(0x09003000, 5, level, 55, 50, nativeObjectId: 5, hideWhenActivated: false, animationFrame: 1),
            new AuthoritativeWorldObject(0x09003001, 6, level, 56, 50, nativeObjectId: 6, hideWhenActivated: false, animationFrame: 1),
            new AuthoritativeWorldObject(ShrineEntityId, objectType, level, 50, 50, nativeObjectId: objectType, hideWhenActivated: false, isShrine: false, animationFrame: initialFrame, missilePassable: nativeObjectType is 61 or 64 or 67),
        };
        if (additionalNativeObjectType != 0) initial = [.. initial,
            new AuthoritativeWorldObject(AdditionalObjectEntityId, (uint)additionalNativeObjectType,
                level, 51, 50, nativeObjectId: (uint)additionalNativeObjectType,
                hideWhenActivated: false, animationFrame: 1),
        ];
        var slots = Enumerable.Range(0, 127).Select(slot => new AuthoritativeCathedralPopulationObjectState {
            Slot = slot, Type = -1, BookMessage = -1,
        }).ToArray();
        var refs = new int[Cells];
        var pieces = new ushort[Cells];
        var active = new List<int> { 2, 0, 1 };
        if (additionalNativeObjectType != 0) active.Add(3);
        foreach (var value in initial) {
            var slot = (int)(value.EntityId - 0x09003000);
            var itemObject = slot >= 2 && (slot == 2 ? nativeObjectType : additionalNativeObjectType) != 0
                && value.NativeObjectId is not (26 or 66 or 76 or 79 or 80 or 81 or 82 or 91);
            slots[slot] = slots[slot] with { Type = (int)value.NativeObjectId,
                Position = new(value.PositionX, value.PositionY), SelectionRegion = value.NativeObjectId is 26 or 91 || slot == 2 && !nativeObjectSelectable ? 0 : 3,
                SolidFlag = itemObject,
                AnimFrame = (uint)value.AnimationFrame, AnimLength = itemObject || value.NativeObjectId == 80 ? 0U : value.NativeObjectId == 82 ? 4U : 11U, MissilePassable = value.MissilePassable,
                RndSeed = slot >= 2 ? seed : (uint)(800 + slot), Var1 = slot >= 2 ? shrineType : chestItemCount,
            };
            refs[value.PositionY * 112 + value.PositionX] = slot + 1;
        }
        initial = active.Select(slot => initial[slot]).ToArray();
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, UniqueType = 255,
        }).ToArray();
        var records = new List<AuthoritativeCathedralMonsterRuntimeRecord>();
        for (var slot = 0; slot < 5; slot++) {
            var golem = slot < 4;
            var position = golem ? new AuthoritativeCathedralPopulationPoint(1, 0) : new(40, 40);
            var raw = monsters[slot] = monsters[slot] with {
                Type = golem ? AuthoritativeCathedralPopulationParameters.GolemNativeType : 0,
                Ai = golem ? 21 : 0, Goal = 1, Tile = position, Future = position, Old = position,
                HitPoints = 64, MaxHitPoints = 64, Leader = 255,
            };
            var binding = new AuthoritativeCathedralMonsterRuntimeBinding(slot, CorpseId - 4 + (uint)slot, 1, level);
            var initialization = new AuthoritativeNativeMonsterInitialization(Fingerprint, raw, 0, 3, 1, 1,
                new(slot, raw.Type, 255, binding.EntityId, 1, level, ""), false) {
                    CatacombsDepth = depth >= 5 ? depth : 0,
                };
            AuthoritativeCombatTarget? target = null;
            if (!golem) {
                target = new(binding.EntityId, 40, 40, 1, maxHitPoints: 1, levelId: level, monsterId: 1, nativeAi: "Zombie");
                target.RestoreNativeInitialization(initialization, 0, 0);
            }
            records.Add(new(binding, raw, initialization, target, golem
                ? AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem
                : AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None));
        }
        var metadata = Cathedral.Value;
        var transparency = new sbyte[Cells];
        if (depth <= 4) {
            var coarse = AuthoritativeCathedralDepthGenerator.Generate(depth, 2588, AuthoritativeCathedralEntry.Main,
                AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs);
            var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(level, coarse,
                metadata.Geometry.Pieces, metadata.Geometry.TileProperties, "restore-fixture").Level;
            transparency = materialized.NativeTransparency.Select(value => (sbyte)value).ToArray();
        }
        var lights = Enumerable.Range(0, 32).Select(slot => new AuthoritativeCathedralEncounterLightState(
            slot, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        var state = new AuthoritativeCathedralThemeRoomState(slots,
            active.Concat(Enumerable.Range(0, 127).Except(active)).ToArray(),
            Enumerable.Range(0, 127).Except(active).Concat(active).ToArray(), active.Count,
            monsters, Enumerable.Range(0, 200).ToArray(), 5, 5, new int[Cells], new int[Cells],
            Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray(),
            Enumerable.Range(0, 127).ToArray(), 0, new int[Cells], new HashSet<int>(),
            pieces, transparency, new byte[Cells], new byte[1379], new byte[Cells], refs,
            new byte[Cells], new byte[Cells], Enumerable.Range(0, 32).ToArray(), 0, lights, false, false);
        if (production is not null)
            itemBindings = AuthoritativeCathedralNativePopulationBuilder.BuildItemBindings(nativeItems, Items, Spells,
                state, [(sbyte)(2 * depth)], [], [], production.Sidecars, includeUniqueLoot: true,
                includeCompleteAllowlist: true, depth <= 4 ? AuthoritativeNativeDungeonFamilyContext.Cathedral(depth)
                    : AuthoritativeNativeDungeonFamilyContext.Catacombs(depth));
        actors = new(Fingerprint, state.MonsterSlots, state.ActiveMonsterIds, 5, state.MonsterPetrified,
            records, (_, _, _, _) => throw new InvalidOperationException("No skeleton reservations in this fixture."), []);
        var bindings = active.Select(slot => {
            var raw = slots[slot];
            var value = initial.Single(value => value.PositionX == raw.Position.X && value.PositionY == raw.Position.Y);
            return new AuthoritativeCathedralItemObjectBinding(slot, value.EntityId, raw.Type,
                raw.Position.X, raw.Position.Y, raw.SolidFlag, raw.SelectionRegion, MutationPolicy: raw.DoorFlag
                    ? depth <= 4 ? AuthoritativeCathedralItemObjectMutationPolicy.CathedralDoor
                        : AuthoritativeCathedralItemObjectMutationPolicy.CatacombsDoor
                    : raw.Type is 5 or 6 or 7 or 68 or 69 or 70
                    ? AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                    : AuthoritativeCathedralItemObjectMutationPolicy.ExplicitlyRefused);
        }).ToArray();
        var checkpoint = new DiabloCathedralItemCheckpoint(Fingerprint, Fingerprint, [], [new(
            level, depth, state.ItemSlots, state.ActiveItemIds, 0, state.ItemRefs,
            Enumerable.Range(0, 128).Select(slot => new DiabloCathedralItemSlotEntityBinding(slot, 0)).ToArray())]);
        AuthoritativeCathedralItemRuntime.ValidateCheckpointShape(checkpoint,
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [level] = depth <= 4 ? AuthoritativeNativeDungeonFamilyContext.Cathedral(depth)
                    : AuthoritativeNativeDungeonFamilyContext.Catacombs(depth),
            });
        items = new(Fingerprint, Fingerprint, checkpoint, nativeItems, new(depth, GameMode: mode),
            MonsterCatalog.Value,
            metadata.UniqueMonsters.Definitions, itemBindings, new(),
            [new(level, depth, new bool[Cells], new bool[Cells], refs, bindings)],
            NativeSpellBindings: Spells.Definitions.Where(spell => spell.NativeSpellId != 0)
                .ToDictionary(spell => (int)spell.NativeSpellId, spell => (int)spell.SpellId)
                .Append(new KeyValuePair<int, int>(0, 0)).ToDictionary(pair => pair.Key, pair => pair.Value));
        if (depth <= 4) {
            var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata, depth, 2588, 0,
                AuthoritativeCathedralEntry.Main, new(6400, 0, 5, 8, new(56, 56), true), "restore-fixture");
            owner = AuthoritativeCathedralNativePopulationPolicy.CreateLevelEntry(request, state, actors, initial, items).Bindings;
        } else {
            var backing = new AuthoritativeLevel(level, 112, 112, nativePieceLayer: state.Pieces,
                nativeSpecialLayer: new ushort[Cells], nativePieceProperties: Enumerable.Range(0, 1379).ToDictionary(value => (ushort)value, _ => AuthoritativeDunTileFlags.None));
            owner = new(AuthoritativeNativePopulationFamily.Catacombs, depth, state, actors, initial, backing,
                Catacombs.Value.Population.Monsters.Select(value => value.PopulationDefinition).ToArray(), items,
                catacombsDoorCatalogFingerprint: AuthoritativeCatacombsDoorCatalog.Load(Catacombs.Value).Fingerprint);
            owner.BindGenerationFingerprint(Fingerprint);
        }
        items = items with { GenerationFingerprint = owner.Fingerprint,
            InitialCheckpoint = checkpoint with { GenerationFingerprint = owner.Fingerprint } };
        var worldGeometry = new AuthoritativeWorld(); worldGeometry.AddLevel(owner.CreateBackingLevel());
        Simulation = DiabloSimulationFactory.Instance.Create(new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = worldGeometry, Objects = initial, Items = Items, Spells = Spells, SpellBooks = SpellBooks, PlayerClasses = Classes,
            CombatRules = new AuthoritativeCombatRules(10, 1, 100, pvpEarItemId: 24),
            CathedralDoors = depth <= 4 ? new AuthoritativeCathedralDoorCatalog([new(level, 1, 2)]) : null,
            GameMode = mode, RandomSeed = 1234, Corpses = [new(CorpseId, level, 40, 40)],
        });
        World = Simulation.WorldState;
        typeof(DiabloWorldState).GetField("nativePopulationParticipantClassId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(World, (uint?)0);
        typeof(DiabloWorldState).GetField("nativePopulationMultiplayer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(World, mode == DiabloGameMode.Multiplayer);
        if (depth >= 5) {
            PopulationField.SetValue(World, owner);
            World.ConfigureNativeCatacombsDoors(owner, AuthoritativeCatacombsDoorCatalog.Load(Catacombs.Value), []);
            World.ConfigureNativeCatacombsMonsterPhases(new(depth, level, actors, []), []);
            World.ConfigureNativeCatacombsRangedActions(owner, new AuthoritativeMonsterMissileCatalog([new("Bat", "Lightning", AuthoritativeProjectileBehavior.MonsterMagic, MonsterId: 43, SpellId: 3, DamageType: AuthoritativeDamageType.Lightning, IsPostMeleeMissile: true)]), []);
            World.RestoreNativeCatacombsArrows(owner, []);
        }
        World.ConfigureNativePopulationBindings(owner, items, Fingerprint, 8, [], [], []);
        if (restoredItems is not null) {
            var raw = new AuthoritativeCathedralItemRuntime(items, restoredItems, null,
                new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> { [level] = owner.ItemFamilyContext });
            foreach (var binding in restoredItems.Levels.Single().SlotEntityBindings.Where(value => value.EntityId != 0)) {
                var projection = raw.GetProjection(level, binding.EntityId)!;
                World.WorldItems.Add(binding.EntityId, new(binding.EntityId, level, projection.Raw.PositionX,
                    projection.Raw.PositionY, projection.ItemSeed, projection.Price, projection.State));
            }
        }
        if (depth <= 4) World.ConfigureCathedralItems(items, restoredItems, Fingerprint, Items, Spells);
        else World.ConfigureNativeFamilyItems(items, restoredItems, [owner], Items, Spells);
        Random = new DeterministicLegacyRandom(1234);
        Rules = new DiabloWorldCommandProcessor(World, items: Items, spells: Spells, spellBooks: SpellBooks, random: Random, gameMode: mode);
        var missiles = new DiabloCombatCommandProcessor(World, Spells, new AuthoritativeCombatRules(10, 1, 100), Random);
        Rules.SetNativeShrineMissileHandler(missiles.AddNativeShrineMissile);
        foreach (var chest in World.Objects.Values.Where(value => value.NativeObjectId is 5 or 6))
            Assert.True(owner.TryCommitNativeChestOpen(chest));
    }

    private sealed record ProductionItemContent(AuthoritativeItemCatalog Items, AuthoritativeCathedralNativeItemSidecars Sidecars);
    private static readonly Lazy<ProductionItemContent> CathedralItemContent = new(() => CreateProductionItemContent(false));
    private static readonly Lazy<ProductionItemContent> CatacombsItemContent = new(() => CreateProductionItemContent(true));

    private static ProductionItemContent CreateProductionItemContent(bool catacombs)
    {
        var pack = ContentPackLoader.LoadManifestDirectory(Path.Combine(RepositoryRoot(), "server/content/base"));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(ComposedContent.Compose(new(pack.Id, pack.Version, [pack])));
        var native = Cathedral.Value.Items.Catalog;
        var content = activation.Simulation.Items!;
        // CI has no retail sprites. Use the shipped inventory dimensions, with the production
        // sidecar compiler and exact native identity matcher (never synthesize stable bindings).
        var text = new System.Text.StringBuilder("native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height\n");
        foreach (var group in content.Definitions.Where(item => item.NativeItemIndex >= 0).GroupBy(item => item.NativeItemIndex)) {
            var raw = native.Items.SingleOrDefault(item => item.NativeItemIndex == group.Key);
            if (raw is null) continue;
            var spellId = raw.SpellId == 0 || raw.MiscId == 21 && raw.SpellId == 5 ? 0 : activation.Simulation.Spells!.TryGetByNativeId((uint)raw.SpellId, out var spell)
                ? (int)spell.SpellId : -1;
            if (spellId < 0) continue;
            var matches = group.Where(item => AuthoritativeCathedralNativePopulationBuilder.MatchesNativeItemDefinition(raw, item, spellId, includeNativeDepthItems: true)
                || catacombs && AuthoritativeCatacombsNativeItemBindings.MatchesElixir(raw, item, spellId)).ToArray();
            if (matches.Length == 0) continue;
            var size = matches.Select(item => (item.InventoryWidth, item.InventoryHeight)).Distinct().Single();
            text.Append($"{group.Key}\t{raw.CursorGraphic + 12}\t{size.InventoryWidth * 28}\t{size.InventoryHeight * 28}\t{size.InventoryWidth}\t{size.InventoryHeight}\n");
        }
        var table = TsvTable.Parse("native-rogue-l1-inventory.tsv", text.ToString());
        var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(table);
        var semantics = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(native, content,
            activation.Simulation.Spells!, footprints, includeNativeJewelry: true, includeNativeDepthItems: true);
        var sidecars = AuthoritativeCathedralNativeItemSidecars.Load(table, semantics);
        var effective = AuthoritativeCathedralNativePopulationBuilder.ApplyItemSidecars(native, content, activation.Simulation.Spells!, sidecars);
        if (catacombs) sidecars = AuthoritativeCathedralNativeItemSidecars.Load(table,
            AuthoritativeCatacombsNativeItemBindings.AppendElixirSemantics(semantics, native, effective, footprints));
        return new(effective, sidecars);
    }

    private static IReadOnlyList<TsvTable> OpenTables() => Directory.GetFiles(
        Path.Combine(RepositoryRoot(), "test", "fixtures", "native-open-metadata"), "*.tsv")
        .Select(path => TsvTable.Parse(Path.GetFileName(path), File.ReadAllText(path))).ToArray();

    private static AuthoritativeNativeDunGeometryCatalog Geometry(int count) => new(
        new AuthoritativeDunPieceCatalog(Enumerable.Range(0, count).Select(value => new AuthoritativeDunPieceDefinition((uint)value, 0, 1, 2, 3))),
        new AuthoritativeDunTilePropertyCatalog(Enumerable.Range(0, 1379).Select(value => new AuthoritativeDunTileProperty((uint)value, AuthoritativeDunTileFlags.None))));

    private static AuthoritativeCathedralPopulationMetadata CreateCathedralMetadata()
    {
        var tables = OpenTables().Where(value => value.SourcePath != AuthoritativeCathedralMonsterRuntimeCatalog.TableName).ToList();
        tables.Add(TsvTable.Parse(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
            + string.Concat(Enumerable.Range(0, 206).Select(value => $"{value}\t0\t1\t2\t3\n"))));
        tables.Add(TsvTable.Parse(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
            + string.Concat(Enumerable.Range(0, 1379).Select(value => $"{value}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse\n"))));
        tables.Add(TsvTable.Parse(AuthoritativeCathedralPopulationMetadata.EffectiveSolTableName,
            "piece_id\teffective_flags\n" + string.Concat(Enumerable.Range(0, 1379).Select(value => $"{value}\t0\n"))));
        return AuthoritativeCathedralPopulationMetadata.Load(new("synthetic-projectile-restore", "1.0.0", tables,
            schema: new(AuthoritativeCathedralPopulationMetadata.SchemaId, AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
    }

    private static AuthoritativeCatacombsPopulationMetadata CreateCatacombsMetadata()
    {
        var source = Cathedral.Value;
        var geometry = Geometry(AuthoritativeCatacombsPopulationMetadata.NativeTilRecordCount);
        var til = new byte[AuthoritativeCatacombsPopulationMetadata.NativeTilRecordCount * 8];
        for (var index = 0; index < til.Length / 2; index++) BinaryPrimitives.WriteUInt16LittleEndian(til.AsSpan(index * 2), (ushort)(index % 4));
        var text = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "assets", "txtdata", "text", "textdat.tsv"));
        var rows = AuthoritativeCatacombsPopulationMetadata.CreateTables(geometry, new byte[1379],
            Convert.ToHexString(SHA256.HashData(til)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(new byte[AuthoritativeCatacombsPopulationMetadata.NativeSolSourceByteCount])).ToLowerInvariant(),
            source.Population.Monsters, source.Population.Objects, source.UniqueMonsters.Definitions, source.Items.Catalog,
            Enumerable.Range(5, 4).Select(depth => new AuthoritativeCatacombsPopulationDepthRule(depth, 30, 150, depth < 7 ? 20 : 25, 190)).ToArray(),
            [250, 253, 256], text.Length, Convert.ToHexString(SHA256.HashData(text)).ToLowerInvariant(), source.ShrineAvailable);
        return AuthoritativeCatacombsPopulationMetadata.Load(new(AuthoritativeCatacombsPopulationMetadata.CatalogId, "1.0.0", rows,
            schema: new(AuthoritativeCatacombsPopulationMetadata.SchemaId, AuthoritativeCatacombsPopulationMetadata.SchemaVersion)));
    }

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "assets", "txtdata"))) directory = directory.Parent;
        return directory!.FullName;
    }
}

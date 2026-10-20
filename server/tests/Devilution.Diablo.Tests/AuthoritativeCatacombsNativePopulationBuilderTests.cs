using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Qualified C2 owner construction tests share the existing native profile fixture. */
public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
	internal static QualifiedCatacombsBuilderFixture CreateFixture(uint classId = 0)
	{
		var prepared = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
		Assert.NotNull(prepared);
		try {
			return new QualifiedCatacombsBuilderFixture(prepared!, prepared!.Fixture.Content,
				prepared.Fixture.PopulationMetadata, prepared.Fixture.BaselineItems, classId);
		} catch {
			prepared!.Dispose();
			throw;
		}
	}

	[Fact(Explicit = true)]
	public void CatacombsAllQualifiedFreshReadyCasesBuildAndAdmitACompleteNativeOwner()
	{
		using var warriorFixture = CreateFixture(0);
		using var sorcererFixture = CreateFixture(2);
		var rows = AuthoritativeCatacombsInitialPopulationNativeOracleTests.Cases
			.Select(row => new {
				File = row.Data.Item1,
				CharacterClass = row.Data.Item2,
				ClassId = row.Data.Item3,
				Depth = row.Data.Item4,
				Entry = row.Data.Item5,
				Seed = row.Data.Item6,
				Retained = row.Data.Item7,
			})
			.ToArray();
		Assert.Equal(32, rows.Length);
		var shrineVariantInputs = 0;
		var shrineVariantProjections = 0;
		var unavailableArmorStandInputs = 0;
		var unavailableArmorStandProjections = 0;
		var unavailableWeaponRackInputs = 0;
		var unavailableWeaponRackProjections = 0;
		var armedDoorsObserved = 0;

		foreach (var row in rows) {
			Assert.Equal(row.ClassId == 0 ? "warrior" : "sorcerer", row.CharacterClass);
			var fixture = row.ClassId == 0 ? warriorFixture : sorcererFixture;
			var history = row.Retained
				? fixture.CreateRetainedHistory(row.Depth, row.Seed)
				: new HashSet<int>();
			var build = fixture.CreateBuild(row.Depth, checked((uint)row.Depth), history,
				dungeonSeed: row.Seed, entry: ToEntry(row.Entry));
			Assert.Equal(row.Depth, build.Policy.Depth);
			Assert.Equal((uint)row.Depth, build.Policy.LevelId);
			Assert.Equal((uint)row.ClassId, build.Policy.CharacterClassId);
			foreach (var (nativeIndex, stableSymbol, stableMiscId, action) in new[] {
				(87, "elixir_of_strength_standard", 11, DiabloItemActionKind.IncreaseStrength),
				(88, "elixir_of_magic_standard", 12, DiabloItemActionKind.IncreaseMagic),
				(89, "elixir_of_dexterity_standard", 13, DiabloItemActionKind.IncreaseDexterity),
				(90, "elixir_of_vitality_standard", 14, DiabloItemActionKind.IncreaseVitality),
			}) {
				var semantic = fixture.ItemSidecars.Semantics[nativeIndex];
				var binding = build.Items.ItemBindings[nativeIndex];
				Assert.Equal(stableSymbol, semantic.StableItemSymbol);
				Assert.Equal(stableMiscId, semantic.MiscId);
				Assert.Equal(action, semantic.ItemActionKind);
				Assert.Equal(stableSymbol, binding.StableSymbol);
				Assert.Equal(action, binding.ItemActionKind);
			}
			Assert.Equal(build.DoorCatalog.Fingerprint, build.Policy.DoorCatalogFingerprint);
			Assert.Equal(build.Policy.DoorCatalogFingerprint,
				build.Policy.Bindings.CatacombsDoorCatalogFingerprint);
			Assert.Equal(build.Ready.State.ActiveMonsterCount, build.Monsters.SharedRuntime.ActiveMonsterCount);
			Assert.Equal(build.Ready.State.ActiveObjectCount, build.Objects.Count);
			Assert.Equal(4, build.Monsters.SharedRuntime.Records.Count(row => row.Binding.NativeSlot is >= 0 and <= 3));
			Assert.Equal(build.Ready.State.ActiveItemCount,
				build.Items.InitialCheckpoint.Levels.Single().ActiveItemCount);
			Assert.All(build.Objects, value => Assert.Null(value.CathedralDoor));
			Assert.Equal(build.Policy.InitialDoorCount,
				build.Objects.Count(value => value.NativeObjectId is 42 or 43));

			var floor = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells);
			Assert.True(floor.OwnsNativePopulationLevel(checked((uint)row.Depth)));
			floor.ValidateNativePopulationState([]);
			Assert.Equal(build.Policy.InitialDoorCount,
				floor.CaptureNativeCatacombsDoorStates().Single().Doors.Count);
			var objectBindings = build.Items.LevelProfiles.Single().ObjectBindings;
			for (var index = 0; index < build.Ready.State.ActiveObjectCount; index++) {
				var slot = build.Ready.State.ActiveObjectIds[index];
				var raw = build.Ready.State.ObjectSlots[slot];
				var projected = build.Objects[index];
				var binding = objectBindings.Single(value => value.NativeObjectSlot == slot);
				Assert.Equal(raw.Type, checked((int)projected.NativeObjectId));
				Assert.Equal(checked((int)raw.AnimFrame), projected.AnimationFrame);
				Assert.Equal(raw.Position.X, projected.PositionX);
				Assert.Equal(raw.Position.Y, projected.PositionY);
				Assert.Equal(raw.TrapFlag, projected.TrapArmed);
				Assert.Equal(raw.MissilePassable, projected.MissilePassable);
				Assert.Equal(raw.SelectionRegion, binding.InitialSelectionRegion);
				var expectedRawFlags = (raw.AnimFlag != 0 ? 1 : 0)
					| (raw.SolidFlag ? 2 : 0)
					| (raw.MissilePassable ? 4 : 0)
					| (raw.ApplyLighting ? 8 : 0)
					| (raw.BreakFlag != 0 ? 32 : 0);
				Assert.Equal(expectedRawFlags, binding.RawFlags);
				if (raw.Type is 59 or 60 && raw.AnimLength == 22 && raw.AnimFrame == 12) {
					shrineVariantInputs++;
					Assert.Equal(12, projected.AnimationFrame);
					shrineVariantProjections++;
				}
				if (raw.Type == 77 && raw.AnimFlag != 0 && raw.SelectionRegion == 0) {
					unavailableArmorStandInputs++;
					Assert.Equal(0, binding.InitialSelectionRegion);
					Assert.NotEqual(0, binding.RawFlags & 1);
					unavailableArmorStandProjections++;
				}
				if (raw.Type == 92 && raw.AnimFlag != 0 && raw.SelectionRegion == 0) {
					unavailableWeaponRackInputs++;
					Assert.Equal(0, binding.InitialSelectionRegion);
					Assert.NotEqual(0, binding.RawFlags & 1);
					unavailableWeaponRackProjections++;
				}
			}
			Assert.Equal(shrineVariantInputs, shrineVariantProjections);
			Assert.Equal(unavailableArmorStandInputs, unavailableArmorStandProjections);
			Assert.Equal(unavailableWeaponRackInputs, unavailableWeaponRackProjections);
			Assert.All(floor.Objects.Values.Where(value => value.NativeObjectId is 5 or 6 or 7 or 68 or 69 or 70), value => {
				Assert.False(floor.CanOperateNativeChest(value));
				Assert.False(floor.CanOperateNativeObject(value));
			});
			foreach (var armedDoor in floor.Objects.Values.Where(value => value.NativeObjectId is 42 or 43 && value.TrapArmed)) {
				armedDoorsObserved++;
				var before = floor.CaptureNativeCatacombsDoorStates().Single().Doors
					.Single(value => value.EntityId == armedDoor.EntityId);
				Assert.False(floor.CanOperateNativeObject(armedDoor));
				Assert.False(floor.TryOperateNativeCatacombsDoor(armedDoor.EntityId, []));
				Assert.Equal(before, floor.CaptureNativeCatacombsDoorStates().Single().Doors
					.Single(value => value.EntityId == armedDoor.EntityId));
				Assert.True(floor.Objects[armedDoor.EntityId].TrapArmed);
			}
		}
		Assert.True(armedDoorsObserved > 0, "The qualified native case matrix must cover an armed Catacombs door.");
		Assert.Equal(shrineVariantInputs, shrineVariantProjections);
		Assert.Equal(unavailableArmorStandInputs, unavailableArmorStandProjections);
		Assert.Equal(unavailableWeaponRackInputs, unavailableWeaponRackProjections);
	}

	[Fact(Explicit = true)]
	public void CatacombsElixirSemanticsAreExactAndDoNotWidenTheCathedralTable()
	{
		using var fixture = CreateFixture(0);
		var legacyTable = fixture.CathedralSemanticTable;
		var originalLegacyText = legacyTable.ToCanonicalTsv();
		var nativeCatalog = fixture.Metadata.Items.Catalog;
		var expected = new[] {
			(87, 2190U, "elixir_of_strength_standard", 11, DiabloItemActionKind.IncreaseStrength),
			(88, 2191U, "elixir_of_magic_standard", 12, DiabloItemActionKind.IncreaseMagic),
			(89, 2192U, "elixir_of_dexterity_standard", 13, DiabloItemActionKind.IncreaseDexterity),
			(90, 2193U, "elixir_of_vitality_standard", 14, DiabloItemActionKind.IncreaseVitality),
		};
		Assert.DoesNotContain(legacyTable.Rows, row => expected.Any(value =>
			row.RequiredInt32("native_item_index") == value.Item1));

		foreach (var (nativeIndex, stableId, stableSymbol, stableMiscId, action) in expected) {
			var native = nativeCatalog.Items.Single(value => value.NativeItemIndex == nativeIndex);
			var stable = fixture.SemanticItems.GetRequired(stableId);
			Assert.True(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(native, stable, 0));
			Assert.False(AuthoritativeCathedralNativePopulationBuilder.MatchesNativeItemDefinition(
				native, stable, 0, includeNativeDepthItems: true));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native, stable with { ItemId = stableId + 1 }, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native, stable with { NativeItemIndex = nativeIndex + 1 }, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native, stable with { StableSymbol = stableSymbol + "_wrong" }, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native, stable with { MiscId = stableMiscId - 1 }, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native, stable with { ItemActionKind = action == DiabloItemActionKind.IncreaseStrength
					? DiabloItemActionKind.IncreaseMagic : DiabloItemActionKind.IncreaseStrength }, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native with { MiscId = native.MiscId + 1 }, stable, 0));
			Assert.False(AuthoritativeCatacombsNativeItemBindings.MatchesElixir(
				native with { Value = native.Value + 1 }, stable, 0));
			var semantic = fixture.ItemSidecars.Semantics[nativeIndex];
			Assert.Equal(stableSymbol, semantic.StableItemSymbol);
			Assert.Equal(stableMiscId, semantic.MiscId);
			Assert.Equal(action, semantic.ItemActionKind);
		}

		Assert.Equal(originalLegacyText, legacyTable.ToCanonicalTsv());
		Assert.Equal(4, fixture.CatacombsSemanticTable.Rows.Count
			- legacyTable.Rows.Count);
		Assert.Equal(fixture.CatacombsSemanticTable.ToCanonicalTsv(),
			AuthoritativeCatacombsNativeItemBindings.AppendElixirSemantics(legacyTable,
				nativeCatalog, fixture.SemanticItems, fixture.ItemSidecars.Footprints).ToCanonicalTsv());
		Assert.Equal(originalLegacyText, legacyTable.ToCanonicalTsv());
		Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsNativeItemBindings.AppendElixirSemantics(
			fixture.CatacombsSemanticTable, nativeCatalog, fixture.SemanticItems, fixture.ItemSidecars.Footprints));
	}

	[Fact(Explicit = true)]
	public void CatacombsBuilderRejectsClassTimingAndLevelIdentityMismatches()
	{
		using var fixture = CreateFixture(0);
		var defaultBuild = fixture.CreateBuild(5, 5);
		var ready = defaultBuild.Ready;
		var request = ready.Request.FreshLevel.Request;
		var wrongIdle = request.Hero with { IdleFrames = request.Hero.IdleFrames + 1 };
		var wrongFresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(
			fixture.Metadata, 5, 2588, QualifiedCatacombsBuilderFixture.StorySeed,
			AuthoritativeCatacombsEntry.Main, wrongIdle,
			stableLevelSymbol: request.StableLevelSymbol,
			initialUniqueItemFlags: request.InitialUniqueItemFlags);
		var wrongReady = AuthoritativeCatacombsLevelReadyGenerator.Generate(new(
			wrongFresh, ready.Request.Player, ready.Request.Automap, portalsAbsent: true));
		Assert.Throws<InvalidDataException>(() => fixture.BuildFromReady(wrongReady, 5));
		Assert.Throws<ArgumentOutOfRangeException>(() => fixture.BuildFromReady(ready, 0));

		var rogueStart = fixture.CreateCharacterStart(1);
		Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsNativePopulationBuilder.Build(
			ready, 5, rogueStart, fixture.RuntimeCatalog, fixture.Activation.PlayerClasses,
			fixture.Activation.TrustedOptions!.MonsterTypes!, fixture.Items, fixture.Spells,
			fixture.CarriedItems, fixture.StoreItems, fixture.ItemSidecars));
	}

	private static AuthoritativeCatacombsEntry ToEntry(string value)
		=> value switch {
			"main" => AuthoritativeCatacombsEntry.Main,
			"previous" => AuthoritativeCatacombsEntry.Previous,
			"town-warp-down" => AuthoritativeCatacombsEntry.TownWarpDown,
			_ => throw new InvalidDataException("Unsupported native Catacombs test entry."),
		};

	internal sealed class QualifiedCatacombsBuilderFixture : IDisposable
	{
		internal const uint StorySeed = 0x1357_9BDF;
		private readonly IDisposable cleanup;
		private readonly ComposedContent content;
		private readonly AuthoritativeCathedralPopulationMetadata baselineMetadata;
		private readonly AuthoritativeItemCatalog baselineItems;
		private readonly AuthoritativeCathedralNativeCharacterStart characterStart;
		private readonly AuthoritativeCatacombsAutomapMetadata automap;

		internal QualifiedCatacombsBuilderFixture(
			IDisposable cleanup,
			ComposedContent content,
			AuthoritativeCathedralPopulationMetadata baselineMetadata,
			AuthoritativeItemCatalog baselineItems,
			uint classId)
		{
			this.cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
			this.content = content ?? throw new ArgumentNullException(nameof(content));
			this.baselineMetadata = baselineMetadata ?? throw new ArgumentNullException(nameof(baselineMetadata));
			this.baselineItems = baselineItems ?? throw new ArgumentNullException(nameof(baselineItems));
			var baseline = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
				.ActivateNativeDepthProgression(content, StorySeed);
			var footprintSource = content.GetRequiredTable(
				"native_cathedral_runtime/native-rogue-l1-inventory.tsv");
			var footprintTable = TsvTable.Parse("native-rogue-l1-inventory.tsv", footprintSource.ToCanonicalTsv());
			var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(footprintTable);
			var semanticTable = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
				baselineMetadata.Items.Catalog,
				baselineItems, baseline.Simulation.Spells!, footprints,
				includeNativeJewelry: true, includeNativeDepthItems: true);
			CathedralSemanticTable = semanticTable;
			var catacombsSemanticTable = AuthoritativeCatacombsNativeItemBindings.AppendElixirSemantics(
				semanticTable, baselineMetadata.Items.Catalog, baselineItems, footprints);
			CatacombsSemanticTable = catacombsSemanticTable;
			var depthContent = ReplaceTable(content,
				"native_cathedral_runtime/native_item_semantics.tsv",
				semanticTable with { SourcePath = "native_cathedral_runtime/native_item_semantics.tsv" });
			var classContent = AddNativeCharacterStartTable(baseline, depthContent, classId);
			Activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
				.ActivateNativeDepthProgression(classContent, StorySeed);
			characterStart = Activation.NativeCharacterStart
				?? throw new InvalidDataException("The qualified C2 fixture must retain its explicit class start.");
			Metadata = AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs().Metadata;
			var runtimePath = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH")
				?? throw new InvalidOperationException("The qualified C2 fixture requires the native monster runtime table.");
			RuntimeCatalog = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
				AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(runtimePath)));
			ItemSidecars = AuthoritativeCathedralNativeItemSidecars.Load(footprintTable, catacombsSemanticTable);
			var template = Activation.PlayerLoadouts.CreateTemplate(
				"catacombs-native-owner-fixture", classId, null, null, null,
				startingLevelId: 1, startingPositionX: 75, startingPositionY: 68);
			CarriedItems = template.Inventory.Select(value => value.State)
				.Concat(template.Equipment.Select(value => value.State))
				.Concat(template.Belt.Select(value => value.State)).ToArray();
			StoreItems = Activation.TrustedOptions!.StoreCatalog.CaptureAll()
				.SelectMany(value => value.Items!).Select(value => value.State).ToArray();
			Items = Activation.Simulation.Items!;
			Spells = Activation.Simulation.Spells!;
			automap = LoadAutomap();
		}

		internal DiabloContentActivation Activation { get; }
		internal AuthoritativeItemCatalog Items { get; }
		internal AuthoritativeSpellCatalog Spells { get; }
		internal AuthoritativeCatacombsPopulationMetadata Metadata { get; }
		internal TsvTable CathedralSemanticTable { get; }
		internal TsvTable CatacombsSemanticTable { get; }
		internal AuthoritativeItemCatalog SemanticItems => baselineItems;
		internal AuthoritativeCathedralMonsterRuntimeCatalog RuntimeCatalog { get; }
		internal AuthoritativeCathedralNativeItemSidecars ItemSidecars { get; }
		internal IReadOnlyCollection<AuthoritativeItemState> CarriedItems { get; }
		internal IReadOnlyCollection<AuthoritativeItemState> StoreItems { get; }

		internal AuthoritativeCathedralNativeCharacterStart CreateCharacterStart(uint classId)
		{
			var temporary = AddNativeCharacterStartTable(Activation, content, classId);
			var table = temporary.GetRequiredTable(
				"native_cathedral_runtime/" + AuthoritativeCathedralNativeCharacterStart.TableName);
			return AuthoritativeCathedralNativeCharacterStart.Resolve(
				table with { SourcePath = AuthoritativeCathedralNativeCharacterStart.TableName },
				Activation.PlayerClasses, Activation.PlayerAnimations, Activation.PlayerLoadouts);
		}

		internal AuthoritativeCatacombsNativePopulationBuild CreateBuild(
			int depth,
			uint stableLevelId,
			IReadOnlySet<int>? retainedUniqueHistory = null,
			AuthoritativeCatacombsFreshLevelHeroContext? heroOverride = null,
			uint dungeonSeed = 2588,
			AuthoritativeCatacombsEntry? entry = null,
			uint? retainedLevelSeed = null)
		{
			var hero = heroOverride ?? CreateHeroContext(characterStart.Hero);
			var uniqueHistory = retainedUniqueHistory ?? new HashSet<int>();
			var selectedEntry = entry ?? (depth == 5
				? AuthoritativeCatacombsEntry.Main : AuthoritativeCatacombsEntry.Previous);
			var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(
				Metadata, depth, dungeonSeed, StorySeed, selectedEntry, hero,
				stableLevelSymbol: $"catacombs{depth}", retainedLevelSeed: retainedLevelSeed,
				initialUniqueItemFlags: uniqueHistory);
			var ready = AuthoritativeCatacombsLevelReadyGenerator.Generate(new(fresh,
				new(0, false, false, true, hero.MaximumLifeBase,
					new(13, 17), new(0, 0), true), automap, portalsAbsent: true));
			return BuildFromReady(ready, stableLevelId);
		}

		internal AuthoritativeCatacombsNativePopulationBuild BuildFromReady(
			AuthoritativeCatacombsLevelReadyResult ready, uint stableLevelId)
			=> AuthoritativeCatacombsNativePopulationBuilder.Build(
				ready, stableLevelId, characterStart, RuntimeCatalog,
				Activation.PlayerClasses, Activation.TrustedOptions!.MonsterTypes!, Items, Spells,
				CarriedItems, StoreItems, ItemSidecars);

		internal IReadOnlySet<int> CreateRetainedHistory(int depth, uint dungeonSeed)
		{
			var flags = new HashSet<int> { 7 };
			var hero = CreateHeroContext(characterStart.Hero);
			for (var priorDepth = 5; priorDepth < depth; priorDepth++) {
				var prior = AuthoritativeCatacombsFreshLevelGenerator.Generate(
					Metadata, priorDepth, dungeonSeed, StorySeed,
					AuthoritativeCatacombsEntry.Main, hero,
					initialUniqueItemFlags: flags);
				flags = prior.Population.State.UniqueItemFlags.ToHashSet();
			}
			return flags;
		}

		private static AuthoritativeCatacombsFreshLevelHeroContext CreateHeroContext(
			AuthoritativeCathedralFreshLevelHeroContext hero)
			=> new(hero.MaximumLifeBase, hero.MaximumManaBase, hero.LightRadius,
				hero.IdleFrames, new(75, 68), LevelChanging: true, LevelLoading: true);

		private static AuthoritativeCatacombsAutomapMetadata LoadAutomap()
		{
			using var source = AuthoritativeCatacombsInitialPopulationNativeOracleTests.OpenQualifiedSource();
			var words = source.RootElement.GetProperty("automapRawWordsLittleEndian")
				.EnumerateArray().Select(value => value.GetUInt16()).ToArray();
			var bytes = new byte[words.Length * sizeof(ushort)];
			for (var index = 0; index < words.Length; index++)
				BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort)), words[index]);
			var tables = AuthoritativeCatacombsAutomapMetadata.CreateTables(words,
				Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
			return AuthoritativeCatacombsAutomapMetadata.Load(new ContentPack(
				AuthoritativeCatacombsAutomapMetadata.CatalogId,
				AuthoritativeCatacombsAutomapMetadata.SchemaVersion, tables,
				schema: new ContentSchemaIdentity(AuthoritativeCatacombsAutomapMetadata.SchemaId,
					AuthoritativeCatacombsAutomapMetadata.SchemaVersion)));
		}

		public void Dispose() => cleanup.Dispose();
	}
}

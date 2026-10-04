using System.Security.Cryptography;
using System.Text;

using Devilution.Diablo;

using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralMonsterRuntimeAdapterTests
{
	[Fact]
	public void DeterministicAdapterBindsActiveOrderRestoresQ6AndRetainsDormantRows()
	{
		var state = CreateState();
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("population", "uniques", catalog.Fingerprint);

		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, entityIdBase: 1000, contentMonsterIdBase: 2000);

		Assert.Equal(fingerprint, result.MetadataFingerprint);
		Assert.Equal(state.ActiveMonsterIds, result.ActiveMonsterIds);
		Assert.Equal(2, result.Records.Count);
		Assert.Equal(0, result.Records[0].Binding.NativeSlot);
		Assert.Equal(1000u, result.Records[0].Binding.EntityId);
		Assert.Equal(2000u, result.Records[0].Binding.ContentMonsterId);
		var liveTarget = result.Records[0].Target ?? throw new InvalidOperationException("The ordinary native row must produce a target.");
		Assert.True(liveTarget.NativeInitializationPresent);
		Assert.True(liveTarget.UsesNativeRandomSeeds);
		Assert.Equal(6400, liveTarget.HitPointsQ6);
		Assert.Equal(6400, liveTarget.MaxHitPointsQ6);
		Assert.Equal(7u, liveTarget.MonsterItemSeed);
		Assert.Equal(8u, liveTarget.MonsterAiSeed);
		Assert.Equal(fingerprint, result.Records[0].Initialization.MetadataFingerprint);
		Assert.Equal(0xC007, result.Records[0].Initialization.Treasure);

		Assert.Equal(1, result.Records[1].Binding.NativeSlot);
		Assert.Equal(2109u, result.Records[1].Binding.ContentMonsterId);
		Assert.Null(result.Records[1].Target);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem,
			result.Records[1].UnsupportedReason);
		Assert.Equal(200, result.MonsterSlots.Count);
		Assert.Equal(200, result.MonsterPetrified.Count);
		Assert.Equal(state.MonsterPetrified, result.MonsterPetrified);
		Assert.NotSame(state.MonsterSlots, result.MonsterSlots);
	}

	[Fact]
	public void ExplicitBindingsFollowNativeActiveOrderAndRejectCollisions()
	{
		var state = CreateState(activeIds: [1, 0, ..Enumerable.Range(2, 198)]);
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("generation", "unique", catalog.Fingerprint);
		var bindings = new[] {
			new AuthoritativeCathedralMonsterRuntimeBinding(0, 900, 1900),
			new AuthoritativeCathedralMonsterRuntimeBinding(1, 901, 1901),
		};

		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			state, catalog, fingerprint, bindings);

		Assert.Equal(new[] { 1, 0 }, result.Records.Select(record => record.Binding.NativeSlot));
		Assert.Equal(901u, result.Records[0].Binding.EntityId);
		Assert.Equal(900u, result.Records[1].Binding.EntityId);

		var duplicate = new[] {
			new AuthoritativeCathedralMonsterRuntimeBinding(1, 900, 1900),
			new AuthoritativeCathedralMonsterRuntimeBinding(1, 901, 1901),
		};
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			CreateState(), catalog, fingerprint, duplicate));
	}

	[Fact]
	public void ContentIdentityMayRepeatForSameNativeTypeButNotAcrossTypes()
	{
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("generation", "unique", catalog.Fingerprint);
		var sameType = CreateState(activeIds: [0, 2, 1, ..Enumerable.Range(3, 197)]);
		var accepted = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			sameType, catalog, fingerprint,
			new[] {
				new AuthoritativeCathedralMonsterRuntimeBinding(0, 900, 1900),
				new AuthoritativeCathedralMonsterRuntimeBinding(2, 901, 1900),
			});
		Assert.Equal(1900u, accepted.Records[0].Binding.ContentMonsterId);
		Assert.Equal(1900u, accepted.Records[1].Binding.ContentMonsterId);

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			CreateState(), catalog, fingerprint,
			new[] {
				new AuthoritativeCathedralMonsterRuntimeBinding(0, 900, 1900),
				new AuthoritativeCathedralMonsterRuntimeBinding(1, 901, 1900),
			}));
	}

	[Theory]
	[InlineData(0, AuthoritativeCathedralMonsterRuntimeUnsupportedReason.EmptyHitPoints)]
	[InlineData(7, AuthoritativeCathedralMonsterRuntimeUnsupportedReason.NonStandMode)]
	public void UnsupportedNativeModesRemainRawAndAreNotInstalled(int mode,
		AuthoritativeCathedralMonsterRuntimeUnsupportedReason expected)
	{
		var state = CreateState(ordinaryMode: mode, ordinaryHitPoints: mode == 0 ? 0 : 6400);
		var catalog = CreateCatalog();
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20);

		Assert.Null(result.Records[0].Target);
		Assert.Equal(expected, result.Records[0].UnsupportedReason);
		Assert.Equal(mode, result.Records[0].InitialState.Mode);
	}

	[Fact]
	public void PreSpawnSkeletonHoldingCellIsRetainedButNotInstalled()
	{
		var state = CreateState(ordinaryType: 8, ordinaryTile: new(0, 0));
		var catalog = CreateCatalog();
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20);

		Assert.Null(result.Records[0].Target);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton,
			result.Records[0].UnsupportedReason);
		Assert.Equal(8, result.Records[0].InitialState.Type);
	}

	[Fact]
	public void CombinedMetadataFingerprintIsOrderedUppercaseAndValidatesInputs()
	{
		var allA = new string('A', 64);
		var allB = new string('B', 64);
		var first = AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(allA, allB, allA);
		var second = AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(allB, allA, allA);

		Assert.Equal(64, first.Length);
		Assert.All(first, character => Assert.True(character is >= '0' and <= '9' or >= 'A' and <= 'F'));
		Assert.NotEqual(first, second);
		Assert.Throws<ArgumentException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(
			allA.ToLowerInvariant(), allB, allA));
	}

	[Fact]
	public void DeterministicBasesRejectZeroAndOverflowWithoutPartialOutput()
	{
		var state = CreateState();
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("p", "u", catalog.Fingerprint);
		Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 0, 20));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, uint.MaxValue, 20));
	}

	[Fact]
	public void NormalAdapterMapsNativeAiClassResistanceFlagsAndLevelIdentity()
	{
		var state = CreateState(ordinaryType: 2, resistance: (1 << 0) | (1 << 3) | (1 << 1) | (1 << 7), flags: 1U << 7);
		var catalog = CreateCatalog();
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20, levelId: 42);

		var target = result.Records[0].Target!;
		Assert.Equal(42u, target.LevelId);
		Assert.Equal("Zombie", target.NativeAi);
		Assert.True(target.IsAnimal);
		Assert.False(target.IsUndead);
		Assert.Equal(75, target.FireResistance);
		Assert.Equal(0, target.LightningResistance);
		Assert.Equal(100, target.MagicResistance);
		Assert.Equal(100, target.AcidResistance);
		Assert.True(target.HasKnockback);
	}

	[Fact]
	public void AdapterRejectsNightmareAndInvalidLeaderReferences()
	{
		var state = CreateState();
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("p", "u", catalog.Fingerprint);
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20, DiabloDifficulty.Nightmare));

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			CreateStateWithLeader(0, 0), catalog, fingerprint,
			new[] {
				new AuthoritativeCathedralMonsterRuntimeBinding(0, 10, 20),
				new AuthoritativeCathedralMonsterRuntimeBinding(1, 11, 21),
			}));

		var valid = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
			CreateStateWithLeader(0, 1), catalog, fingerprint,
			new[] {
				new AuthoritativeCathedralMonsterRuntimeBinding(0, 10, 20),
				new AuthoritativeCathedralMonsterRuntimeBinding(1, 11, 21),
			});
		var target = valid.Records[0].Target ?? throw new InvalidOperationException("A valid pack member must produce a target.");
		Assert.Equal(11u, target.LeaderEntityId);
		Assert.Equal(AuthoritativeMonsterLeaderRelation.Leashed, target.LeaderRelation);
	}

	[Theory]
	[InlineData(0, AuthoritativeCathedralMonsterRuntimeUnsupportedReason.EmptyHitPoints)]
	[InlineData(32, AuthoritativeCathedralMonsterRuntimeUnsupportedReason.LowHitPoints)]
	public void LowQ6AndEmptyRowsRemainRaw(int hitPoints,
		AuthoritativeCathedralMonsterRuntimeUnsupportedReason expected)
	{
		var catalog = CreateCatalog();
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateState(ordinaryHitPoints: hitPoints), catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20);

		Assert.Null(result.Records[0].Target);
		Assert.Equal(expected, result.Records[0].UnsupportedReason);
	}

	[Fact]
	public void InvalidGoalInvalidFlagAndPetrifiedRowsRemainRawWithoutReset()
	{
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("p", "u", catalog.Fingerprint);
		var invalidGoal = CreateStateWithMonster(row => row with { Goal = 0 });
		var invalid = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			invalidGoal, catalog, fingerprint, 10, 20);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.UnsupportedGoalState,
			invalid.Records[0].UnsupportedReason);

		var invalidFlag = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateStateWithMonster(row => row with { Invalid = true }), catalog, fingerprint, 10, 20);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.InvalidNativeState,
			invalidFlag.Records[0].UnsupportedReason);

		var petrified = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateState(ordinaryPetrified: true), catalog, fingerprint, 10, 20);
		Assert.True(petrified.MonsterPetrified[0]);
		Assert.Null(petrified.Records[0].Target);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.Petrified,
			petrified.Records[0].UnsupportedReason);
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("ambiguous")]
	[InlineData("wrong-stable-id")]
	public void SuppliedTypeCatalogRejectsMissingAmbiguousAndWrongBindings(string defect)
	{
		AuthoritativeMonsterTypeDefinition Type(uint id, uint native) => new(
			id, native, "fixture", 1, 4, 10, 1, 100, 0, 0, 0, 0, 0, true, true, true);
		var rows = new List<AuthoritativeMonsterTypeDefinition> { Type(20, 0), Type(129, 109) };
		if (defect == "missing") rows.RemoveAt(0);
		if (defect == "ambiguous") rows.Add(Type(30, 0));
		if (defect == "wrong-stable-id") rows[0] = Type(30, 0);
		var catalog = CreateCatalog();
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateState(), catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20,
			monsterTypeCatalog: new(rows)));
	}

	[Fact]
	public void UnavailableSourceRowsAreRetainedButNeverActivated()
	{
		var rows = CreateCatalog().Definitions.ToArray();
		var row = rows[112];
		rows[112] = new(112, row.StableSymbol, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(rows);
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateState(ordinaryType: 112), catalog, Fingerprint("p", "u", catalog.Fingerprint), 10, 20);
		Assert.Null(result.Records[0].Target);
		Assert.Equal(112, result.Records[0].InitialState.Type);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.UnavailableSourceMetadata,
			result.Records[0].UnsupportedReason);
	}

	[Fact]
	public void UniqueImportRequiresCoherentMetadataAndPreservesRawExperienceAndZeroSeed()
	{
		var state = CreateStateWithMonster(row => row with { UniqueType = 0, TalkMessage = 7, RndItemSeed = 0, AiSeed = 1 });
		var unique = new AuthoritativeCathedralUniqueDefinition(0, 0, 5, 10, 0, 0, 1, 1,
			0, 0, 0, 0, 0, 0, 0, AuthoritativeCathedralUniquePack.None, 40, 0, 7, false);
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("p", "u", catalog.Fingerprint);
		var identities = new Dictionary<int, string> { [0] = "native.unique.0" };
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20, uniqueDefinitions: [unique]));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20, uniqueDefinitions: [unique with { MonsterType = 2 }], uniqueIdentities: identities));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20, uniqueDefinitions: [unique],
			uniqueIdentities: new Dictionary<int, string> { [0] = "duplicate", [1] = "duplicate" }));
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			state, catalog, fingerprint, 10, 20, uniqueDefinitions: [unique], uniqueIdentities: identities);
		var target = result.Records[0].Target ?? throw new InvalidOperationException("Unique import failed.");
		Assert.True(target.IsUnique);
		Assert.Equal("native.unique.0", target.UniqueIdentity);
		Assert.Equal(7, target.DialogueMessageId);
		Assert.Equal(100U, target.ExperienceReward); // defeat path, not initialization, doubles this
		Assert.Equal(40, target.ToHit);
		Assert.Equal((0U, 1U), (target.MonsterItemSeed, target.MonsterAiSeed));
		Assert.Null(target.Drop);
	}

	[Fact]
	public void FallenAnimationAndPetrifiedTailStayDetachedAndNonzeroGoalVarsAreNotReset()
	{
		var catalog = CreateCatalog();
		var fingerprint = Fingerprint("p", "u", catalog.Fingerprint);
		var state = CreateStateWithMonster(row => row with {
			Ai = 8, AnimCurrentFrame = 3, AnimTickCounter = 2, AnimNumberOfFrames = 4, AnimTicksPerFrame = 3,
		});
		var result = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(state, catalog, fingerprint, 10, 20);
		var target = result.Records[0].Target ?? throw new InvalidOperationException("Fallen import failed.");
		Assert.Equal((3, 2), (target.FallenStandAnimationFrame, target.FallenStandAnimationTick));
		Assert.Equal(200, result.MonsterPetrified.Count);
		Assert.NotSame(state.MonsterPetrified, result.MonsterPetrified);
		Assert.False(result.Records[0].Initialization.InitialAnimationPetrified);
		var unsupported = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithDeterministicIds(
			CreateStateWithMonster(row => row with { GoalVar1 = 1 }), catalog, fingerprint, 10, 20);
		Assert.Null(unsupported.Records[0].Target);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.UnsupportedGoalState, unsupported.Records[0].UnsupportedReason);
	}

	private static string Fingerprint(string generation, string uniques, string runtime)
	{
		static string Expand(string value)
			=> value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F')
				? value
				: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
		return AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(
			Expand(generation), Expand(uniques), Expand(runtime));
	}

	private static AuthoritativeCathedralMonsterRuntimeCatalog CreateCatalog()
		=> new(Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
			.Select(index => new AuthoritativeCathedralMonsterRuntimeDefinition(
				index,
				index == 12 ? "Gargoyle" : $"MONSTER_{index:000}",
				index == 12 ? "Gargoyle" : $"Monster_{index:000}",
				10,
				20,
				4,
				22,
				5,
				index % 3,
				index % 8,
				index == 0 ? 0xC007 : 0,
				100,
				12)));

	private static AuthoritativeCathedralThemeRoomState CreateState(
		int ordinaryMode = 0,
		int ordinaryHitPoints = 6400,
		int ordinaryType = 0,
		AuthoritativeCathedralPopulationPoint? ordinaryTile = null,
		IReadOnlyList<int>? activeIds = null,
		int resistance = 0,
		uint flags = 0,
		int ai = 0,
		bool ordinaryPetrified = false)
	{
		var monsters = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.MonsterCapacity)
			.Select(slot => new AuthoritativeCathedralPopulationMonsterState {
				Slot = slot,
				Type = slot == 1 ? AuthoritativeCathedralPopulationParameters.GolemNativeType : ordinaryType,
				LevelType = 0,
				Mode = 0,
				Goal = 1,
				UniqueType = byte.MaxValue,
				Enemy = 0,
				Direction = 0,
				AnimTicksPerFrame = 1,
				AnimNumberOfFrames = 1,
				HitPoints = slot == 0 ? ordinaryHitPoints : 6400,
				MaxHitPoints = slot == 0 ? ordinaryHitPoints : 6400,
				RndItemSeed = (uint)(slot + 7),
				AiSeed = (uint)(slot + 8),
				Leader = 255,
				Resistance = resistance,
				Flags = flags,
				Ai = ai,
				Tile = slot == 0 && ordinaryTile is { } tile ? tile : new(slot, 0),
				Future = slot == 0 && ordinaryTile is { } future ? future : new(slot, 0),
			}).ToArray();
		monsters[0] = monsters[0] with { Mode = ordinaryMode };
		var objectSlots = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.ObjectCapacity)
			.Select(slot => new AuthoritativeCathedralPopulationObjectState { Slot = slot, Type = -1, BookMessage = -1 })
			.ToArray();
		var itemSlots = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.ItemCapacity)
			.Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
		var lights = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.LightCapacity)
			.Select(light => new AuthoritativeCathedralEncounterLightState(light, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false))
			.ToArray();
		var ids = activeIds?.ToArray() ?? Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.MonsterCapacity).ToArray();
		return new AuthoritativeCathedralThemeRoomState(
			objectSlots, Enumerable.Range(0, 127).ToArray(), Enumerable.Range(0, 127).ToArray(), 0,
			monsters, ids, 2, 2, new int[AuthoritativeCathedralThemeRoomState.CellCount],
			new int[AuthoritativeCathedralThemeRoomState.CellCount], itemSlots, Enumerable.Range(0, 127).ToArray(), 0,
			new int[AuthoritativeCathedralThemeRoomState.CellCount], new HashSet<int>(),
			new ushort[AuthoritativeCathedralThemeRoomState.CellCount], new sbyte[AuthoritativeCathedralThemeRoomState.CellCount],
			new byte[AuthoritativeCathedralThemeRoomState.CellCount], new byte[AuthoritativeCathedralPopulationGeometry.PieceCount],
			new byte[AuthoritativeCathedralThemeRoomState.CellCount], new int[AuthoritativeCathedralThemeRoomState.CellCount],
			new byte[AuthoritativeCathedralThemeRoomState.CellCount], new byte[AuthoritativeCathedralThemeRoomState.CellCount],
			Enumerable.Range(0, 32).ToArray(), 0, lights, false, false,
			monsterPetrified: Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.MonsterCapacity)
				.Select(slot => slot == 0 && ordinaryPetrified).ToArray());
	}

	private static AuthoritativeCathedralThemeRoomState CreateStateWithMonster(
		Func<AuthoritativeCathedralPopulationMonsterState, AuthoritativeCathedralPopulationMonsterState> mutate)
	{
		var state = CreateState();
		var monsters = state.MonsterSlots.ToArray();
		monsters[0] = mutate(monsters[0]);
		return new AuthoritativeCathedralThemeRoomState(
			state.ObjectSlots, state.ActiveObjectIds, state.AvailableObjectIds, state.ActiveObjectCount,
			monsters, state.ActiveMonsterIds, state.ActiveMonsterCount, state.MonsterTargetCount,
			state.MonsterRefs, state.PlayerRefs, state.ItemSlots, state.ActiveItemIds,
			state.ActiveItemCount, state.ItemRefs, state.UniqueItemFlags, state.Pieces, state.Transparency,
			state.Special, state.PieceProperties, state.TileFlags, state.ObjectRefs, state.Light, state.PreLight,
			state.ActiveLightIds, state.ActiveLightCount, state.LightRecords, state.UpdateLighting, state.DisableLighting,
			flags: state.Flags, corpses: state.Corpses, monsterPetrified: state.MonsterPetrified);
	}

	private static AuthoritativeCathedralThemeRoomState CreateStateWithLeader(int slot, int leader)
	{
		var state = CreateState();
		var monsters = state.MonsterSlots.ToArray();
		monsters[slot] = monsters[slot] with { Leader = leader, LeaderRelation = 1 };
		return new AuthoritativeCathedralThemeRoomState(
			state.ObjectSlots, state.ActiveObjectIds, state.AvailableObjectIds, state.ActiveObjectCount,
			monsters, state.ActiveMonsterIds, state.ActiveMonsterCount, state.MonsterTargetCount,
			state.MonsterRefs, state.PlayerRefs, state.ItemSlots, state.ActiveItemIds,
			state.ActiveItemCount, state.ItemRefs, state.UniqueItemFlags, state.Pieces, state.Transparency,
			state.Special, state.PieceProperties, state.TileFlags, state.ObjectRefs, state.Light, state.PreLight,
			state.ActiveLightIds, state.ActiveLightCount, state.LightRecords, state.UpdateLighting, state.DisableLighting,
			flags: state.Flags, corpses: state.Corpses, monsterPetrified: state.MonsterPetrified);
	}
}

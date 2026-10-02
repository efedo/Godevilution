using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class StoreCatalogCohortTests
{
	[Fact]
	public void CaptureAllIncludesEveryStoreAndDetachedRemainingStock()
	{
		var catalog = CreateCatalog();
		Assert.True(catalog.RemoveItem(9, 2));
		Assert.True(catalog.TryGetItem(9, 4, out var sourceItem));

		var captured = catalog.CaptureAll();

		Assert.Equal(new uint[] { 3, 5, 9 }, captured.Select(state => state.StoreId));
		Assert.Empty(captured.Single(state => state.StoreId == 3).Items!);
		var capturedItem = Assert.Single(captured.Single(state => state.StoreId == 9).Items!);
		Assert.Equal(4U, capturedItem.StoreSlot);
		Assert.NotSame(sourceItem.State, capturedItem.State);

		var capturedItems = Assert.IsType<StoreItem[]>(
			captured.Single(state => state.StoreId == 9).Items);
		capturedItems[0] = new StoreItem(4, 999, 999);
		Assert.True(catalog.TryGetItem(9, 4, out var unchanged));
		Assert.Equal(44U, unchanged.ItemSeed);
		Assert.Equal(AuthoritativeStoreRole.Wirt, captured.Single(state => state.StoreId == 9).Role);
	}

	[Fact]
	public void RestoreAllRoundTripsAllStoresAndPreservesPurchasedSlotRemoval()
	{
		var catalog = CreateCatalog();
		Assert.True(catalog.RemoveItem(9, 2));
		var expected = catalog.CaptureAll();

		catalog.ReplaceStore(3, [new StoreItem(8, 808, 808)]);
		catalog.ReplaceStore(5, []);
		catalog.ReplaceStore(9, [new StoreItem(2, 202, 202)]);

		catalog.RestoreAll(expected);

		AssertCatalogEqual(expected, catalog.CaptureAll());
		Assert.False(catalog.TryGetItem(9, 2, out _));
		Assert.True(catalog.TryGetItem(9, 4, out var retained));
		Assert.Equal(44U, retained.ItemSeed);
		Assert.True(catalog.TryGetRole(9, out var role));
		Assert.Equal(AuthoritativeStoreRole.Wirt, role);
	}

	[Fact]
	public void RestoreAllRejectsDuplicateStoreIdsWithoutMutation()
	{
		var catalog = CreateCatalog();
		var valid = catalog.CaptureAll().ToArray();
		valid[1] = valid[0] with { StoreId = valid[0].StoreId };

		AssertRejectsWithoutMutation(catalog, valid);
	}

	[Fact]
	public void RestoreAllRejectsMissingOrExtraStoresWithoutMutation()
	{
		var catalog = CreateCatalog();
		var valid = catalog.CaptureAll().ToArray();

		AssertRejectsWithoutMutation(catalog, valid.Skip(1).ToArray());
		AssertRejectsWithoutMutation(
			catalog,
			valid.Append(new DiabloStoreCatalogState(
				99,
				AuthoritativeStoreRole.Generic,
				[])).ToArray());
	}

	[Fact]
	public void RestoreAllRejectsDuplicateSlotsNullEntriesAndRoleMismatchWithoutMutation()
	{
		var catalog = CreateCatalog();
		var valid = catalog.CaptureAll().ToArray();
		var populated = valid.Single(state => state.StoreId == 9);
		var item = populated.Items!.Single(candidate => candidate.StoreSlot == 2);

		AssertRejectsWithoutMutation(
			catalog,
			Replace(valid, populated with {
				Items = populated.Items!.Append(item with { ItemSeed = 404 }).ToArray(),
			}));
		AssertRejectsWithoutMutation(
			catalog,
			Replace(valid, populated with { Items = new StoreItem[] { null! } }));
		AssertRejectsWithoutMutation(
			catalog,
			Replace(valid, populated with {
				Items = new[] { item with { State = null! } },
			}));
		AssertRejectsWithoutMutation(
			catalog,
			Replace(valid, populated with { Items = null }));
		var nullStore = valid.ToArray();
		nullStore[0] = null!;
		AssertRejectsWithoutMutation(catalog, nullStore);
		AssertRejectsWithoutMutation(
			catalog,
			Replace(valid, valid.Single(state => state.StoreId == 3) with {
				Role = AuthoritativeStoreRole.Wirt,
			}));
	}

	[Fact]
	public void CaptureRestoreLeavesLegacyStoreQueriesValueIdentical()
	{
		var catalog = CreateCatalog();
		var expected = catalog.CaptureAll();

		catalog.RestoreAll(expected);

		foreach (var state in expected) {
			Assert.True(catalog.ContainsStore(state.StoreId));
			Assert.True(catalog.TryGetRole(state.StoreId, out var role));
			Assert.Equal(state.Role, role);
			var actual = catalog.GetItems(state.StoreId);
			Assert.Equal(state.Items!.Count, actual.Count);
			for (var index = 0; index < actual.Count; index++)
				Assert.Equal(state.Items[index], actual[index]);
		}
	}

	private static StoreCatalog CreateCatalog()
	{
		var catalog = new StoreCatalog();
		catalog.AddStore(9, [
			new StoreItem(2, 22, 220, ItemState(2)),
			new StoreItem(4, 44, 440, ItemState(4)),
		], AuthoritativeStoreRole.Wirt);
		catalog.AddStore(3, [], AuthoritativeStoreRole.Generic);
		catalog.AddStore(5, [new StoreItem(1, 55, 550, ItemState(5))], AuthoritativeStoreRole.Smith);
		return catalog;
	}

	private static AuthoritativeItemState ItemState(int itemType)
	{
		return AuthoritativeItemState.Empty with {
			ItemType = itemType,
			Identified = true,
			BaseName = $"item-{itemType}",
		};
	}

	private static DiabloStoreCatalogState[] Replace(
		IReadOnlyList<DiabloStoreCatalogState> states,
		DiabloStoreCatalogState replacement)
	{
		return states
			.Select(state => state.StoreId == replacement.StoreId ? replacement : state)
			.ToArray();
	}

	private static void AssertRejectsWithoutMutation(
		StoreCatalog catalog,
		IReadOnlyList<DiabloStoreCatalogState> invalid)
	{
		var before = catalog.CaptureAll();
		Assert.Throws<InvalidDataException>(() => catalog.RestoreAll(invalid));
		AssertCatalogEqual(before, catalog.CaptureAll());
	}

	private static void AssertCatalogEqual(
		IReadOnlyList<DiabloStoreCatalogState> expected,
		IReadOnlyList<DiabloStoreCatalogState> actual)
	{
		Assert.Equal(expected.Count, actual.Count);
		for (var storeIndex = 0; storeIndex < expected.Count; storeIndex++) {
			Assert.Equal(expected[storeIndex].StoreId, actual[storeIndex].StoreId);
			Assert.Equal(expected[storeIndex].Role, actual[storeIndex].Role);
			var expectedItems = expected[storeIndex].Items!;
			var actualItems = actual[storeIndex].Items!;
			Assert.Equal(expectedItems.Count, actualItems.Count);
			for (var itemIndex = 0; itemIndex < expectedItems.Count; itemIndex++)
				Assert.Equal(expectedItems[itemIndex], actualItems[itemIndex]);
		}
	}
}

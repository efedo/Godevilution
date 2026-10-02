namespace Devilution.Diablo;

/** Detached stock projection for one registered authoritative store. */
public sealed record DiabloStoreCatalogState(
	uint StoreId,
	AuthoritativeStoreRole Role,
	IReadOnlyList<StoreItem>? Items);

namespace Devilution.Client.PresentationAssets;

/** Client-only localization lookup with a caller-provided fallback. */
public sealed class PresentationLocalization
{
    private readonly PresentationAssetCatalog catalog;

    public PresentationLocalization(PresentationAssetCatalog catalog)
        => this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public string Resolve(string key, string fallback)
        => catalog.TryGetLocalization(key, out var text) ? text : fallback;
}

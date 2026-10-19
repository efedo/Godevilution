namespace Devilution.Client.PresentationAssets;

/** Resolves a validated pack binding without loading Godot resources. */
public sealed class PresentationAssetResolver
{
    private readonly PresentationAssetCatalog catalog;
    private readonly string manifestPath;

    public PresentationAssetResolver(PresentationAssetCatalog catalog, string manifestPath)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (!PresentationAssetPath.TryNormalizeResourcePath(manifestPath, out var normalized, out var error))
            throw new ArgumentException(error, nameof(manifestPath));
        this.manifestPath = normalized;
    }

    public bool TryResolve(
        string id,
        out PresentationAssetResolution resolution)
    {
        resolution = default!;
        if (!catalog.TryGet(id, out var definition)
            || definition.Kind == PresentationAssetKind.Localization)
            return false;
        if (!PresentationAssetPath.TryCombineResourcePath(
                manifestPath,
                definition.Path,
                out var resourcePath,
                out var error))
            return false;
        resolution = new PresentationAssetResolution(id, definition, resourcePath);
        return true;
    }

    public bool TryResolveFrame(
        string id,
        int frameIndex,
        out PresentationAssetResolution resolution,
        out PresentationFrameRegion? region)
    {
        if (!TryResolve(id, out resolution)) {
            region = null;
            return false;
        }
        var layout = resolution.Definition.Frames;
        if (layout is null) {
            region = null;
            return true;
        }
        region = ResolveFrameRegion(layout, frameIndex);
        return true;
    }

    public static PresentationFrameRegion ResolveFrameRegion(
        PresentationFrameLayout layout,
        int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var index = Math.Clamp(frameIndex, 0, layout.FrameCount - 1);
        var column = index % layout.Columns;
        var row = index / layout.Columns;
        return new PresentationFrameRegion(
            checked(column * layout.FrameWidth),
            checked(row * layout.FrameHeight),
            layout.FrameWidth,
            layout.FrameHeight);
    }
}

public sealed record PresentationAssetResolution(
    string Id,
    PresentationAssetDefinition Definition,
    string ResourcePath);

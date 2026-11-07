namespace Devilution.Client.PresentationAssets;

/** Catalogs and atlas declarations are immutable; validate and normalize each binding once. */
public sealed class NativeSceneAtlasPathCache(string manifestPath)
{
    private readonly Dictionary<(NativeSceneFamily Family, int Atlas), NativeSceneAtlasPathBinding> bindings = [];
    public int ResolutionCount { get; private set; }

    public NativeSceneAtlasPathBinding Resolve(NativeSceneFamily family, int atlasId)
    {
        var key = (family, atlasId);
        if (bindings.TryGetValue(key, out var binding)) return binding;
        ResolutionCount++;
        if (!family.TryGetAtlas(atlasId, out var atlas))
            binding = new(null, $"Native scene atlas {atlasId} is not declared.");
        else if (!PresentationAssetPath.TryCombineResourcePath(manifestPath, atlas.Path, out var path, out var error))
            binding = new(null, $"Native scene atlas path was refused: {error}.");
        else binding = new(path, null);
        bindings.Add(key, binding);
        return binding;
    }
}

public sealed record NativeSceneAtlasPathBinding(string? ResourcePath, string? Diagnostic);

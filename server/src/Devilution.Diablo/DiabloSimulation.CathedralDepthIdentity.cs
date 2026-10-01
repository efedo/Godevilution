namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /** Immutable depth profile identity used only by game-owned persistence, not the live wire. */
    public DiabloCathedralDepthWorldIdentity? CathedralDepthWorldIdentity { get; private set; }

    internal void ConfigureCathedralDepthWorldIdentity(DiabloCathedralDepthWorldIdentity? identity)
    {
        if (CathedralDepthWorldIdentity is not null)
            throw new InvalidOperationException("Cathedral depth world identity is already configured.");
        if (identity is not null && NativeGeneratedWorldIdentity is not null)
            throw new InvalidDataException("A simulation cannot combine L1 and Cathedral depth generation identities.");
        CathedralDepthWorldIdentity = identity;
    }
}

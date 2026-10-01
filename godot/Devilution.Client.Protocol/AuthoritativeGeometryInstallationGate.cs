namespace Devilution.Client.Protocol;

/** Tracks an explicitly accepted geometry generation installed by presentation. */
public sealed class AuthoritativeGeometryInstallationGate
{
    private ulong? installedRevision;

    /** Returns whether geometry for the accepted generation still needs installation. */
    public bool NeedsInstallation(ulong revision)
        => installedRevision != revision;

    /** Records a completed installation; call only after the projection succeeds. */
    public void MarkInstalled(ulong revision)
        => installedRevision = revision;

    /** Forces the next accepted generation to refresh, including a repeated number. */
    public void Invalidate()
        => installedRevision = null;
}

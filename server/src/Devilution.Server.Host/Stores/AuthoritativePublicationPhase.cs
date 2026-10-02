namespace Devilution.Server.Stores;

/** Internal observation seam for owned managed-process interruption fixtures. */
internal enum AuthoritativePublicationPhase
{
    BeforeStageWrite,
    AfterPartialWriteBeforeFlush,
    AfterFlushBeforeRename,
    AfterCanonicalRename,
    AfterMarkerCommit,
}

/** Shared durable-write boundaries; fault observers remain instance-only test seams. */
internal static class AuthoritativePublicationWriter
{
    internal static void Write(
        FileStream output,
        byte[] bytes,
        string stagePath,
        Action<AuthoritativePublicationPhase, string>? observer)
    {
        observer?.Invoke(AuthoritativePublicationPhase.BeforeStageWrite, stagePath);
        if (observer is null) {
            output.Write(bytes);
            output.Flush(flushToDisk: true);
            return;
        }
        var split = bytes.Length == 0 ? 0 : Math.Max(1, bytes.Length / 2);
        output.Write(bytes, 0, split);
        output.Flush(flushToDisk: false);
        observer(AuthoritativePublicationPhase.AfterPartialWriteBeforeFlush, stagePath);
        output.Write(bytes, split, bytes.Length - split);
        output.Flush(flushToDisk: true);
    }
}

namespace Devilution.Client.Protocol;

/** Animation recovery follows admitted publications, independently of retained-content revisions. */
public sealed class AuthoritativePresentationEpochGate
{
    private ulong pendingAdmissionRevision;
    public bool IsPending { get; private set; }

    public void WaitForAdmission(ulong admittedRevision)
    {
        pendingAdmissionRevision = admittedRevision;
        IsPending = true;
    }

    public bool Observe(bool hasSnapshot, bool connected, ulong? admittedRevision)
    {
        if (!hasSnapshot) {
            // Session counters may restart at the same value after a return to the menu.
            IsPending = false;
            pendingAdmissionRevision = 0;
            return false;
        }
        if (!connected || !IsPending || admittedRevision is null || admittedRevision == pendingAdmissionRevision)
            return false;
        IsPending = false;
        return true;
    }
}

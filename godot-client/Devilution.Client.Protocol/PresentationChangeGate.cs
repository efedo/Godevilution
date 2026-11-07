namespace Devilution.Client.Protocol;

/** Retains immutable presentation inputs; hidden panels consume their latest state only when shown. */
public sealed class PresentationChangeGate<T>
{
    private T? previous;
    private bool initialized;
    private bool deferred;

    public bool ShouldUpdate(bool visible, T value)
    {
        if (!visible) { deferred = true; return false; }
        if (initialized && !deferred && EqualityComparer<T>.Default.Equals(previous!, value)) return false;
        previous = value;
        initialized = true;
        deferred = false;
        return true;
    }

    public void Invalidate() => initialized = false;
}

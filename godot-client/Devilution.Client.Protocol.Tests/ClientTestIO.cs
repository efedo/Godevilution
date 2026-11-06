using Devilution.Client.Protocol;

namespace Devilution.Client.Protocol.Tests;

/** Socket-delivery waits never advance the scheduling clock. Their deadline only detects a stuck test. */
internal static class ClientTestIO
{
    public static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(1, timeout.Token);
    }

    public static async Task PumpUntilAsync(AuthoritativeClient client, Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition()) {
            await client.PollAsync(timeout.Token);
            await Task.Delay(1, timeout.Token);
        }
    }
}

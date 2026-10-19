using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class SessionLifecyclePolicyTests
{
    [Fact]
    public async Task CleanupJoinsWorkBeforeDisposingTransport()
    {
        var joined = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = false;
        using var sceneLifetime = new CancellationTokenSource();
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var cleanup = coordinator.Begin(
            () => joined.Task,
            () => {
                disposed = true;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);

        await Task.Yield();
        Assert.False(disposed);
        joined.SetResult(null);
        var result = await cleanup;

        Assert.True(result.Succeeded);
        Assert.True(disposed);
    }

    [Fact]
    public async Task DuplicateCleanupRequestsShareOneJoinAndDispose()
    {
        var joined = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var joinCount = 0;
        var disposeCount = 0;
        using var sceneLifetime = new CancellationTokenSource();
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var first = coordinator.Begin(
            async () => {
                joinCount++;
                await joined.Task;
            },
            () => {
                disposeCount++;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);
        var duplicate = coordinator.Begin(
            () => Task.CompletedTask,
            () => Task.CompletedTask,
            sceneLifetime.Token);

        Assert.Same(first, duplicate);
        joined.SetResult(null);
        Assert.True((await first).Succeeded);
        Assert.Equal(1, joinCount);
        Assert.Equal(1, disposeCount);
    }

    [Fact]
    public async Task SceneExitJoinsCanceledWorkButDoesNotReportAUsableMenu()
    {
        var joined = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sceneLifetime = new CancellationTokenSource();
        var disposed = false;
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var cleanup = coordinator.Begin(
            () => joined.Task,
            () => {
                disposed = true;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);
        sceneLifetime.Cancel();
        joined.SetResult(null);

        var result = await cleanup;

        Assert.True(disposed);
        Assert.True(result.SceneExiting);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DisposalFailureIsReturnedAfterWorkHasJoined()
    {
        var joined = false;
        using var sceneLifetime = new CancellationTokenSource();
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var result = await coordinator.Begin(
            () => {
                joined = true;
                return Task.CompletedTask;
            },
            () => Task.FromException(new IOException("dispose failed")),
            sceneLifetime.Token);

        Assert.True(joined);
        Assert.False(result.Succeeded);
        Assert.Contains("dispose failed", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SceneExitClassificationRetainsDisposalOutcomeAfterCancellation(bool disposalFails)
    {
        var joined = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sceneLifetime = new CancellationTokenSource();
        var failure = new IOException("disposal after scene exit");
        var disposalCount = 0;
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();
        var cleanup = coordinator.Begin(
            () => joined.Task,
            () => {
                disposalCount++;
                return disposalFails ? Task.FromException(failure) : Task.CompletedTask;
            },
            sceneLifetime.Token);

        Assert.False(cleanup.IsCompleted);
        sceneLifetime.Cancel();
        joined.SetResult(null);
        var outcome = await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(cleanup.IsCompletedSuccessfully);
        Assert.Equal(1, disposalCount);
        Assert.Equal(!disposalFails,
            Devilution.Client.SessionLifecyclePolicy.CanCompleteSceneExit(outcome, returnFailure: null));
        if (disposalFails)
            Assert.Same(failure, outcome.Failure);
        else {
            Assert.True(outcome.SceneExiting);
            Assert.False(outcome.Succeeded);
            Assert.Null(outcome.Failure);
        }
    }

    [Fact]
    public void SceneExitRefusesMissingOutcomeOrFaultedReturnEvenAfterSuccessfulDisposal()
    {
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanCompleteSceneExit(null, null));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanCompleteSceneExit(
            Devilution.Client.SessionCleanupResult.Canceled(), new IOException("return failed")));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanCompleteSceneExit(
            new Devilution.Client.SessionCleanupResult(false, false, null, null), null));
        Assert.True(Devilution.Client.SessionLifecyclePolicy.CanCompleteSceneExit(
            Devilution.Client.SessionCleanupResult.Completed(new IOException("joined poll diagnostic")), null));
    }

    [Fact]
    public async Task FaultedOldWorkIsDiagnosticOnlyAfterSuccessfulDisposalAndAllowsNextSession()
    {
        using var sceneLifetime = new CancellationTokenSource();
        var disposeCount = 0;
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var first = await coordinator.Begin(
            () => Task.FromException(new IOException("terminal poll")),
            () => {
                disposeCount++;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);
        var second = await coordinator.Begin(
            () => Task.CompletedTask,
            () => {
                disposeCount++;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);

        Assert.True(first.Succeeded);
        Assert.Null(first.Failure);
        Assert.Contains("terminal poll", first.WorkFailure!.Message, StringComparison.Ordinal);
        Assert.True(second.Succeeded);
        Assert.Equal(2, disposeCount);
    }

    [Fact]
    public async Task DisposalFailureCanBeRetriedWithoutConcurrentOrDoubleDisposal()
    {
        using var sceneLifetime = new CancellationTokenSource();
        var disposeCount = 0;
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var first = await coordinator.Begin(
            () => Task.CompletedTask,
            () => {
                disposeCount++;
                return Task.FromException(new IOException("first disposal failed"));
            },
            sceneLifetime.Token);
        var second = await coordinator.Begin(
            () => Task.CompletedTask,
            () => {
                disposeCount++;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);

        Assert.False(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, disposeCount);
    }

    [Fact]
    public async Task AlreadyCanceledSceneStillJoinsAndDisposesOwnedWork()
    {
        using var sceneLifetime = new CancellationTokenSource();
        sceneLifetime.Cancel();
        var disposed = false;
        var coordinator = new Devilution.Client.SessionCleanupCoordinator();

        var result = await coordinator.Begin(
            () => Task.CompletedTask,
            () => {
                disposed = true;
                return Task.CompletedTask;
            },
            sceneLifetime.Token);

        Assert.True(disposed);
        Assert.True(result.SceneExiting);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    public void ReturnRequiresAnEmptyImmutableRetryLedger(int pendingCommandCount, bool expected)
        => Assert.Equal(expected,
            Devilution.Client.SessionLifecyclePolicy.CanReturnToSessionMenu(pendingCommandCount));

    [Fact]
    public void ConnectingAttemptCanBeCancelledBeforeAnyCommandIsQueued()
    {
        Assert.True(Devilution.Client.SessionLifecyclePolicy.CanBeginReturn(false, 0));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanBeginReturn(false, 1));
    }

    [Fact]
    public void DuplicateReturnIsRejectedWhileCleanupIsInProgress()
        => Assert.False(Devilution.Client.SessionLifecyclePolicy.CanBeginReturn(true, 0));

    [Fact]
    public void RefusalMessageDoesNotSuggestDiscardingPendingCommands()
    {
        var message = Devilution.Client.SessionLifecyclePolicy.ReturnMessage(1);

        Assert.Contains("acknowledged", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discard", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("retry", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaleCallbacksCannotBecomeCurrentAfterGenerationChanges()
    {
        Assert.True(Devilution.Client.SessionLifecyclePolicy.IsCurrentGeneration(7, 7));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.IsCurrentGeneration(8, 7));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanPublishCallback(true, 7, 7));
    }

    [Fact]
    public void SceneExitCannotPublishACompletedCleanupAsANewMenu()
    {
        Assert.True(Devilution.Client.SessionLifecyclePolicy.CanShowSessionMenu(true, false));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanShowSessionMenu(true, true));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanShowSessionMenu(false, false));
    }

    [Fact]
    public void CleanupFailureKeepsTheMenuClosedAndExplainsTheFailure()
    {
        var message = Devilution.Client.SessionLifecyclePolicy.CleanupFailureMessage(
            new IOException("transport still open"));

        Assert.Contains("transport still open", message, StringComparison.Ordinal);
        Assert.Contains("remains closed", message, StringComparison.Ordinal);
        Assert.False(Devilution.Client.SessionLifecyclePolicy.CanShowSessionMenu(false, false));
    }

    [Fact]
    public void FailedConnectStillUsesTheExplicitReturnPath()
    {
        var message = Devilution.Client.SessionLifecyclePolicy.ReturnMessage(0);
        var selected = Options("remote.example", 6120) with {
            RequestedCharacterClassId = 2,
            RequestedCharacterName = "RemoteHero",
        };
        var defaults = Devilution.Client.SessionLifecyclePolicy.MenuDefaults(selected, null);

        Assert.Contains("Returning", message, StringComparison.Ordinal);
        Assert.True(Devilution.Client.SessionLifecyclePolicy.CanBeginReturn(false, 0));
        Assert.Equal("remote.example", defaults.Host);
        Assert.Equal(6120, defaults.Port);
        Assert.Equal("RemoteHero", defaults.RequestedCharacterName);
    }

    [Fact]
    public void RemoteEndpointDoesNotPersistOrOverwriteLocalBookmark()
    {
        var configured = Options("localhost", 6113);
        var remote = Options("example.test", 6113);

        Assert.True(Devilution.Client.SessionLifecyclePolicy.ShouldPersistLocalBookmark(
            configured, configured, "resume-token.txt"));
        Assert.False(Devilution.Client.SessionLifecyclePolicy.ShouldPersistLocalBookmark(
            configured, remote, "resume-token.txt"));
    }

    [Fact]
    public void MenuDefaultsRetainBookmarkWithoutRetainingNewCharacterRequest()
    {
        var configured = Options("localhost", 6113) with {
            RequestedCharacterClassId = 2,
            RequestedCharacterName = "NewHero",
        };

        var defaults = Devilution.Client.SessionLifecyclePolicy.MenuDefaults(
            configured, "private-resume-token");

        Assert.Equal("private-resume-token", defaults.ResumeToken);
        Assert.Null(defaults.RequestedCharacterClassId);
        Assert.Null(defaults.RequestedCharacterName);
    }

    [Fact]
    public void RemoteMenuDefaultsRetainEndpointAndResumeTokenInMemory()
    {
        var remote = Options("remote.example", 6120) with {
            ResumeToken = "remote-token",
        };

        var defaults = Devilution.Client.SessionLifecyclePolicy.MenuDefaults(remote, remote.ResumeToken);

        Assert.Equal("remote.example", defaults.Host);
        Assert.Equal(6120, defaults.Port);
        Assert.Equal("remote-token", defaults.ResumeToken);
    }

    [Theory]
    [InlineData(null, "confirmed-local-token", true)]
    [InlineData("stale-local-token", "confirmed-local-token", true)]
    [InlineData("confirmed-local-token", "confirmed-local-token", false)]
    [InlineData("confirmed-local-token", null, false)]
    public void ConfirmedLocalTokenOnlySupersedesAnAbsentOrDifferentBookmark(
        string? durableBookmark,
        string? confirmedToken,
        bool expected)
        => Assert.Equal(
            expected,
            Devilution.Client.SessionLifecyclePolicy.ShouldPreferConfirmedToken(
                durableBookmark,
                confirmedToken));

    [Theory]
    [InlineData(null)]
    [InlineData("stale-local-token")]
    public void ConfirmedLocalTokenBecomesMenuDefaultWithoutLeakingCredential(
        string? durableBookmark)
    {
        const string confirmedToken = "confirmed-local-token";
        var configured = Options("localhost", 6113) with {
            RequestedCharacterClassId = 2,
            RequestedCharacterName = "NewHero",
        };

        Assert.True(Devilution.Client.SessionLifecyclePolicy.ShouldPreferConfirmedToken(
            durableBookmark,
            confirmedToken));
        var defaults = Devilution.Client.SessionLifecyclePolicy.MenuDefaults(
            configured,
            confirmedToken);

        Assert.Equal(confirmedToken, defaults.ResumeToken);
        Assert.Null(defaults.RequestedCharacterClassId);
        Assert.Null(defaults.RequestedCharacterName);
        Assert.DoesNotContain(
            confirmedToken,
            Devilution.Client.SessionLifecyclePolicy.InMemoryResumeWarning,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MatchingDurableBookmarkDoesNotRequireAnInMemoryOnlyWarning()
    {
        const string confirmedToken = "confirmed-local-token";

        Assert.False(Devilution.Client.SessionLifecyclePolicy.ShouldPreferConfirmedToken(
            confirmedToken,
            confirmedToken));
        Assert.Contains(
            "local bookmark was not replaced",
            Devilution.Client.SessionLifecyclePolicy.InMemoryResumeWarning,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryOnlyCredentialWarningIsNotHiddenByOtherCleanupWarnings()
    {
        var warning = Devilution.Client.SessionLifecyclePolicy.MenuWarningMessage(
            true, "bookmark is read-only", new IOException("terminal poll"));

        Assert.StartsWith(Devilution.Client.SessionLifecyclePolicy.InMemoryResumeWarning, warning);
        Assert.Contains("only in memory", warning, StringComparison.Ordinal);
        Assert.Contains("bookmark is read-only", warning, StringComparison.Ordinal);
        Assert.Contains("terminal poll", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanMenuDoesNotShowAWarning()
        => Assert.Null(Devilution.Client.SessionLifecyclePolicy.MenuWarningMessage(false, null, null));

    [Fact]
    public void EmptyBookmarkLeavesConfiguredDefaultsUntouched()
    {
        var configured = Options("localhost", 6113) with {
            RequestedCharacterClassId = 2,
            RequestedCharacterName = "NewHero",
        };

        var defaults = Devilution.Client.SessionLifecyclePolicy.MenuDefaults(configured, null);

        Assert.Equal(configured, defaults);
    }

    private static ClientConnectionOptions Options(string host, int port)
        => new(host, port, "godot-test", "0.1.0", "content-hash");
}

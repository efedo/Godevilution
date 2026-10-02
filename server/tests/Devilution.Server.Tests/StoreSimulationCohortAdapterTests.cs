using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Focused adapter-boundary coverage for detached Diablo cohort installation. */
public sealed class StoreSimulationCohortAdapterTests
{
    [Fact]
    public void PrepareRecapturesDetachedBindingsAndCommitPreservesLegacyFacade()
    {
        var source = CreateExecutor();
        source.InitializeParticipant(
            "hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 7 });
        var checkpoint = source.CaptureCohortState();
        var target = CreateExecutor();

        using var prepared = target.PrepareCohortRestore(checkpoint);

        Assert.Equal(checkpoint.ModuleId, prepared.CohortState.ModuleId);
        Assert.Equal(checkpoint.FormatVersion, prepared.CohortState.FormatVersion);
        Assert.Equal(checkpoint.Tick, prepared.CohortState.Tick);
        Assert.Equal(
            checkpoint.Participants,
            prepared.CohortState.Participants);
        Assert.Equal(
            checkpoint.SerializedState,
            prepared.CohortState.SerializedState);
        Assert.True(
            Assert.IsAssignableFrom<IList<AuthoritativeModuleCohortParticipant>>(
                prepared.CohortState.Participants).IsReadOnly);
        Assert.Equal(
            checkpoint.SerializedState,
            source.CaptureCohortState().SerializedState);

        target.CommitCohortRestore(prepared);

        Assert.Equal(7U, target.GetPlayerState("hero").EntityId);
        target.InitializeParticipant(
            "new-hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 8 });
        Assert.Equal(8U, target.GetPlayerState("new-hero").EntityId);
        Assert.Equal(
            checkpoint.SerializedState,
            source.CaptureCohortState().SerializedState);
    }

    [Fact]
    public void PreparedTokensRejectForeignConsumedAndDisposedOwnership()
    {
        var source = CreateExecutor();
        source.InitializeParticipant(
            "hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 7 });
        var checkpoint = source.CaptureCohortState();
        var target = CreateExecutor();
        var foreign = CreateExecutor();

        var prepared = target.PrepareCohortRestore(checkpoint);
        Assert.Throws<InvalidOperationException>(
            () => foreign.CommitCohortRestore(prepared));
        target.CommitCohortRestore(prepared);
        Assert.Throws<InvalidOperationException>(
            () => target.CommitCohortRestore(prepared));

        var disposed = foreign.PrepareCohortRestore(checkpoint);
        disposed.Dispose();
        Assert.Throws<InvalidOperationException>(
            () => foreign.CommitCohortRestore(disposed));
    }

    [Fact]
    public void CommitRejectsEveryInterveningUseOfFreshTarget()
    {
        var source = CreateExecutor();
        source.InitializeParticipant(
            "hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 7 });
        var save = source.ExportPlayerSave("hero");
        var checkpoint = source.CaptureCohortState();

        var advanced = CreateExecutor();
        var advancedPrepared = advanced.PrepareCohortRestore(checkpoint);
        advanced.AdvanceTo(1);
        Assert.Throws<InvalidOperationException>(
            () => advanced.CommitCohortRestore(advancedPrepared));
        advancedPrepared.Dispose();

        var admitted = CreateExecutor();
        var admittedPrepared = admitted.PrepareCohortRestore(checkpoint);
        admitted.InitializeParticipant(
            "other",
            new AuthoritativeParticipantSelection(null) { EntityId = 8 });
        Assert.Throws<InvalidOperationException>(
            () => admitted.CommitCohortRestore(admittedPrepared));
        admittedPrepared.Dispose();

        var imported = CreateExecutor();
        var importedPrepared = imported.PrepareCohortRestore(checkpoint);
        imported.ImportPlayerSave("hero", save);
        Assert.Throws<InvalidOperationException>(
            () => imported.CommitCohortRestore(importedPrepared));
        importedPrepared.Dispose();

        Assert.Equal(checkpoint.SerializedState, source.CaptureCohortState().SerializedState);
    }

    [Fact]
    public void CompatibilityConstructorRetainsLazyParticipantsAndPerSessionSaveImport()
    {
        var source = CreateExecutor();

        var lazy = source.GetPlayerState("legacy");
        Assert.Equal(100U, lazy.Gold);
        Assert.True(source.SupportsCohortPersistence);

        var serialized = source.ExportSessionState("legacy");
        var restored = CreateExecutor();
        restored.ImportSessionState("legacy", serialized);

        Assert.Equal(lazy.Gold, restored.GetPlayerState("legacy").Gold);
        Assert.Equal(
            source.GetSessionStateTick("legacy"),
            restored.GetSessionStateTick("legacy"));
    }

    private static StoreSimulationExecutor CreateExecutor()
    {
        return new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 100,
            worldWidth: 8,
            worldHeight: 8,
            startingPositionX: 0,
            startingPositionY: 0);
    }
}

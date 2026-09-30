using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Entry metadata initializes new participants; durable state owns resumed positions. */
public sealed class NativeStartHostTests
{
    [Theory]
    [InlineData(false, null, 1U, 0, 0)]
    [InlineData(true, null, 1U, 0, 0)]
    [InlineData(true, 0U, 0U, 75, 68)]
    public void FactoryUsesOnlyTheRequestedLevelsContentOwnedEntry(
        bool nativeProfile,
        uint? requestedLevel,
        uint expectedLevel,
        int expectedX,
        int expectedY)
    {
        var adapter = CreateAdapter(nativeProfile, requestedLevel);
        adapter.InitializeParticipant("entry-test", new AuthoritativeParticipantSelection(0) {
            CharacterName = "entry-test",
        });

        var player = Assert.Single(adapter.CreateSnapshot("entry-test", 1, 0).Players);
        Assert.Equal((expectedLevel, expectedX, expectedY),
            (player.LevelId, player.PositionX, player.PositionY));
        Assert.Equal("entry-test", player.CharacterName);
        Assert.Equal(0U, player.CharacterClassId);
        Assert.True(player.Life > 0);
        Assert.Equal(player.LifeMaximum, player.Life);
    }

    [Theory]
    [InlineData(AuthoritativeGameMode.SinglePlayer)]
    [InlineData(AuthoritativeGameMode.Multiplayer)]
    public void ColdCheckpointRestoreKeepsMovedPositionAndCommandLedger(
        AuthoritativeGameMode mode)
    {
        const string sessionId = "native-start-save";
        var root = Path.Combine(Path.GetTempPath(), "Godevilution-NativeStartHostTest-" + Guid.NewGuid().ToString("N"));
        try {
            var source = CreateAdapter(true, 0, mode);
            source.InitializeParticipant(sessionId, new AuthoritativeParticipantSelection(0) {
                CharacterName = "native-save",
            });
            var initial = Assert.Single(source.CreateSnapshot(sessionId, 1, 0).Players);
            Assert.Equal((0U, 75, 68), (initial.LevelId, initial.PositionX, initial.PositionY));
            var commands = new AuthoritativeCommandServer(source);
            var firstMove = Move(1, 1, 1, 0);
            var accepted = commands.Process(sessionId, firstMove, 1);
            Assert.Equal(CommandStatus.Accepted, accepted.Status);
            source.AdvanceTo(32);
            var settled = Assert.Single(source.CreateSnapshot(sessionId, 1, 32).Players);
            Assert.Equal((0U, 76, 68), (settled.LevelId, settled.PositionX, settled.PositionY));
            Assert.Equal(initial.Life, settled.Life);

            var store = new AuthoritativeSessionStore(root);
            var persistence = new AuthoritativeSessionPersistence(sessionStore: store);
            persistence.Save(sessionId, source.ExportPlayerSave(sessionId), commands.ExportCompletedCommands(sessionId));
            var checkpoint = Assert.IsType<AuthoritativeSessionDocument>(store.Load(sessionId));
            Assert.Single(checkpoint.CommandReceipts);

            var replacement = CreateAdapter(true, 0, mode);
            replacement.ImportPlayerSave(sessionId, checkpoint.SerializedState);
            var restoredCommands = new AuthoritativeCommandServer(replacement);
            restoredCommands.ImportCompletedCommands(sessionId, checkpoint.CommandReceipts);
            var restored = Assert.Single(replacement.CreateSnapshot(sessionId, 1, 32).Players);
            Assert.Equal((0U, 76, 68), (restored.LevelId, restored.PositionX, restored.PositionY));
            Assert.Equal(settled.CharacterName, restored.CharacterName);
            Assert.Equal(settled.CharacterClassId, restored.CharacterClassId);
            Assert.Equal(settled.Life, restored.Life);
            Assert.Equal(settled.Mana, restored.Mana);
            Assert.Equal(settled.Gold, restored.Gold);
            Assert.Equal(settled.Belt, restored.Belt);
            Assert.Equal(settled.Equipment, restored.Equipment);
            Assert.Equal(settled.Inventory, restored.Inventory);

            var duplicate = restoredCommands.Process(sessionId, firstMove, 32);
            Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
            Assert.Equal(CommandRejectReason.Unspecified, duplicate.RejectReason);
            Assert.Equal(accepted.AppliedTick, duplicate.AppliedTick);
            Assert.Equal((76, 68), Position(replacement, sessionId, 32));

            var next = restoredCommands.Process(sessionId, Move(2, 33, 0, 1), 33);
            Assert.Equal(CommandStatus.Accepted, next.Status);
            replacement.AdvanceTo(64);
            Assert.Equal((76, 69), Position(replacement, sessionId, 64));
            Assert.Equal(new ulong[] { 1, 2 }, restoredCommands.ExportCompletedCommands(sessionId).Keys.Order());
        } finally {
            var resolved = Path.GetFullPath(root);
            if (Path.GetDirectoryName(resolved) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)
                && Path.GetFileName(resolved).StartsWith("Godevilution-NativeStartHostTest-", StringComparison.Ordinal)
                && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private static (int X, int Y) Position(IAuthoritativeGameHostAdapter adapter, string sessionId, ulong tick)
    {
        var player = Assert.Single(adapter.CreateSnapshot(sessionId, 1, tick).Players);
        return (player.PositionX, player.PositionY);
    }

    private static Command Move(ulong sequence, ulong tick, int x, int y)
        => new() {
            ClientSequence = sequence,
            RequestedTick = tick,
            MoveRequested = new MoveRequested { DirectionX = x, DirectionY = y },
        };

    private static IAuthoritativeGameHostAdapter CreateAdapter(
        bool nativeProfile,
        uint? startingLevel,
        AuthoritativeGameMode mode = AuthoritativeGameMode.SinglePlayer)
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content");
        var packs = new List<ContentPack> {
            ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "base")),
        };
        if (nativeProfile)
            packs.Add(ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "native-start")));
        var manifest = new ContentManifest("devilution.base", "0.1.0", packs);
        var registry = new GameplayModuleRegistry();
        registry.Load([new DiabloModule()], new GameplayModuleContext(manifest, new DelegateAuthoritativeClock(() => 0)));
        return new DiabloAuthoritativeGameHostAdapterFactory().Activate(registry,
            ComposedContent.Compose(manifest), new AuthoritativePlayerStartConfiguration(null, null, null) {
                StartingLevelId = startingLevel,
                RandomSeed = 0,
                GameMode = mode,
            });
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}

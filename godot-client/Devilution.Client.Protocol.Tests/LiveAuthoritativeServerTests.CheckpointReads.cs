using System.Text.Json;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    public static bool WindowsCheckpointSharing => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(WindowsCheckpointSharing), Skip = "Windows File.Move sharing contract.")]
    public void WindowsCheckpointPublicationRefusesOpenReader()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "Godevilution-Checkpoint-Publication-" + Guid.NewGuid().ToString("N"));
        var lease = LiveTestSaveRoot.Register(root);
        Exception? primaryFailure = null;
        try {
            var store = new AuthoritativeSessionStore(root);
            var receipts = new Dictionary<ulong, CommandResult>();
            store.Save("participant", "old-generation", receipts);
            var path = Path.Combine(root, "participant.session.json");
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete)) {
                // Characterize the actual publisher, not File.Replace or a
                // different rename API. Delete sharing alone is insufficient.
                var blocked = Record.Exception(() => store.Save("participant", "new-generation", receipts));
                Assert.NotNull(blocked);
                Assert.True(blocked is IOException or UnauthorizedAccessException, blocked.ToString());
                Assert.Contains(blocked.HResult & 0xFFFF, new[] { 5, 32 });
                Assert.Equal("old-generation", store.Load("participant")!.SerializedState);
            }
            store.Save("participant", "new-generation", receipts);
            Assert.Equal("new-generation", store.Load("participant")!.SerializedState);
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
            if (primaryFailure is null && cleanupFailures.Count == 0)
                CleanupLocalRoot(lease);
        }
    }

    [Theory]
    [InlineData("{broken", typeof(JsonException))]
    [InlineData("{}", typeof(KeyNotFoundException))]
    [InlineData("{\"SnapshotBase64\":null}", typeof(InvalidDataException))]
    [InlineData("{\"SnapshotBase64\":17}", typeof(InvalidOperationException))]
    [InlineData("{\"SnapshotBase64\":\"%%%\"}", typeof(FormatException))]
    [InlineData("{\"SnapshotBase64\":\"/w==\"}", typeof(InvalidProtocolBufferException))]
    public void StoppedCheckpointDecoderRejectsMalformedState(string state, Type exceptionType)
    {
        var checkpoint = new AuthoritativeSessionDocument(
            AuthoritativeSessionStore.CurrentFormatVersion, state, []);
        var failure = Record.Exception(() => ParseCheckpointSnapshot(checkpoint));
        Assert.NotNull(failure);
        Assert.True(exceptionType.IsAssignableFrom(failure.GetType()), failure.ToString());
    }

    [Fact]
    public void StoppedCheckpointDecoderPreservesExactPlayerState()
    {
        var snapshot = new Snapshot {
            Tick = 42,
            Players = { new PlayerSnapshot {
                EntityId = 17, CharacterClassId = 2, PositionX = 7, PositionY = 8,
            } },
        };
        var checkpoint = new AuthoritativeSessionDocument(
            AuthoritativeSessionStore.CurrentFormatVersion,
            JsonSerializer.Serialize(new { SnapshotBase64 = Convert.ToBase64String(snapshot.ToByteArray()) }),
            []);
        Assert.Equal(snapshot, ParseCheckpointSnapshot(checkpoint));
    }
}

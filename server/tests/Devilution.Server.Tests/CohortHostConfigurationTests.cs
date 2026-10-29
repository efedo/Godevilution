using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Cohort recovery is explicit and cannot silently consume legacy save roots. */
public sealed class CohortHostConfigurationTests
{
    [Fact]
    public void DefaultPersistenceRemainsLegacy()
    {
        Assert.True(ServerHostOptions.TryParse([], out var options, out var error), error);
        Assert.False(options.UseCohortPersistence);
    }

    [Theory]
    [InlineData("legacy", false)]
    [InlineData("cohort", true)]
    public void PersistenceModeIsExplicit(string value, bool expected)
    {
        Assert.True(ServerHostOptions.TryParse(["--persistence-mode", value], out var options, out var error), error);
        Assert.Equal(expected, options.UseCohortPersistence);
    }

    [Theory]
    [InlineData("")]
    [InlineData("COHORT")]
    [InlineData("merge")]
    public void UnknownPersistenceModeIsRejected(string value)
    {
        Assert.False(ServerHostOptions.TryParse(["--persistence-mode", value], out _, out var error));
        Assert.Contains("persistence mode", error);
    }

    [Fact]
    public void CohortCheckpointIntervalDefaultsToOneSecond()
    {
        Assert.True(ServerHostOptions.TryParse([], out var options, out var error), error);
        Assert.Equal(20U, options.CohortCheckpointIntervalTicks);
        Assert.True(ServerHostOptions.TryParse(["--cohort-checkpoint-interval", "1"], out options, out error), error);
        Assert.Equal(1U, options.CohortCheckpointIntervalTicks);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("ten")]
    [InlineData("4294967296")]
    public void InvalidCohortCheckpointIntervalIsRejected(string value)
    {
        Assert.False(ServerHostOptions.TryParse(["--cohort-checkpoint-interval", value], out _, out var error));
        Assert.Contains("cohort checkpoint interval", error);
    }

    [Fact]
    public void LegacyFilesAreRefusedWithoutMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godevilution-Cohort-Config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var path = Path.Combine(root, "legacy-session.json");
            byte[] original = [0x01, 0xA2, 0xFF];
            File.WriteAllBytes(path, original);
            using var store = new AuthoritativeCohortStore(root);
            Assert.Throws<InvalidDataException>(() => AuthoritativeTcpServer.ValidateCohortRootForStartup(store));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.False(File.Exists(store.CheckpointPath));
        } finally {
            // Only this explicitly constructed GUID-scoped test directory.
            Assert.False((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(root, recursive: true);
        }
    }
}

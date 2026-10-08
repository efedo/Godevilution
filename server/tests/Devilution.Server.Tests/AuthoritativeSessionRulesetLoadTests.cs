using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSessionRulesetLoadTests
{
    [Fact]
    public void StrictLoadAcceptsAnExactCurrentRulesetCheckpoint()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);
            var expectedReceipt = new CommandResult {
                ClientSequence = 7,
                ServerReceiptSequence = 19,
                AppliedTick = 23,
                Status = CommandStatus.Accepted,
                RejectReason = CommandRejectReason.Unspecified,
            };
            store.Save(
                "session-a",
                "current-state",
                new Dictionary<ulong, CommandResult> { [expectedReceipt.ClientSequence] = expectedReceipt },
                "ruleset-current");

            var document = store.LoadForRuleset("session-a", "ruleset-current");

            Assert.NotNull(document);
            Assert.Equal(AuthoritativeSessionStore.CurrentFormatVersion, document.FormatVersion);
            Assert.Equal("ruleset-current", document.RulesetIdentityHash);
            Assert.Equal("current-state", document.SerializedState);
            var actualReceipt = Assert.Single(document.CommandReceipts);
            Assert.Equal(expectedReceipt.ClientSequence, actualReceipt.ClientSequence);
            Assert.Equal(expectedReceipt.ServerReceiptSequence, actualReceipt.ServerReceiptSequence);
            Assert.Equal(expectedReceipt.AppliedTick, actualReceipt.AppliedTick);
            Assert.Equal(expectedReceipt.Status, actualReceipt.Status);
            Assert.Equal(expectedReceipt.RejectReason, actualReceipt.RejectReason);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictLoadReturnsNullWhenTheCombinedCheckpointIsAbsent()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);

            Assert.Null(store.LoadForRuleset("missing-session", "ruleset-current"));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StrictLoadRejectsLegacyFormatsWhileCompatibilityLoadStillAcceptsThem(int formatVersion)
    {
        var root = CreateOwnedRoot();
        try {
            WriteLegacyCheckpoint(root, formatVersion, "legacy-state");
            var store = new AuthoritativeSessionStore(root);

            var compatibilityDocument = store.Load("session-a", "ruleset-current");
            Assert.NotNull(compatibilityDocument);
            Assert.Equal(formatVersion, compatibilityDocument.FormatVersion);
            Assert.Null(compatibilityDocument.RulesetIdentityHash);

            Assert.Throws<InvalidDataException>(() =>
                store.LoadForRuleset("session-a", "ruleset-current"));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictLoadRejectsCurrentCheckpointWithoutRulesetIdentity()
    {
        var root = CreateOwnedRoot();
        try {
            WriteCurrentCheckpointWithoutIdentity(root, "state");
            var store = new AuthoritativeSessionStore(root);

            Assert.Throws<InvalidDataException>(() =>
                store.LoadForRuleset("session-a", "ruleset-current"));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictLoadRejectsCurrentCheckpointForAnotherRuleset()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "state", new Dictionary<ulong, CommandResult>(), "ruleset-old");

            var exception = Assert.Throws<InvalidDataException>(() =>
                store.LoadForRuleset("session-a", "ruleset-current"));

            Assert.Contains("ruleset identity", exception.Message, StringComparison.Ordinal);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictLoadRejectsRulesetIdentityMismatchByCaseOnly()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "state", new Dictionary<ulong, CommandResult>(), "Ruleset-Current");

            Assert.Throws<InvalidDataException>(() =>
                store.LoadForRuleset("session-a", "ruleset-current"));
        } finally {
            DeleteOwnedRoot(root);
        }
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StrictLoadRequiresANonemptyRulesetIdentity(string? requiredRulesetIdentityHash)
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);

            Assert.ThrowsAny<ArgumentException>(() =>
                store.LoadForRuleset("session-a", requiredRulesetIdentityHash!));
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    [Fact]
    public void StrictLoadReusesCurrentCheckpointIntegrityValidation()
    {
        var root = CreateOwnedRoot();
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "original-state", new Dictionary<ulong, CommandResult>(), "ruleset-current");
            var path = Path.Combine(root, "session-a.session.json");
            var json = File.ReadAllText(path);
            Assert.Contains("original-state", json, StringComparison.Ordinal);
            File.WriteAllText(path, json.Replace("original-state", "tampered-state", StringComparison.Ordinal));

            var exception = Assert.Throws<InvalidDataException>(() =>
                store.LoadForRuleset("session-a", "ruleset-current"));

            Assert.Contains("integrity hash", exception.Message, StringComparison.Ordinal);
        } finally {
            DeleteOwnedRoot(root);
        }
    }

    private static string CreateOwnedRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-ruleset-load-tests",
            Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteOwnedRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static void WriteLegacyCheckpoint(string root, int formatVersion, string state)
    {
        var stateHash = HashUtf8(state);
        var json = formatVersion switch {
            1 => "{\"FormatVersion\":1,\"SerializedState\":\"" + state + "\",\"CommandReceipts\":[]}",
            2 => "{\"FormatVersion\":2,\"SerializedState\":\"" + state + "\",\"CommandReceipts\":[]"
                + ",\"StateSha256\":\"" + stateHash + "\"}",
            3 => "{\"FormatVersion\":3,\"SerializedState\":\"" + state + "\",\"CommandReceipts\":[]"
                + ",\"StateSha256\":\"" + stateHash + "\",\"CommandReceiptsSha256\":\"" + HashUtf8("[]") + "\"}",
            _ => throw new ArgumentOutOfRangeException(nameof(formatVersion)),
        };
        WriteCheckpoint(root, json);
    }

    private static void WriteCurrentCheckpointWithoutIdentity(string root, string state)
    {
        var stateHash = HashUtf8(state);
        var json = "{\"FormatVersion\":4,\"SerializedState\":\"" + state + "\",\"CommandReceipts\":[]"
            + ",\"StateSha256\":\"" + stateHash + "\",\"CommandReceiptsSha256\":\"" + HashUtf8("[]") + "\"}";
        WriteCheckpoint(root, json);
    }

    private static void WriteCheckpoint(string root, string json)
    {
        var path = Path.Combine(root, "session-a.session.json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string HashUtf8(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
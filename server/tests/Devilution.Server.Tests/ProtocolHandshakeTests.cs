using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ProtocolHandshakeTests
{
    [Fact]
    public void MatchingIdentityIsAccepted()
    {
        var handshake = CreateHandshake();

        var result = handshake.Validate(new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
        });

        Assert.True(result.Accepted);
        Assert.Equal("server-build", result.ServerHello!.ServerBuildId);
        Assert.Equal(20U, result.ServerHello.TickRateHz);
        Assert.Equal(GameMode.SinglePlayer, result.ServerHello.GameMode);
    }

    [Fact]
    public void ProjectsExplicitMultiplayerMode()
    {
        var handshake = new ProtocolHandshake(
            new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20) {
                GameMode = AuthoritativeGameMode.Multiplayer,
            });

        var result = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
        });

        Assert.True(result.Accepted);
        Assert.Equal(GameMode.Multiplayer, result.ServerHello!.GameMode);
    }

    [Fact]
    public void SchemaMismatchIsRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello {
            ProtocolSchemaVersion = "0.2.0",
            ContentManifestHash = "content-hash",
        });

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.UnsupportedSchema, result.Error!.Code);
    }

    [Fact]
    public void ContentMismatchIsRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "different-content",
        });

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.ContentMismatch, result.Error!.Code);
    }

    [Fact]
    public void MissingIdentityFieldsAreRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello());

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, result.Error!.Code);
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("bad\nname")]
    [InlineData("bad\0name")]
    public void InvalidRequestedCharacterNamesAreRejected(string name)
    {
        var result = CreateHandshake().Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
            RequestedCharacterName = name,
        });

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, result.Error!.Code);
    }

    [Fact]
    public void ValidatesOptionalRulesetIdentityWithoutBreakingLegacyClients()
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20) {
            RulesetIdentityHash = "ruleset-hash",
        });

        var legacy = handshake.Validate(new ClientHello { ContentManifestHash = "content-hash", ProtocolSchemaVersion = "0.1.0" });
        var matching = handshake.Validate(new ClientHello { ContentManifestHash = "content-hash", ProtocolSchemaVersion = "0.1.0", RulesetIdentityHash = "ruleset-hash" });
        var mismatched = handshake.Validate(new ClientHello { ContentManifestHash = "content-hash", ProtocolSchemaVersion = "0.1.0", RulesetIdentityHash = "other" });

        Assert.True(legacy.Accepted);
        Assert.True(matching.Accepted);
        Assert.Equal(ProtocolErrorCode.ContentMismatch, mismatched.Error!.Code);
        Assert.Equal("ruleset-hash", matching.ServerHello!.RulesetIdentityHash);
    }

    [Fact]
    public void ProtocolV2RequiresTheIndependentContentAndRulesetIdentities()
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            "content-hash",
            20) {
            RulesetIdentityHash = "ruleset-hash",
        });

        var matching = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            ContentManifestHash = "content-hash",
            RulesetIdentityHash = "ruleset-hash",
        });
        var missingRuleset = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            ContentManifestHash = "content-hash",
        });
        var wrongRuleset = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            ContentManifestHash = "content-hash",
            RulesetIdentityHash = "other-ruleset",
        });

        Assert.True(matching.Accepted);
        Assert.False(missingRuleset.Accepted);
        Assert.False(wrongRuleset.Accepted);
        Assert.Equal(ProtocolErrorCode.ContentMismatch, wrongRuleset.Error!.Code);
    }

    [Fact]
    public void RulesetIdentityCanBeUpdatedAfterAHostMigration()
    {
        var handshake = CreateHandshake();
        handshake.UpdateIdentity(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "replacement-content",
            20) {
            RulesetIdentityHash = "replacement-ruleset",
        });

        var old = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
        });
        var replacement = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "replacement-content",
            RulesetIdentityHash = "replacement-ruleset",
        });

        Assert.Equal(ProtocolErrorCode.ContentMismatch, old.Error!.Code);
        Assert.True(replacement.Accepted);
        Assert.Equal("replacement-ruleset", handshake.Identity.RulesetIdentityHash);
    }

    [Fact]
    public void ExistingResumedSessionsCanRefreshAfterAnIdentityChange()
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            "replacement-content",
            20) {
            RulesetIdentityHash = "replacement-ruleset",
        });

        var result = handshake.Validate(new ClientHello {
            ProtocolSchemaVersion = ProtocolServerIdentity.IndependentIdentityProtocolSchemaVersion,
            ContentManifestHash = "previous-content",
            RulesetIdentityHash = "previous-ruleset",
            ResumeToken = "existing-session",
        }, allowResumedIdentityRefresh: true);

        Assert.True(result.Accepted);
        Assert.Equal("replacement-content", result.ServerHello!.ContentManifestHash);
        Assert.Equal("replacement-ruleset", result.ServerHello.RulesetIdentityHash);
    }

    private static ProtocolHandshake CreateHandshake()
    {
        return new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
    }
}

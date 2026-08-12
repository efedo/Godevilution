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

    private static ProtocolHandshake CreateHandshake()
    {
        return new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
    }
}

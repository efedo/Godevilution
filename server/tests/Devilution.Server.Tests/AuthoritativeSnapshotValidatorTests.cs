using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSnapshotValidatorTests
{
    [Fact]
    public void AcceptsLegacyOmittedAndCanonicalStateHashes()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot());
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            StateSha256 = new string('a', AuthoritativeSnapshotValidator.Sha256HexLength),
        });
    }

    [Fact]
    public void AcceptsDistinctEntityIdsAcrossProjectedCollections()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1 } },
            Monsters = { new MonsterSnapshot { EntityId = 2 } },
            WorldItems = { new WorldItemSnapshot { EntityId = 3 } },
            Objects = { new ObjectSnapshot { EntityId = 4 } },
            Projectiles = { new ProjectileSnapshot { EntityId = 5 } },
            Corpses = { new CorpseSnapshot { EntityId = 6 } },
        };

        AuthoritativeSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void RejectsMalformedNonEmptyStateHashes()
    {
        foreach (var hash in new[] {
            "not-a-hash",
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength + 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1) + "g",
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { StateSha256 = hash }));
    }

    [Fact]
    public void RejectsZeroOrDuplicateEntityIds()
    {
        var zero = new Snapshot {
            Players = { new PlayerSnapshot() },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(zero));

        var duplicate = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 7 } },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(duplicate));
    }
}

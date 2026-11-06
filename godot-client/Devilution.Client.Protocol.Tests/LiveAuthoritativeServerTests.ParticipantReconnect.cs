using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeParticipantReconnectTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task LiveServerCreatesRequestedClassesPerSessionAndResumeStateWins()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        string sorcererToken;
        await using (var sorcerer = new AuthoritativeClient(
            CreateOptions(server, "godot-sorcerer", requestedClassId: 2))) {
            await sorcerer.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                sorcerer,
                message => message.Snapshot is not null);
            var player = initial.Snapshot!.Players[0];
            Assert.Equal(2U, player.CharacterClassId);
            Assert.Equal(15, player.Attributes.Strength.Base);
            Assert.Equal(35, player.Attributes.Magic.Base);
            Assert.Equal(1920, player.LifeMaximum);
            Assert.Equal(4480, player.ManaMaximum);
            Assert.Equal(100U, player.Gold);
            Assert.Equal(27U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
            var firebolt = Assert.Single(player.MemorizedSpells);
            Assert.Equal(1U, firebolt.SpellId);
            Assert.Equal(2U, firebolt.Level);
            var staff = Assert.Single(player.Equipment);
            Assert.Equal(166, staff.State.ItemIndex);
            Assert.Equal(40, staff.State.Charges);
            Assert.Equal([0U, 1U], player.Belt.Select(item => item.Slot));
            Assert.All(player.Belt, item => Assert.Equal(6, item.State.MiscId));
            var gold = Assert.Single(player.Inventory);
            Assert.Equal(100U, gold.Price);
            Assert.Equal(11, gold.State.ItemType);
            Assert.Equal(
                Devilution.Protocol.V1.ItemPresentationKind.Gold,
                gold.State.PresentationKind);
            Assert.Equal(0, player.InventoryGrid[30]);
            sorcererToken = sorcerer.SessionToken;
        }

        await using (var rogue = new AuthoritativeClient(
            CreateOptions(server, "godot-rogue", requestedClassId: 1))) {
            await rogue.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                rogue,
                message => message.Snapshot is not null);
            Assert.Equal(2, initial.Snapshot!.Players.Count);
            var player = initial.Snapshot.Players[0];
            Assert.Equal(1U, player.CharacterClassId);
            Assert.Equal(20, player.Attributes.Strength.Base);
            Assert.Equal(30, player.Attributes.Dexterity.Base);
            Assert.Equal(2880, player.LifeMaximum);
            Assert.Equal(1440, player.ManaMaximum);
            Assert.Equal(100U, player.Gold);
            Assert.Equal(28U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.TrapDisarm, player.AbilityKind);
            Assert.Empty(player.MemorizedSpells);
            Assert.Equal(4, Assert.Single(player.Equipment).State.ItemIndex);
            Assert.Equal(2, player.Belt.Count);
        }

        await using (var resumed = new AuthoritativeClient(
            CreateOptions(server, "godot-resume", requestedClassId: 1) with {
                ResumeToken = sorcererToken,
            })) {
            await resumed.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                resumed,
                message => message.Snapshot is not null);
            Assert.Equal(2, initial.Snapshot!.Players.Count);
            var player = initial.Snapshot.Players[0];
            Assert.Equal(2U, player.CharacterClassId);
            Assert.Equal(27U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
            Assert.Equal(1U, Assert.Single(player.MemorizedSpells).SpellId);
            Assert.Equal(166, Assert.Single(player.Equipment).State.ItemIndex);
            Assert.Equal(2, player.Belt.Count);
        }

        await using var invalid = new AuthoritativeClient(
            CreateOptions(server, "godot-invalid-class", requestedClassId: 99));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            invalid.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.Contains("class ID 99", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveClientReconnectsThroughEightCyclesWithSecondParticipantConnected()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var first = new AuthoritativeClient(
            CreateOptions(server, "godot-live-reconnect-first", requestedClassId: 2));
        await first.ConnectAsync(TestContext.Current.CancellationToken);
        var firstInitial = await WaitForMessageAsync(
            first,
            message => message.Snapshot is not null);
        var firstEntityId = Assert.Single(firstInitial.Snapshot!.Players).EntityId;
        var sessionToken = first.SessionToken;

        await using var second = new AuthoritativeClient(
            CreateOptions(server, "godot-live-reconnect-second", requestedClassId: 1));
        await second.ConnectAsync(TestContext.Current.CancellationToken);
        var secondInitial = await WaitForMessageAsync(
            second,
            message => message.Snapshot is not null);
        var secondEntityId = secondInitial.Snapshot!.Players[0].EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);
        Assert.Equal(
            [secondEntityId, firstEntityId],
            secondInitial.Snapshot.Players.Select(player => player.EntityId));

        const int reconnectCycles = 8;
        for (var cycle = 0; cycle < reconnectCycles; cycle++) {
            await first.ReconnectAsync(TestContext.Current.CancellationToken);
            Assert.Equal(sessionToken, first.SessionToken);

            var firstResumed = await WaitForMessageAsync(
                first,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [firstEntityId, secondEntityId],
                firstResumed.Snapshot!.Players.Select(player => player.EntityId));
            Assert.NotEmpty(firstResumed.Snapshot.StateSha256);
            Assert.True(second.IsConnected);

            await second.PollAsync(TestContext.Current.CancellationToken);
            var secondResumed = await WaitForMessageAsync(
                second,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [secondEntityId, firstEntityId],
                secondResumed.Snapshot!.Players.Select(player => player.EntityId));
            Assert.NotEmpty(secondResumed.Snapshot.StateSha256);
        }
    }

    [Fact]
    public async Task LiveServerUsesOverlaySelectedDefaultPlayerClass()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Class-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.class-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "classes.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            var classTablePath = Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv");
            var classLines = await File.ReadAllLinesAsync(
                classTablePath,
                TestContext.Current.CancellationToken);
            var warrior = SetDefaultClass(
                classLines.Single(line => line.StartsWith("0\t", StringComparison.Ordinal)),
                isDefault: false);
            var monk = SetDefaultClass(
                classLines.Single(line => line.StartsWith("3\t", StringComparison.Ordinal)),
                isDefault: true);
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "classes.tsv"),
                [classLines[0], warrior, monk],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot);
            await using var client = new AuthoritativeClient(new ClientConnectionOptions(
                "127.0.0.1",
                server.Port,
                "godot-class-overlay-test",
                ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                server.ContentHash,
                server.RulesetHash));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var player = Assert.Single(initial.Snapshot!.Players);

            Assert.Equal(3U, player.CharacterClassId);
            Assert.Equal(25, player.Attributes.Strength.Base);
            Assert.Equal(15, player.Attributes.Magic.Base);
            Assert.Equal(25, player.Attributes.Dexterity.Base);
            Assert.Equal(20, player.Attributes.Vitality.Base);
            Assert.Equal(2880, player.LifeMaximum);
            Assert.Equal(1440, player.ManaMaximum);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }
}

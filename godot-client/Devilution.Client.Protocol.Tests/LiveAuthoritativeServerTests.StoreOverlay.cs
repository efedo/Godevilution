using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Trait("Category", "Slow")]
public sealed class LiveAuthoritativeStoreOverlayTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task LiveServerAppliesDeclaredContentOverlayToAuthoritativeStore()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Content-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.store-overlay",
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
                      "table": "stores.tsv",
                      "keys": ["store_id", "store_slot"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "stores.tsv"),
                "store_id\tstore_symbol\tstore_role\tstore_slot\titem_seed\tprice\titem_id\titem_symbol\n1\tsmith\tSmith\t0\t42\t777\t1\tshort_sword\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLevelOverride: 0);
            await using var client = new AuthoritativeClient(new ClientConnectionOptions(
                "127.0.0.1",
                server.Port,
                "godot-overlay-test",
                ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                server.ContentHash,
                server.RulesetHash));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            await MoveToProjectedTownerAsync(client, model, 0);
            var sequence = client.Queue(new Command {
                OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            var message = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.ActiveStore?.StoreId == 1);

            Assert.Equal(777U, message.Snapshot!.ActiveStore!.Items[0].Price);
            Assert.Equal(25U, message.Snapshot.ActiveStore.Items[1].Price);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }
}

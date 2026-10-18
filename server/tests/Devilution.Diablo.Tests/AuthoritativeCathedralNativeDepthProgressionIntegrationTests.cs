using Devilution.Data;
using Devilution.Server.Stores;
using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeDepthProgressionV10RetainsFourVisitedFloorOwnersAndFreshContinuation()
    {
        var preparedSetup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(preparedSetup);
        using var setup = preparedSetup!;

        var initialActivation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup.Fixture.Content, 0x13579BDF);
        var footprintSourcePath = "native_cathedral_runtime/native-rogue-l1-inventory.tsv";
        var footprintTable = setup.Fixture.Content.GetRequiredTable(footprintSourcePath);
        var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(TsvTable.Parse(
            "native-rogue-l1-inventory.tsv", footprintTable.ToCanonicalTsv()));
        var expandedSemantics = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            setup.Fixture.PopulationMetadata.Items.Catalog,
            setup.Fixture.BaselineItems,
            initialActivation.Simulation.Spells!,
            footprints,
            includeNativeJewelry: true,
            includeNativeDepthItems: true);
        var originalSemantics = setup.Fixture.Content.GetRequiredTable(
            "native_cathedral_runtime/native_item_semantics.tsv");
        var expectedAddedIndexes = new[] { 82, 100, 101, 109, 112, 113 };
        var originalIndexes = originalSemantics.Rows
            .Select(row => row.RequiredInt32("native_item_index")).ToHashSet();
        var addedIndexes = expandedSemantics.Rows
            .Select(row => row.RequiredInt32("native_item_index"))
            .Where(index => !originalIndexes.Contains(index))
            .Order()
            .ToArray();
        Assert.Equal(expectedAddedIndexes, addedIndexes);
        var originalRows = originalSemantics.Rows
            .Select(row => string.Join('\t', originalSemantics.Columns.Select(column => column == "item_action_kind"
                    && row.RequiredInt32("native_item_index") is 27 or 91 or 98 or 102 ? "None" : row.Required(column))))
            .ToArray();
        var expandedLegacyRows = expandedSemantics.Rows
            .Where(row => !expectedAddedIndexes.Contains(row.RequiredInt32("native_item_index")))
            .Select(row => string.Join('\t', expandedSemantics.Columns.Select(column => row.Required(column))))
            .ToArray();
        Assert.Equal(originalRows, expandedLegacyRows);
        var fullRejuvenation = Assert.Single(expandedSemantics.Rows,
            row => row.RequiredInt32("native_item_index") == 82);
        Assert.Equal("potion_full_rejuvenation_standard", fullRejuvenation.Required("stable_item_symbol"));
        Assert.Equal(20, fullRejuvenation.RequiredInt32("misc_id"));
        Assert.Equal("RestoreFullLifeAndMana", fullRejuvenation.Required("item_action_kind"));
        var nativeContent = ReplaceTable(setup.Fixture.Content,
            "native_cathedral_runtime/native_item_semantics.tsv",
            expandedSemantics with { SourcePath = "native_cathedral_runtime/native_item_semantics.tsv" });
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(nativeContent, 0x13579BDF);
        var simulation = activation.Simulation;
        var expectedNativeSpellMinimumMagic = new Dictionary<int, int> {
            [27] = 0,
            [91] = 0,
            [98] = 0,
            [100] = 23,
            [101] = 25,
            [102] = 0,
            [109] = 57,
            [112] = 81,
            [113] = 117,
        };
        foreach (var nativeIndex in expectedNativeSpellMinimumMagic.Keys.Order()) {
            var row = Assert.Single(expandedSemantics.Rows,
                value => value.RequiredInt32("native_item_index") == nativeIndex);
            Assert.Equal("None", row.Required("item_action_kind"));
            var definition = Assert.Single(simulation.Items!.Definitions,
                value => value.StableSymbol == row.Required("stable_item_symbol"));
            Assert.Equal(DiabloItemSpellSourceKind.ConsumableItem, definition.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, definition.ItemActionKind);
            Assert.Equal(expectedNativeSpellMinimumMagic[nativeIndex], definition.MinimumMagic);
        }
        Assert.Equal(23, simulation.Items!.GetRequired(2121).MinimumMagic);
        Assert.Equal(25, simulation.Items.GetRequired(2122).MinimumMagic);
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var firstStair = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-four-floor", rogue.ClassId,
            null, null, null, startingLevelId: 1,
            startingPositionX: firstStair.TriggerX, startingPositionY: firstStair.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-four-floor", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-four-floor", value => value);
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;
        var codec = new DiabloCohortCodec();

        for (uint sourceLevel = 1; sourceLevel < 4; sourceLevel++) {
            var stair = world.World.WarpTargets.Single(row => row.SourceLevelId == sourceLevel
                && row.TargetLevelId == sourceLevel + 1);
            player.PositionX = stair.TriggerX;
            player.PositionY = stair.TriggerY;

            Assert.False(world.OwnsNativePopulationLevel(sourceLevel + 1));
            var beforeDryRun = codec.Serialize(simulation.CaptureCohortState(runtime));
            var dryRun = commands.ExecuteLevelTrigger(player, 0, [player], applyEffects: false);
            Assert.True(dryRun.Succeeded);
            Assert.Equal(beforeDryRun, codec.Serialize(simulation.CaptureCohortState(runtime)));
            Assert.Equal(sourceLevel, player.LevelId);
            Assert.False(world.OwnsNativePopulationLevel(sourceLevel + 1));

            if (sourceLevel == 1) {
                var prepared = world.PrepareNativeProgressionEntry(player, 2, [player],
                    simulation.Items!, simulation.Spells!)!.Value;
                var foreign = world.CombatTargets.Values.First();
                prepared.Admission.Floor.CombatTargets.Add(foreign.EntityId, foreign);
                var beforeRefusal = codec.Serialize(simulation.CaptureCohortState(runtime));
                Assert.Throws<InvalidOperationException>(() => world.CommitNativeProgressionEntry(
                    prepared.Admission, prepared.Entry));
                Assert.Equal(beforeRefusal, codec.Serialize(simulation.CaptureCohortState(runtime)));
                prepared.Admission.Floor.CombatTargets.Remove(foreign.EntityId);
            }

            var entered = commands.ExecuteLevelTrigger(player, 0, [player]);
            Assert.True(entered.Succeeded);
            Assert.Equal(sourceLevel + 1, player.LevelId);
            Assert.True(world.OwnsNativePopulationLevel(sourceLevel));
            Assert.True(world.OwnsNativePopulationLevel(sourceLevel + 1));
        }

        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeDepthProgressionFormatVersion, captured.FormatVersion);
        Assert.Equal(new uint[] { 2, 3, 4 }, captured.NativeProgression!.Entries.Select(entry => entry.LevelId));
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, captured.CathedralItems!.Levels.Select(level => level.LevelId));
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, captured.CathedralDoors!.DoorLevels.Select(level => level.LevelId));

        var bytes = codec.Serialize(captured);
        var decoded = codec.Deserialize(bytes);
        Assert.Throws<InvalidDataException>(() => codec.Serialize(captured with {
            NativeProgression = captured.NativeProgression with {
                Entries = [captured.NativeProgression.Entries[1], captured.NativeProgression.Entries[2]],
            },
        }));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(captured with {
            FormatVersion = DiabloCohortState.NativeProgressionFormatVersion,
        }));
        var candidate = activation.CohortCandidateFactory!(decoded);
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));

        var beforeTick = simulation.CaptureCohortState(runtime);
        var offLevelActors = JsonSerializer.Serialize(beforeTick.CombatTargets
            .Where(target => target.Monster.LevelId < 4).ToArray());
        var offLevelObjects = JsonSerializer.Serialize(world.Objects.Values
            .Where(value => value.LevelId < 4).Select(DiabloSimulation.CaptureObject).ToArray());
        var nextTick = checked(runtime.LastAdvancedTick + 1);
        runtime.AdvanceTo(nextTick);
        restoredRuntime.AdvanceTo(nextTick);
        var continued = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.Equal(continued, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(offLevelActors, JsonSerializer.Serialize(simulation.CaptureCohortState(runtime)
            .CombatTargets.Where(target => target.Monster.LevelId < 4).ToArray()));
        Assert.Equal(offLevelObjects, JsonSerializer.Serialize(world.Objects.Values
            .Where(value => value.LevelId < 4).Select(DiabloSimulation.CaptureObject).ToArray()));
    }

    private static ComposedContent ReplaceTable(ComposedContent content, string sourcePath, TsvTable replacement)
    {
        var manifest = content.Manifest;
        var packs = manifest.Packs.Select(pack => new ContentPack(
            pack.Id,
            pack.Version,
            pack.Tables.Select(table => string.Equals(table.SourcePath, sourcePath, StringComparison.Ordinal)
                ? replacement
                : table).ToArray(),
            pack.Dependencies,
            pack.Overlays,
            pack.Schema,
            pack.DependencyConstraints,
            pack.Assets)).ToArray();
        return ComposedContent.Compose(new ContentManifest(manifest.Id, manifest.Version, packs));
    }
}

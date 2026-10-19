using Devilution.Data;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Theory(Explicit = true)]
    [InlineData(0U, DiabloPlayerAbilityKind.Repair)]
    [InlineData(2U, DiabloPlayerAbilityKind.Recharge)]
    public void ClassBoundNativeDepthProgressionRetainsClassAcrossFreshFloorRestore(
        uint classId,
        DiabloPlayerAbilityKind expectedAbility)
    {
        var preparedSetup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(preparedSetup);
        using var setup = preparedSetup!;

        var footprintTable = setup.Fixture.Content.GetRequiredTable(
            "native_cathedral_runtime/native-rogue-l1-inventory.tsv");
        var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(TsvTable.Parse(
            "native-rogue-l1-inventory.tsv", footprintTable.ToCanonicalTsv()));
        var depthSemantics = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            setup.Fixture.PopulationMetadata.Items.Catalog,
            setup.Activation.Simulation.Items!,
            setup.Activation.Simulation.Spells!,
            footprints,
            includeNativeJewelry: true,
            includeNativeDepthItems: true);
        var depthContent = ReplaceTable(setup.Fixture.Content,
            "native_cathedral_runtime/native_item_semantics.tsv",
            depthSemantics with { SourcePath = "native_cathedral_runtime/native_item_semantics.tsv" });
        var classContent = AddNativeCharacterStartTable(setup.Activation, depthContent, classId);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(classContent, 0x13579BDF);
        Assert.NotNull(activation.NativeCharacterStart);
        Assert.True(activation.NativeCharacterStart!.HasCompiledTable);
        Assert.Equal(classId, activation.NativeCharacterStart.ClassId);
        Assert.Equal(classId == 0 ? "warrior" : "sorcerer", activation.NativeCharacterStart.ClassSymbol);
        Assert.Equal(classId, activation.TrustedOptions!.NativePopulationClassId);

        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-class-progression", classId, null, null, null, startingLevelId: 1,
            startingPositionX: activation.Simulation.WorldState.World.GetStartPosition(1).X,
            startingPositionY: activation.Simulation.WorldState.World.GetStartPosition(1).Y);
        var player = template.CreatePlayer();
        Assert.Equal(expectedAbility, player.AbilityKind);
        if (classId == 2)
            Assert.Equal(new MemorizedSpellState(1, 2), Assert.Single(player.MemorizedSpells));
        else
            Assert.Empty(player.MemorizedSpells);

        var simulation = activation.Simulation;
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-class-progression", state => state.EntityId = 0x70000001);
        var mismatchActivation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(classContent, 0x13579BDF);
        var rogue = activation.PlayerLoadouts.CreateTemplate(
            "native-class-mismatch", 1, null, null, null, startingLevelId: 1,
            startingPositionX: template.PositionX, startingPositionY: template.PositionY);
        var mismatchRuntime = mismatchActivation.Simulation.CreateRuntime(rogue);
        mismatchRuntime.Write("native-class-mismatch", state => state.EntityId = 0x70000002);
        Assert.Throws<InvalidDataException>(() => mismatchActivation.Simulation.CaptureCohortState(mismatchRuntime));

        var world = simulation.WorldState;
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;
        var state = runtime.Read("native-class-progression", value => value);
        var target = world.CombatTargets.Values.First(value => value.LevelId == 1 && value.HitPoints > 0);
        var offsets = new[] { (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1) };
        var adjacentCells = offsets.Select(offset => (X: target.PositionX + offset.X, Y: target.PositionY + offset.Y))
            .Where(position => world.IsWalkable(1, position.X, position.Y)
                && !world.IsOccupied(1, position.X, position.Y, [state], target.EntityId))
            .ToArray();
        Assert.NotEmpty(adjacentCells);
        var adjacent = adjacentCells[0];
        state.PositionX = adjacent.X;
        state.PositionY = adjacent.Y;
        var attack = simulation.Execute(state, new DiabloAttackIntent(target.EntityId), 1, [state]);
        Assert.True(attack.Succeeded, $"The class-bound participant could not use ordinary melee combat: {attack.Failure}.");

        for (uint sourceLevel = 1; sourceLevel <= 2; sourceLevel++) {
            var stair = world.World.WarpTargets.Single(row => row.SourceLevelId == sourceLevel
                && row.TargetLevelId == sourceLevel + 1);
            state.PositionX = stair.TriggerX;
            state.PositionY = stair.TriggerY;
            Assert.True(commands.ExecuteLevelTrigger(state, 0, [state]).Succeeded);
            Assert.Equal(sourceLevel + 1, state.LevelId);
        }

        var codec = new DiabloCohortCodec();
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(new uint[] { 2, 3 }, captured.NativeProgression!.Entries.Select(entry => entry.LevelId));
        Assert.Equal(new uint[] { 1, 2, 3 }, captured.CathedralItems!.Levels.Select(level => level.LevelId));
        var bytes = codec.Serialize(captured);
        var capturedParticipant = Assert.Single(captured.Participants);
        var rogueClass = activation.PlayerClasses.GetRequired(1);
        var rogueParticipant = capturedParticipant with {
            Player = capturedParticipant.Player with { CharacterClassId = rogueClass.ClassId },
            PlayerClassDefinition = rogueClass,
        };
        Assert.Throws<InvalidDataException>(() => activation.CohortCandidateFactory!(captured with {
            Participants = [rogueParticipant],
        }));
        Assert.Equal(bytes, codec.Serialize(simulation.CaptureCohortState(runtime)));

        var otherClassId = classId == 0 ? 2U : 0U;
        var otherClassContent = AddNativeCharacterStartTable(activation, depthContent, otherClassId);
        var otherClassActivation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(otherClassContent, 0x13579BDF);
        Assert.Throws<InvalidDataException>(() => otherClassActivation.CohortCandidateFactory!(codec.Deserialize(bytes)));
        Assert.Equal(bytes, codec.Serialize(simulation.CaptureCohortState(runtime)));

        var candidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(classId, candidate.Simulation.CaptureCohortState(restoredRuntime)
            .Participants.Single().Player.CharacterClassId);
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));

        var nextTick = checked(runtime.LastAdvancedTick + 1);
        runtime.AdvanceTo(nextTick);
        restoredRuntime.AdvanceTo(nextTick);
        Assert.Equal(
            codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.True(candidate.Simulation.WorldState.OwnsNativePopulationLevel(1));
        Assert.True(candidate.Simulation.WorldState.OwnsNativePopulationLevel(2));
        Assert.True(candidate.Simulation.WorldState.OwnsNativePopulationLevel(3));
    }

    private static ComposedContent AddNativeCharacterStartTable(
        DiabloContentActivation activation,
        ComposedContent content,
        uint classId)
    {
        var playerClass = activation.PlayerClasses.GetRequired(classId);
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-cathedral-population-profile", classId, null, null, null, 1,
            startingPositionX: 57, startingPositionY: 40);
        var player = template.CreatePlayer();
        var idleFrames = activation.PlayerAnimations.GetRequired(classId).IdleFrames;
        var table = TsvTable.Parse(
            "native_cathedral_runtime/" + AuthoritativeCathedralNativeCharacterStart.TableName,
            "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n"
                + $"{classId}\t{playerClass.Symbol}\t{template.BaseLifeMaximum}\t{template.BaseManaMaximum}\t{player.LightRadius}\t{idleFrames}\n");
        var runtimePath = "native_cathedral_runtime/native-rogue-l1-inventory.tsv";
        var manifest = content.Manifest;
        var runtimePacks = manifest.Packs.Where(pack => pack.Tables.Any(candidate =>
            string.Equals(candidate.SourcePath, runtimePath, StringComparison.Ordinal))).ToArray();
        if (runtimePacks.Length != 1)
            throw new InvalidDataException("The native fixture must contain exactly one runtime sidecar pack.");
        var runtimePack = runtimePacks[0];
        var packs = manifest.Packs.Select(pack => ReferenceEquals(pack, runtimePack)
            ? new ContentPack(pack.Id, pack.Version, pack.Tables.Where(candidate =>
                    !string.Equals(candidate.SourcePath, table.SourcePath, StringComparison.Ordinal))
                .Append(table).ToArray(), pack.Dependencies,
                pack.Overlays, pack.Schema, pack.DependencyConstraints, pack.Assets)
            : pack).ToArray();
        return ComposedContent.Compose(new ContentManifest(manifest.Id, manifest.Version, packs));
    }
}

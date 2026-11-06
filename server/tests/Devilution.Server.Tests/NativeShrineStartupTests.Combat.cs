using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineStartupTests
{
    [Fact]
    public void MysteriousWarriorL8ChangesTheNativeStatsAndOnlyTwoHitPointsOfMaximumLife()
    {
        var fixture = new Devilution.Tests.NativeShrineTestFixture(5, 0, seed: 0);
        var hero = CreateWarriorL8();
        hero.LevelId = 5; hero.PositionX = 50; hero.PositionY = 51;
        Assert.Equal((80, 10, 30, 65), (hero.Attributes.Strength.Base, hero.Attributes.Magic.Base,
            hero.Attributes.Dexterity.Base, hero.Attributes.Vitality.Base));
        Assert.Equal((11776, 1728), (hero.LifeMaximum, hero.ManaMaximum));
        var beforeEquipment = hero.Equipment.ToArray();
        var beforeBelt = hero.Belt.ToArray();
        Assert.True(fixture.Simulation.Execute(hero,
            new Devilution.Diablo.DiabloOperateObjectIntent(Devilution.Tests.NativeShrineTestFixture.ShrineEntityId), 0, [hero]).Succeeded);
        // Native OperateShrineMysterious: all stats -1, selected Strength +6; Warrior vitality is 128 Q6 and magic is 64 Q6.
        Assert.Equal((85, 9, 29, 64), (hero.Attributes.Strength.Base, hero.Attributes.Magic.Base,
            hero.Attributes.Dexterity.Base, hero.Attributes.Vitality.Base));
        Assert.Equal((11648, 1664), (hero.LifeMaximum, hero.ManaMaximum));
        Assert.Equal(beforeEquipment, hero.Equipment);
        Assert.Equal(beforeBelt, hero.Belt);
    }

    [Fact(Explicit = true)]
    public void SavedL7CombatExplainsPotionAdmissionAndDamage()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SHRINE_COMBAT_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile)) Assert.Skip("Select the private failed L7 session and profile.");
        var directory = Directory.GetDirectories(Path.Combine(session!, "save-command-logs")).Single();
        var records = CommandLogReader.ReadAll(Path.Combine(directory, HostCommandLog.LogFileName));
        var finalMarker = records.Last(value => value.Checkpoint is not null).Checkpoint;
        var end = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, finalMarker.FileName)));
        var potionFailure = records.SingleOrDefault(value => value.ServerCommand?.Command.ClientSequence == 286
            && value.ServerCommand.Result.Status == CommandStatus.Rejected)?.ServerCommand;
        var observationStart = potionFailure is null ? 43900UL : 33934UL;
        var marker = records.Select((value, index) => (value, index))
            .Last(value => value.value.Checkpoint is { } checkpoint && checkpoint.Tick < observationStart
                && File.Exists(Path.Combine(directory, checkpoint.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, marker.value.Checkpoint.FileName)));
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory("devilution.diablo.cathedral-native-all-class-town.v1");
        var start = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 0, GameMode = AuthoritativeGameMode.SinglePlayer, StartingLevelId = 0,
            TestParticipantDefinition = File.ReadAllText(Path.Combine(FindRepository(), "tools/native-test-heroes/warrior-l8.json")),
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(manifest,
            factory.CreateBuiltInModules(), new TestClock(), (content, registry) => factory.Activate(registry, content, start));
        var adapter = runtime.Active.Target;
        using var prepared = ((IAuthoritativeGameCohortAdapter)adapter).PrepareCohortRestore(new(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        var commands = new AuthoritativeCommandServer(adapter, CommandAdmissionPolicy.ForLiveHost(end.Identity.TickRateHz));
        commands.InitializeCohortState(new(document.Tick, document.NextServerReceiptSequence,
            document.Participants.Select(value => new AuthoritativeSessionCommandLedger(value.SessionId, value.CommandReceipts) {
                RetiredThroughClientSequence = value.RetiredThroughClientSequence,
            }).ToArray()));
        ((IAuthoritativeGameCohortAdapter)adapter).CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        var loggedCommands = records.Skip(marker.index + 1).Where(value => value.ServerCommand is not null)
            .Select(value => value.ServerCommand).ToLookup(value => value.CurrentTick);
        var damage = new List<(ulong Tick, uint Source, int Amount)>();
        var sources = new Dictionary<uint, MonsterSnapshot>();
        ulong deathTick = 0;
        for (var tick = document.Tick; tick <= end.Tick; tick++) {
            commands.AdvanceTo(tick);
            if (tick == observationStart || tick == 33985 || tick == potionFailure?.CurrentTick || tick == end.Tick
                || potionFailure is null && tick == 43993) {
                var snapshot = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, tick);
                foreach (var monster in snapshot.Monsters) sources[monster.EntityId] = monster;
                var player = Assert.Single(snapshot.Players);
                output.WriteLine($"STATE tick={tick}, life={player.Life}/{player.LifeMaximum}, mode={player.CombatMode}, action={player.CurrentAction}, position={player.PositionX},{player.PositionY}, attrs={player.Attributes}");
                var healingSupply = player.Inventory.Select(value => value.State).Concat(player.Belt.Select(value => value.State))
                    .Count(value => value.ItemActionKind is ItemActionKind.RestorePartialLife or ItemActionKind.RestoreFullLife
                        or ItemActionKind.RestorePartialLifeAndMana or ItemActionKind.RestoreFullLifeAndMana);
                output.WriteLine($"SUPPLY tick={tick}, healing={healingSupply}");
                if (potionFailure is null && tick == 43993) Assert.Equal(0, healingSupply);
                foreach (var equipment in player.Equipment)
                    output.WriteLine($"GEAR slot={equipment.Slot}, name={equipment.State.IdentifiedName}, durability={equipment.State.Durability}/{equipment.State.MaxDurability}, stat={equipment.State.StatFlag}, armor={equipment.State.ArmorClass}");
                if (potionFailure is not null && tick == potionFailure.CurrentTick) {
                    Assert.Equal(0, player.Life);
                    Assert.Equal(PlayerCombatMode.Death, player.CombatMode);
                    Assert.Equal(34009UL, player.CurrentAction.StartedAtTick);
                }
                foreach (var item in player.Belt.Where(value => value.ItemSeed == 1765365475))
                    output.WriteLine($"POTION {item}");
                foreach (var missile in snapshot.Projectiles.Where(value => value.LevelId == 7))
                    output.WriteLine($"MISSILE tick={tick}: {missile}");
            }
            var events = adapter.DrainEvents(participant.SessionId, participant.EntityId, tick);
            if (tick > observationStart && events is not null) {
                foreach (var hit in events.Events.Where(value => value.Damage?.TargetEntityId == participant.EntityId).Select(value => value.Damage)) {
                    damage.Add((tick, hit.SourceEntityId, hit.AmountQ6));
                    output.WriteLine($"DAMAGE tick={tick}, source={hit.SourceEntityId}, q6={hit.AmountQ6}");
                    if (deathTick == 0) {
                        var affected = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, tick).Players[0];
                        if (affected.Life == 0) {
                            deathTick = tick;
                            output.WriteLine($"DEATH tick={tick}, source={affected.CurrentAction?.TargetEntityId ?? hit.SourceEntityId}");
                        }
                    }
                }
            }
            foreach (var logged in loggedCommands[tick]) {
                var result = commands.Process(logged.SessionId, logged.Command.Clone(), tick);
                Assert.Equal(logged.Result, result);
                output.WriteLine($"COMMAND seq={logged.Command.ClientSequence} tick={tick}: {logged.Command}; {result.Status}/{result.RejectReason}");
            }
        }
        foreach (var group in damage.GroupBy(value => value.Source)) {
            sources.TryGetValue(group.Key, out var monster);
            output.WriteLine($"SOURCE {group.Key}, hits={group.Count()}, totalQ6={group.Sum(value => value.Amount)}, type={monster?.MonsterId}, AI={monster?.NativeAi}, min={monster?.MinDamage}, max={monster?.MaxDamage}, minSpecial={monster?.MinDamageSpecial}, maxSpecial={monster?.MaxDamageSpecial}");
        }
        if (potionFailure is not null) {
            var window = damage.Where(value => value.Tick <= 33985).ToArray();
            Assert.Equal(3079, window.Sum(value => value.Amount));
            Assert.Equal(25, window.Count(value => value.Amount == 64));
            Assert.Equal(new[] { 936, 543 }, window.Where(value => value.Amount != 64).Select(value => value.Amount));
            Assert.All(window, value => Assert.Equal((43U, "Bat"), (sources[value.Source].MonsterId, sources[value.Source].NativeAi)));
            Assert.Equal(34009UL, deathTick);
            Assert.Equal(CommandRejectReason.NotAllowed, potionFailure.Result.RejectReason);
        }
        Assert.True(string.Equals(end.SerializedModuleState,
            ((IAuthoritativeGameCohortAdapter)adapter).CaptureCohortState().SerializedState, StringComparison.Ordinal),
            "Tick-by-tick replay changed the final saved module bytes.");
        Assert.NotEqual(0UL, deathTick);
        var last = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, end.Tick);
        Assert.Equal(0, last.Players[0].Life);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PerformanceResumeStateContractTests
{
    [Fact]
    public void ReadsVersionedCredentialFreeBaselineAndRejectsExtraFields()
    {
        var baseline = CreateBaseline();
        var json = JsonSerializer.Serialize(baseline);

        Assert.Equal(baseline, PerformanceResumeStateContract.ParseBaseline(json));

        var node = JsonNode.Parse(json)!.AsObject();
        node["ResumeToken"] = "must-not-be-accepted";
        Assert.Throws<InvalidDataException>(
            () => PerformanceResumeStateContract.ParseBaseline(node.ToJsonString()));
    }

    [Fact]
    public void RejectsMissingDuplicateAndWronglyTypedFields()
    {
        var json = JsonSerializer.Serialize(CreateBaseline());
        var missing = JsonNode.Parse(json)!.AsObject();
        missing.Remove(nameof(PerformanceResumeBaseline.Passed));
        Assert.Throws<InvalidDataException>(
            () => PerformanceResumeStateContract.ParseBaseline(missing.ToJsonString()));

        var missingConnection = JsonNode.Parse(json)!.AsObject();
        missingConnection.Remove(nameof(PerformanceResumeBaseline.Connection));
        Assert.Throws<InvalidDataException>(
            () => PerformanceResumeStateContract.ParseBaseline(missingConnection.ToJsonString()));

        var extraConnection = JsonNode.Parse(json)!.AsObject();
        extraConnection[nameof(PerformanceResumeBaseline.Connection)]!["Unexpected"] = true;
        Assert.Throws<InvalidDataException>(
            () => PerformanceResumeStateContract.ParseBaseline(extraConnection.ToJsonString()));

        var duplicate = json[..^1] + ",\"Passed\":true}";
        Assert.Throws<InvalidDataException>(
            () => PerformanceResumeStateContract.ParseBaseline(duplicate));

        var wrongType = json.Replace("\"SchemaVersion\":2", "\"SchemaVersion\":\"2\"", StringComparison.Ordinal);
        Assert.Throws<JsonException>(
            () => PerformanceResumeStateContract.ParseBaseline(wrongType));
    }

    [Fact]
    public void RejectsWrongPhaseIdentityAndUnsettledBaselineState()
    {
        var baseline = CreateBaseline();
        var invalid = new[] {
            baseline with { Phase = "Performance" },
            baseline with { Identity = baseline.Identity with { GameMode = GameMode.Multiplayer.ToString() } },
            baseline with { Identity = baseline.Identity with { RulesetIdentityHash = "bad" } },
            baseline with { Identity = baseline.Identity with { CommandSchedulingMode = CommandSchedulingMode.Unspecified.ToString() } },
            baseline with { Identity = baseline.Identity with { CommandSchedulingMode = CommandSchedulingMode.Immediate.ToString() } },
            baseline with { Connection = baseline.Connection with { AttemptEpoch = 0 } },
            baseline with { Connection = baseline.Connection with { AdmissionCompleted = false } },
            baseline with { Connection = baseline.Connection with { TransportConnected = false } },
            baseline with { Player = baseline.Player with { HostileMode = true } },
            baseline with { Player = baseline.Player with { CurrentActionPresent = true } },
            baseline with { Player = baseline.Player with { CombatMode = PlayerCombatMode.WalkSouthwards.ToString() } },
            baseline with { Player = baseline.Player with { CharacterClassId = 2 } },
        };

        foreach (var candidate in invalid)
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateBaseline(candidate));
    }

    [Fact]
    public void RejectsZeroOverflowAndUnacceptedCreateCursors()
    {
        var baseline = CreateBaseline();
        var invalid = new[] {
            baseline with { LastCommand = baseline.LastCommand with { ClientSequence = 0 } },
            baseline with { LastCommand = baseline.LastCommand with { ClientSequence = ulong.MaxValue - 1 } },
            baseline with { LastCommand = baseline.LastCommand with { ServerReceiptSequence = 0 } },
            baseline with { LastCommand = baseline.LastCommand with { AppliedTick = baseline.SnapshotTick + 1 } },
            baseline with { LastCommand = baseline.LastCommand with { Status = CommandStatus.Unspecified.ToString() } },
            baseline with { LastCommand = baseline.LastCommand with { Status = CommandStatus.Rejected.ToString() } },
            baseline with { LastCommand = baseline.LastCommand with { RejectReason = CommandRejectReason.NotAllowed.ToString() } },
        };

        foreach (var candidate in invalid)
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateBaseline(candidate));
    }

    [Fact]
    public void ResumeHelloAndFirstWireMustMatchIdentityCursorAndCompleteActorState()
    {
        var baseline = CreateBaseline();
        var snapshot = CreateSnapshot(baseline);
        PerformanceResumeStateContract.ValidateResumeHandshakeAndFirstWire(
            baseline, CreateHello(baseline), snapshot);

        var invalidHellos = new[] {
            CreateHello(baseline) with { LastClientSequence = baseline.LastCommand.ClientSequence - 1 },
            CreateHello(baseline) with { ResumedSessionMatched = false },
            CreateHello(baseline) with { Identity = baseline.Identity with { ServerBuildId = "other-build" } },
            CreateHello(baseline) with { Identity = baseline.Identity with { ProtocolSchemaVersion = "other-version" } },
            CreateHello(baseline) with { Identity = baseline.Identity with { ContentManifestHash = new string('b', 64) } },
            CreateHello(baseline) with { Identity = baseline.Identity with { RulesetIdentityHash = new string('d', 64) } },
            CreateHello(baseline) with { Identity = baseline.Identity with { TickRateHz = baseline.Identity.TickRateHz + 1 } },
            CreateHello(baseline) with { Identity = baseline.Identity with { GameMode = GameMode.Multiplayer.ToString() } },
            CreateHello(baseline) with { Identity = baseline.Identity with { CommandSchedulingMode = CommandSchedulingMode.Immediate.ToString() } },
        };
        foreach (var hello in invalidHellos)
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateResumeHandshakeAndFirstWire(baseline, hello, snapshot));

        var changedContent = snapshot.Clone();
        changedContent.ContentManifestHash = new string('b', 64);
        Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateResumeHandshakeAndFirstWire(baseline, CreateHello(baseline), changedContent));
    }

    [Fact]
    public void FirstWireRejectsChangedIdentityResourcesAttributesAndActionState()
    {
        var baseline = CreateBaseline();
        var mutations = new Action<PlayerSnapshot>[] {
            player => player.EntityId++,
            player => player.CharacterName = "other",
            player => player.CharacterClassId++,
            player => player.LevelId++,
            player => player.PositionX++,
            player => player.Life--,
            player => player.LifeMaximum++,
            player => player.Mana++,
            player => player.ManaMaximum++,
            player => player.BaseLifeMaximum++,
            player => player.BaseManaMaximum++,
            player => player.Gold++,
            player => player.Experience++,
            player => player.Attributes.Strength.Current++,
            player => player.Attributes.Magic.Base++,
            player => player.Attributes.Dexterity.Current++,
            player => player.Attributes.Vitality.Base++,
            player => player.FacingDirection = PlayerDirection.North,
            player => player.CombatModeEndTick++,
            player => player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Move },
        };

        foreach (var mutate in mutations) {
            var snapshot = CreateSnapshot(baseline);
            mutate(snapshot.Players[0]);
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateResumeHandshakeAndFirstWire(
                baseline, CreateHello(baseline), snapshot));
        }
    }

    [Fact]
    public void AcceptsOnlyExactlyNextClientSequenceAndLaterIndependentServerReceipt()
    {
        var baseline = CreateBaseline();
        var control = new PerformanceResumeLegalControl(
            PerformanceResumeDirection.Right, 1, 3, PlayerDirection.SouthEast);
        var command = new PerformanceResumeCommandReceipt(
            16, 24, 121, CommandStatus.Accepted.ToString(), CommandRejectReason.Unspecified.ToString());
        var finalPlayer = baseline.Player with {
            PositionX = control.TargetX,
            PositionY = control.TargetY,
            FacingDirection = control.ExpectedFacing.ToString(),
        };

        PerformanceResumeStateContract.ValidateNextCommand(baseline, command, 122, finalPlayer, control);

        var invalid = new[] {
            command with { ClientSequence = 17 },
            command with { ClientSequence = 15 },
            command with { ServerReceiptSequence = 23 },
            command with { AppliedTick = 118 },
            command with { Status = CommandStatus.Unspecified.ToString() },
            command with { Status = CommandStatus.Rejected.ToString() },
            command with { Status = CommandStatus.Duplicate.ToString() },
            command with { RejectReason = CommandRejectReason.InvalidTarget.ToString() },
            command with { AppliedTick = 123 },
        };
        foreach (var candidate in invalid)
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateNextCommand(
                baseline, candidate, 122, finalPlayer, control));

        Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateNextCommand(
            baseline, command, 122, finalPlayer with { FacingDirection = PlayerDirection.South.ToString() }, control));
    }

    [Fact]
    public void RejectsUnspecifiedResumeCommandStatusWithOtherwiseValidCursor()
    {
        var baseline = CreateBaseline();
        var unspecified = new PerformanceResumeCommandReceipt(
            16, 24, 121, CommandStatus.Unspecified.ToString(), CommandRejectReason.Unspecified.ToString());

        Assert.Throws<InvalidDataException>(() =>
            PerformanceResumeStateContract.ValidateNextCommandReceipt(baseline, unspecified));
    }

    [Fact]
    public void WaitsForIntermediateCommandMovementToSettleWithoutRejectingIt()
    {
        var baseline = CreateBaseline();
        var control = new PerformanceResumeLegalControl(
            PerformanceResumeDirection.Right, 3, 2, PlayerDirection.SouthEast);
        var command = new PerformanceResumeCommandReceipt(
            16, 24, 121, CommandStatus.Accepted.ToString(), CommandRejectReason.Unspecified.ToString());
        var walking = baseline.Player with {
            PositionX = 2,
            PositionY = 2,
            CombatMode = PlayerCombatMode.WalkSouthwards.ToString(),
            CurrentActionPresent = true,
            FacingDirection = PlayerDirection.SouthEast.ToString(),
            CombatModeEndTick = 125,
        };

        PerformanceResumeStateContract.ValidateNextCommandReceipt(baseline, command);
        Assert.False(PerformanceResumeStateContract.IsSettledAtControlTarget(
            baseline, command, 122, walking, control));
        Assert.False(PerformanceResumeStateContract.IsSettledAtControlTarget(
            baseline, command, 120, baseline.Player with {
                PositionX = control.TargetX,
                PositionY = control.TargetY,
                FacingDirection = control.ExpectedFacing.ToString(),
            }, control));
        Assert.True(PerformanceResumeStateContract.IsSettledAtControlTarget(
            baseline, command, 126, baseline.Player with {
                PositionX = control.TargetX,
                PositionY = control.TargetY,
                FacingDirection = control.ExpectedFacing.ToString(),
                CombatModeEndTick = 125,
            }, control));
    }

    [Fact]
    public void FinalResumeActorMustPreserveBaselineIdentityAndResourceScalars()
    {
        var baseline = CreateBaseline();
        var control = new PerformanceResumeLegalControl(
            PerformanceResumeDirection.Right, 3, 2, PlayerDirection.SouthEast);
        var command = new PerformanceResumeCommandReceipt(
            16, 24, 121, CommandStatus.Accepted.ToString(), CommandRejectReason.Unspecified.ToString());
        var settled = baseline.Player with {
            PositionX = control.TargetX,
            PositionY = control.TargetY,
            FacingDirection = control.ExpectedFacing.ToString(),
            CombatModeEndTick = 122,
        };
        var mutations = new[] {
            settled with { EntityId = settled.EntityId + 1 },
            settled with { CharacterName = "other" },
            settled with { CharacterClassId = settled.CharacterClassId + 1 },
            settled with { CharacterLevel = settled.CharacterLevel + 1 },
            settled with { LevelId = settled.LevelId + 1 },
            settled with { Life = settled.Life - 1 },
            settled with { LifeMaximum = settled.LifeMaximum + 1 },
            settled with { Mana = settled.Mana + 1 },
            settled with { ManaMaximum = settled.ManaMaximum + 1 },
            settled with { BaseLifeMaximum = settled.BaseLifeMaximum + 1 },
            settled with { BaseManaMaximum = settled.BaseManaMaximum + 1 },
            settled with { Gold = settled.Gold + 1 },
            settled with { Experience = settled.Experience + 1 },
            settled with { HostileMode = !settled.HostileMode },
            settled with { Attributes = settled.Attributes with { StrengthBase = settled.Attributes.StrengthBase + 1 } },
            settled with { Attributes = settled.Attributes with { StrengthCurrent = settled.Attributes.StrengthCurrent + 1 } },
            settled with { Attributes = settled.Attributes with { MagicBase = settled.Attributes.MagicBase + 1 } },
            settled with { Attributes = settled.Attributes with { MagicCurrent = settled.Attributes.MagicCurrent + 1 } },
            settled with { Attributes = settled.Attributes with { DexterityBase = settled.Attributes.DexterityBase + 1 } },
            settled with { Attributes = settled.Attributes with { DexterityCurrent = settled.Attributes.DexterityCurrent + 1 } },
            settled with { Attributes = settled.Attributes with { VitalityBase = settled.Attributes.VitalityBase + 1 } },
            settled with { Attributes = settled.Attributes with { VitalityCurrent = settled.Attributes.VitalityCurrent + 1 } },
        };

        foreach (var changed in mutations)
            Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateNextCommand(
                baseline, command, 126, changed, control));

        Assert.Throws<InvalidDataException>(() => PerformanceResumeStateContract.ValidateNextCommand(
            baseline, command, 126, settled with { CombatModeEndTick = 127 }, control));
    }

    [Fact]
    public void SelectsOnlyAnAdjacentControlSafeInBothFirstWireAndCurrentGeometry()
    {
        var baseline = CreateBaseline();
        var firstWire = CreateSnapshot(baseline);
        var current = CreateSnapshot(baseline);
        firstWire.LevelGeometry[0].BlockedCells.Add(7); // Up from (2,2).

        var selected = PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current);

        Assert.Equal(PerformanceResumeDirection.Right, selected?.Direction);
        Assert.Equal((3, 2, PlayerDirection.SouthEast), (selected!.TargetX, selected.TargetY, selected.ExpectedFacing));

        current.Objects.Add(new ObjectSnapshot { EntityId = 88, LevelId = 0, PositionX = 3, PositionY = 2 });
        var changedWorld = PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current);
        Assert.Equal(PerformanceResumeDirection.Down, changedWorld?.Direction);
        Assert.Equal((2, 3), (changedWorld!.TargetX, changedWorld.TargetY));

        current = CreateSnapshot(baseline);
        current.Players[0].Mana++;
        Assert.Null(PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current));
    }

    [Fact]
    public void RefusesIncompleteGeometryAndAnyOccupiedOrTriggeredCandidateCell()
    {
        var baseline = CreateBaseline();
        var firstWire = CreateSnapshot(baseline);
        var current = CreateSnapshot(baseline);
        BlockAllAdjacent(firstWire.LevelGeometry[0]);
        Assert.Null(PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current));

        firstWire = CreateSnapshot(baseline);
        current = CreateSnapshot(baseline);
        current.LevelGeometry[0].NativeObjectLayer.Add(1);
        Assert.Null(PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current));

        var blockers = new Action<Snapshot>[] {
            snapshot => snapshot.Objects.Add(new ObjectSnapshot { EntityId = 1, LevelId = 0, PositionX = 2, PositionY = 1 }),
            snapshot => snapshot.WorldItems.Add(new WorldItemSnapshot { EntityId = 1, LevelId = 0, PositionX = 2, PositionY = 1 }),
            snapshot => snapshot.Monsters.Add(new MonsterSnapshot { EntityId = 1, LevelId = 0, PositionX = 2, PositionY = 1, Alive = true }),
            snapshot => snapshot.Monsters.Add(new MonsterSnapshot { EntityId = 1, LevelId = 0, PositionX = 1, PositionY = 1, FuturePositionX = 2, FuturePositionY = 1, Moving = true, Alive = true }),
            snapshot => snapshot.WarpTriggersForTest(2, 1),
            snapshot => snapshot.TownerGreetings.Add(new TownerGreetingSnapshot { LevelId = 0, PositionX = 2, PositionY = 1 }),
            snapshot => snapshot.QuestTownerInteractions.Add(new QuestTownerInteractionSnapshot { LevelId = 0, PositionX = 2, PositionY = 1 }),
            snapshot => snapshot.Portals.Add(new PortalSnapshot { PortalId = 7, SourceLevelId = 0, SourcePositionX = 2, SourcePositionY = 1 }),
        };
        foreach (var addBlocker in blockers) {
            firstWire = CreateSnapshot(baseline);
            current = CreateSnapshot(baseline);
            BlockAllExceptUp(firstWire.LevelGeometry[0]);
            addBlocker(current);
            Assert.Null(PerformanceResumeStateContract.SelectLegalControl(baseline, firstWire, current));
        }
    }

    private static PerformanceResumeBaseline CreateBaseline()
        => new(
            2,
            true,
            "PerformanceCreate",
            new PerformanceResumeServerIdentity(
                "devilution-server", "0.2.0", new string('a', 64), new string('c', 64), 20,
                GameMode.SinglePlayer.ToString(), CommandSchedulingMode.ClockGated.ToString()),
            120,
            new PerformanceResumeConnectionProvenance(4, true, true),
            new PerformanceResumeCommandReceipt(15, 23, 118,
                CommandStatus.Accepted.ToString(), CommandRejectReason.Unspecified.ToString()),
            new PerformanceResumePlayerState(
                2, "M0-Performance-1", 1, 0, 2, 2, 1,
                128, 128, 0, 0, 128, 0, 0, 0, false,
                PlayerCombatMode.Stand.ToString(), false, PlayerDirection.West.ToString(), 117,
                new PerformanceResumeAttributes(10, 10, 10, 10, 10, 10, 10, 10)));

    private static PerformanceResumeServerHello CreateHello(PerformanceResumeBaseline baseline)
        => new(baseline.Identity, baseline.LastCommand.ClientSequence, ResumedSessionMatched: true);

    private static Snapshot CreateSnapshot(PerformanceResumeBaseline baseline)
    {
        var snapshot = new Snapshot {
            Tick = baseline.SnapshotTick,
            ContentManifestHash = baseline.Identity.ContentManifestHash,
            RulesetIdentityHash = baseline.Identity.RulesetIdentityHash,
        };
        snapshot.Players.Add(CreatePlayer(baseline.Player));
        snapshot.LevelGeometry.Add(new LevelGeometrySnapshot { LevelId = 0, Width = 5, Height = 5 });
        return snapshot;
    }

    private static PlayerSnapshot CreatePlayer(PerformanceResumePlayerState state)
        => new() {
            EntityId = state.EntityId,
            CharacterName = state.CharacterName,
            CharacterClassId = state.CharacterClassId,
            LevelId = state.LevelId,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            CharacterLevel = state.CharacterLevel,
            Life = state.Life,
            LifeMaximum = state.LifeMaximum,
            Mana = state.Mana,
            ManaMaximum = state.ManaMaximum,
            BaseLifeMaximum = state.BaseLifeMaximum,
            BaseManaMaximum = state.BaseManaMaximum,
            Gold = state.Gold,
            Experience = state.Experience,
            HostileMode = state.HostileMode,
            CombatMode = Enum.Parse<PlayerCombatMode>(state.CombatMode),
            FacingDirection = Enum.Parse<PlayerDirection>(state.FacingDirection),
            CombatModeEndTick = state.CombatModeEndTick,
            Attributes = new PlayerAttributesSnapshot {
                Strength = new AttributeSnapshot { Base = state.Attributes.StrengthBase, Current = state.Attributes.StrengthCurrent },
                Magic = new AttributeSnapshot { Base = state.Attributes.MagicBase, Current = state.Attributes.MagicCurrent },
                Dexterity = new AttributeSnapshot { Base = state.Attributes.DexterityBase, Current = state.Attributes.DexterityCurrent },
                Vitality = new AttributeSnapshot { Base = state.Attributes.VitalityBase, Current = state.Attributes.VitalityCurrent },
            },
        };

    private static void BlockAllAdjacent(LevelGeometrySnapshot geometry)
    {
        geometry.BlockedCells.Add(7);
        geometry.BlockedCells.Add(13);
        geometry.BlockedCells.Add(17);
        geometry.BlockedCells.Add(11);
    }

    private static void BlockAllExceptUp(LevelGeometrySnapshot geometry)
    {
        geometry.BlockedCells.Add(13);
        geometry.BlockedCells.Add(17);
        geometry.BlockedCells.Add(11);
    }
}

internal static class PerformanceResumeTestSnapshotExtensions
{
    public static void WarpTriggersForTest(this Snapshot snapshot, int x, int y)
        => snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot {
            TriggerX = x,
            TriggerY = y,
            Available = true,
        });
}

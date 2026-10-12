using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Safe, credential-free scalar state captured after the Performance workload. */
internal sealed record PerformanceResumeBaseline(
    int SchemaVersion,
    bool Passed,
    string Phase,
    PerformanceResumeServerIdentity Identity,
    ulong SnapshotTick,
    PerformanceResumeConnectionProvenance Connection,
    PerformanceResumeCommandReceipt LastCommand,
    PerformanceResumePlayerState Player);

internal sealed record PerformanceResumeConnectionProvenance(
    long AttemptEpoch,
    bool AdmissionCompleted,
    bool TransportConnected);

internal sealed record PerformanceResumeServerIdentity(
    string ServerBuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash,
    uint TickRateHz,
    string GameMode,
    string CommandSchedulingMode);

internal sealed record PerformanceResumeCommandReceipt(
    ulong ClientSequence,
    ulong ServerReceiptSequence,
    ulong AppliedTick,
    string Status,
    string RejectReason);

internal sealed record PerformanceResumePlayerState(
    uint EntityId,
    string CharacterName,
    uint CharacterClassId,
    uint LevelId,
    int PositionX,
    int PositionY,
    uint CharacterLevel,
    int Life,
    int LifeMaximum,
    int Mana,
    int ManaMaximum,
    int BaseLifeMaximum,
    int BaseManaMaximum,
    uint Gold,
    uint Experience,
    bool HostileMode,
    string CombatMode,
    bool CurrentActionPresent,
    string FacingDirection,
    ulong CombatModeEndTick,
    PerformanceResumeAttributes Attributes);

internal sealed record PerformanceResumeAttributes(
    int StrengthBase,
    int StrengthCurrent,
    int MagicBase,
    int MagicCurrent,
    int DexterityBase,
    int DexterityCurrent,
    int VitalityBase,
    int VitalityCurrent);

internal sealed record PerformanceResumeServerHello(
    PerformanceResumeServerIdentity Identity,
    ulong LastClientSequence,
    bool ResumedSessionMatched);

internal enum PerformanceResumeDirection
{
    Up,
    Right,
    Down,
    Left,
}

internal sealed record PerformanceResumeLegalControl(
    PerformanceResumeDirection Direction,
    int TargetX,
    int TargetY,
    PlayerDirection ExpectedFacing);

/** Closed scalar schema and independent admission/command checks for fresh resume. */
internal static class PerformanceResumeStateContract
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] BaselineFields = [
        nameof(PerformanceResumeBaseline.SchemaVersion),
        nameof(PerformanceResumeBaseline.Passed),
        nameof(PerformanceResumeBaseline.Phase),
        nameof(PerformanceResumeBaseline.Identity),
        nameof(PerformanceResumeBaseline.SnapshotTick),
        nameof(PerformanceResumeBaseline.Connection),
        nameof(PerformanceResumeBaseline.LastCommand),
        nameof(PerformanceResumeBaseline.Player),
    ];

    private static readonly string[] ConnectionFields = [
        nameof(PerformanceResumeConnectionProvenance.AttemptEpoch),
        nameof(PerformanceResumeConnectionProvenance.AdmissionCompleted),
        nameof(PerformanceResumeConnectionProvenance.TransportConnected),
    ];

    private static readonly string[] IdentityFields = [
        nameof(PerformanceResumeServerIdentity.ServerBuildId),
        nameof(PerformanceResumeServerIdentity.ProtocolSchemaVersion),
        nameof(PerformanceResumeServerIdentity.ContentManifestHash),
        nameof(PerformanceResumeServerIdentity.RulesetIdentityHash),
        nameof(PerformanceResumeServerIdentity.TickRateHz),
        nameof(PerformanceResumeServerIdentity.GameMode),
        nameof(PerformanceResumeServerIdentity.CommandSchedulingMode),
    ];

    private static readonly string[] ReceiptFields = [
        nameof(PerformanceResumeCommandReceipt.ClientSequence),
        nameof(PerformanceResumeCommandReceipt.ServerReceiptSequence),
        nameof(PerformanceResumeCommandReceipt.AppliedTick),
        nameof(PerformanceResumeCommandReceipt.Status),
        nameof(PerformanceResumeCommandReceipt.RejectReason),
    ];

    private static readonly string[] PlayerFields = [
        nameof(PerformanceResumePlayerState.EntityId),
        nameof(PerformanceResumePlayerState.CharacterName),
        nameof(PerformanceResumePlayerState.CharacterClassId),
        nameof(PerformanceResumePlayerState.LevelId),
        nameof(PerformanceResumePlayerState.PositionX),
        nameof(PerformanceResumePlayerState.PositionY),
        nameof(PerformanceResumePlayerState.CharacterLevel),
        nameof(PerformanceResumePlayerState.Life),
        nameof(PerformanceResumePlayerState.LifeMaximum),
        nameof(PerformanceResumePlayerState.Mana),
        nameof(PerformanceResumePlayerState.ManaMaximum),
        nameof(PerformanceResumePlayerState.BaseLifeMaximum),
        nameof(PerformanceResumePlayerState.BaseManaMaximum),
        nameof(PerformanceResumePlayerState.Gold),
        nameof(PerformanceResumePlayerState.Experience),
        nameof(PerformanceResumePlayerState.HostileMode),
        nameof(PerformanceResumePlayerState.CombatMode),
        nameof(PerformanceResumePlayerState.CurrentActionPresent),
        nameof(PerformanceResumePlayerState.FacingDirection),
        nameof(PerformanceResumePlayerState.CombatModeEndTick),
        nameof(PerformanceResumePlayerState.Attributes),
    ];

    private static readonly string[] AttributeFields = [
        nameof(PerformanceResumeAttributes.StrengthBase),
        nameof(PerformanceResumeAttributes.StrengthCurrent),
        nameof(PerformanceResumeAttributes.MagicBase),
        nameof(PerformanceResumeAttributes.MagicCurrent),
        nameof(PerformanceResumeAttributes.DexterityBase),
        nameof(PerformanceResumeAttributes.DexterityCurrent),
        nameof(PerformanceResumeAttributes.VitalityBase),
        nameof(PerformanceResumeAttributes.VitalityCurrent),
    ];

    public static PerformanceResumeBaseline ReadBaseline(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException("The Performance resume baseline path must be absolute.");
        return ParseBaseline(File.ReadAllText(path));
    }

    public static PerformanceResumeBaseline ParseBaseline(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });
        var root = document.RootElement;
        RequireExactObject(root, BaselineFields, "baseline");
        RequireExactObject(root.GetProperty(nameof(PerformanceResumeBaseline.Identity)), IdentityFields, "identity");
        RequireExactObject(root.GetProperty(nameof(PerformanceResumeBaseline.Connection)), ConnectionFields, "connection provenance");
        RequireExactObject(root.GetProperty(nameof(PerformanceResumeBaseline.LastCommand)), ReceiptFields, "last command");
        var player = root.GetProperty(nameof(PerformanceResumeBaseline.Player));
        RequireExactObject(player, PlayerFields, "player");
        RequireExactObject(player.GetProperty(nameof(PerformanceResumePlayerState.Attributes)), AttributeFields, "attributes");

        var baseline = JsonSerializer.Deserialize<PerformanceResumeBaseline>(root.GetRawText(), JsonOptions)
            ?? throw new InvalidDataException("The Performance resume baseline is empty.");
        ValidateBaseline(baseline);
        return baseline;
    }

    public static void ValidateBaseline(PerformanceResumeBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (baseline.SchemaVersion != 2 || !baseline.Passed || baseline.Phase != "PerformanceCreate")
            throw new InvalidDataException("The baseline is not a successful PerformanceCreate observation.");
        if (baseline.Identity is null
            || string.IsNullOrWhiteSpace(baseline.Identity.ServerBuildId)
            || string.IsNullOrWhiteSpace(baseline.Identity.ProtocolSchemaVersion)
            || !IsSha256(baseline.Identity.ContentManifestHash)
            || !IsSha256(baseline.Identity.RulesetIdentityHash)
            || baseline.Identity.TickRateHz == 0
            || baseline.Identity.GameMode != GameMode.SinglePlayer.ToString()
            || baseline.Identity.CommandSchedulingMode != CommandSchedulingMode.ClockGated.ToString())
            throw new InvalidDataException("The PerformanceCreate server identity is invalid.");
        if (baseline.Connection is null
            || baseline.Connection.AttemptEpoch <= 0
            || !baseline.Connection.AdmissionCompleted
            || !baseline.Connection.TransportConnected)
            throw new InvalidDataException("The PerformanceCreate connection provenance is invalid.");
        if (baseline.SnapshotTick == 0 || baseline.LastCommand is null
            || baseline.LastCommand.ClientSequence == 0
            || baseline.LastCommand.ClientSequence >= ulong.MaxValue - 1
            || baseline.LastCommand.ServerReceiptSequence == 0
            || baseline.LastCommand.AppliedTick == 0
            || baseline.LastCommand.AppliedTick > baseline.SnapshotTick
            || baseline.LastCommand.Status != nameof(CommandStatus.Accepted)
            || baseline.LastCommand.RejectReason != CommandRejectReason.Unspecified.ToString())
            throw new InvalidDataException("The PerformanceCreate command cursor is invalid.");
        if (baseline.Player is null
            || baseline.Player.EntityId == 0
            || baseline.Player.CharacterName != "M0-Performance-1"
            || baseline.Player.CharacterClassId != 1
            || baseline.Player.LevelId != 0
            || baseline.Player.CharacterLevel == 0
            || baseline.Player.Life <= 0
            || baseline.Player.LifeMaximum <= 0
            || baseline.Player.Life > baseline.Player.LifeMaximum
            || baseline.Player.Mana < 0
            || baseline.Player.ManaMaximum < 0
            || baseline.Player.Mana > baseline.Player.ManaMaximum
            || baseline.Player.BaseLifeMaximum < 0
            || baseline.Player.BaseManaMaximum < 0
            || baseline.Player.HostileMode
            || baseline.Player.CombatMode != PlayerCombatMode.Stand.ToString()
            || baseline.Player.CurrentActionPresent
            || !Enum.TryParse<PlayerDirection>(baseline.Player.FacingDirection, ignoreCase: false, out var facing)
            || !Enum.IsDefined(typeof(PlayerDirection), facing)
            || baseline.Player.Attributes is null
            || !AttributesAreNonNegative(baseline.Player.Attributes))
            throw new InvalidDataException("The PerformanceCreate actor state is invalid or unsettled.");
    }

    public static PerformanceResumePlayerState Project(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var attributes = player.Attributes
            ?? throw new InvalidDataException("The authoritative actor attributes are missing.");
        var strength = attributes.Strength
            ?? throw new InvalidDataException("The authoritative Strength projection is missing.");
        var magic = attributes.Magic
            ?? throw new InvalidDataException("The authoritative Magic projection is missing.");
        var dexterity = attributes.Dexterity
            ?? throw new InvalidDataException("The authoritative Dexterity projection is missing.");
        var vitality = attributes.Vitality
            ?? throw new InvalidDataException("The authoritative Vitality projection is missing.");
        return new PerformanceResumePlayerState(
            player.EntityId,
            player.CharacterName,
            player.CharacterClassId,
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.CharacterLevel,
            player.Life,
            player.LifeMaximum,
            player.Mana,
            player.ManaMaximum,
            player.BaseLifeMaximum,
            player.BaseManaMaximum,
            player.Gold,
            player.Experience,
            player.HostileMode,
            player.CombatMode.ToString(),
            player.CurrentAction is not null,
            player.FacingDirection.ToString(),
            player.CombatModeEndTick,
            new PerformanceResumeAttributes(
                strength.Base, strength.Current,
                magic.Base, magic.Current,
                dexterity.Base, dexterity.Current,
                vitality.Base, vitality.Current));
    }

    public static void ValidateResumeHandshakeAndFirstWire(
        PerformanceResumeBaseline baseline,
        PerformanceResumeServerHello hello,
        Snapshot firstWireSnapshot)
    {
        ValidateBaseline(baseline);
        ArgumentNullException.ThrowIfNull(hello);
        ArgumentNullException.ThrowIfNull(firstWireSnapshot);
        if (!hello.ResumedSessionMatched
            || hello.LastClientSequence != baseline.LastCommand.ClientSequence
            || hello.Identity != baseline.Identity)
            throw new InvalidDataException("The first Resume handshake does not match the saved identity and cursor.");
        if (firstWireSnapshot.Tick < baseline.SnapshotTick
            || firstWireSnapshot.ContentManifestHash != baseline.Identity.ContentManifestHash
            || firstWireSnapshot.RulesetIdentityHash != baseline.Identity.RulesetIdentityHash
            || firstWireSnapshot.Players.Count != 1)
            throw new InvalidDataException("The first Resume snapshot does not match the saved session.");
        var player = Project(firstWireSnapshot.Players[0]);
        if (player != baseline.Player)
            throw new InvalidDataException("The first Resume player snapshot differs from the PerformanceCreate baseline.");
    }

    public static void ValidateNextCommandReceipt(
        PerformanceResumeBaseline baseline,
        PerformanceResumeCommandReceipt command)
    {
        ValidateBaseline(baseline);
        ArgumentNullException.ThrowIfNull(command);
        if (baseline.LastCommand.ClientSequence == ulong.MaxValue - 1
            || command.ClientSequence != baseline.LastCommand.ClientSequence + 1
            || command.ServerReceiptSequence <= baseline.LastCommand.ServerReceiptSequence
            || command.AppliedTick <= baseline.LastCommand.AppliedTick
            || command.AppliedTick == 0
            || command.Status != nameof(CommandStatus.Accepted)
            || command.RejectReason != CommandRejectReason.Unspecified.ToString())
            throw new InvalidDataException("The first post-Resume command was not the next accepted command.");
    }

    public static bool IsSettledAtControlTarget(
        PerformanceResumeBaseline baseline,
        PerformanceResumeCommandReceipt command,
        ulong snapshotTick,
        PerformanceResumePlayerState player,
        PerformanceResumeLegalControl control)
    {
        ValidateBaseline(baseline);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(control);
        ValidateFinalActorPreservesBaseline(baseline.Player, player);
        ValidateNextCommandReceipt(baseline, command);
        return snapshotTick >= command.AppliedTick
            && player.PositionX == control.TargetX
            && player.PositionY == control.TargetY
            && player.CombatMode == PlayerCombatMode.Stand.ToString()
            && !player.CurrentActionPresent
            && player.FacingDirection == control.ExpectedFacing.ToString()
            && player.CombatModeEndTick <= snapshotTick;
    }

    public static void ValidateNextCommand(
        PerformanceResumeBaseline baseline,
        PerformanceResumeCommandReceipt command,
        ulong finalSnapshotTick,
        PerformanceResumePlayerState finalPlayer,
        PerformanceResumeLegalControl control)
    {
        ArgumentNullException.ThrowIfNull(finalPlayer);
        ArgumentNullException.ThrowIfNull(control);
        if (!IsSettledAtControlTarget(baseline, command, finalSnapshotTick, finalPlayer, control))
            throw new InvalidDataException("The post-Resume command did not settle at its prevalidated destination.");
    }

    public static PerformanceResumeLegalControl? SelectLegalControl(
        PerformanceResumeBaseline baseline,
        Snapshot firstWireSnapshot,
        Snapshot currentSnapshot)
    {
        ValidateBaseline(baseline);
        ArgumentNullException.ThrowIfNull(firstWireSnapshot);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        if (!SnapshotMatchesBaseline(baseline, firstWireSnapshot)
            || !SnapshotMatchesBaseline(baseline, currentSnapshot))
            return null;

        var origin = baseline.Player;
        var candidates = new[] {
            TryMakeControl(PerformanceResumeDirection.Up, origin.PositionX, origin.PositionY, 0, -1, PlayerDirection.NorthEast),
            TryMakeControl(PerformanceResumeDirection.Right, origin.PositionX, origin.PositionY, 1, 0, PlayerDirection.SouthEast),
            TryMakeControl(PerformanceResumeDirection.Down, origin.PositionX, origin.PositionY, 0, 1, PlayerDirection.SouthWest),
            TryMakeControl(PerformanceResumeDirection.Left, origin.PositionX, origin.PositionY, -1, 0, PlayerDirection.NorthWest),
        }.Where(control => control is not null).Cast<PerformanceResumeLegalControl>().ToArray();
        return candidates.FirstOrDefault(candidate =>
            IsLegalTarget(firstWireSnapshot, origin.EntityId, candidate.TargetX, candidate.TargetY)
            && IsLegalTarget(currentSnapshot, origin.EntityId, candidate.TargetX, candidate.TargetY));
    }

    private static bool SnapshotMatchesBaseline(PerformanceResumeBaseline baseline, Snapshot snapshot)
        => snapshot.Tick >= baseline.SnapshotTick
            && snapshot.ContentManifestHash == baseline.Identity.ContentManifestHash
            && snapshot.RulesetIdentityHash == baseline.Identity.RulesetIdentityHash
            && snapshot.Players.Count == 1
            && Project(snapshot.Players[0]) == baseline.Player;

    private static bool IsLegalTarget(Snapshot snapshot, uint actorEntityId, int x, int y)
    {
        var actor = snapshot.Players.SingleOrDefault(player => player.EntityId == actorEntityId);
        if (actor is null || actor.CurrentAction is not null || actor.CombatMode != PlayerCombatMode.Stand)
            return false;
        var geometry = snapshot.LevelGeometry.SingleOrDefault(level => level.LevelId == actor.LevelId);
        if (geometry is null || geometry.Width <= 0 || geometry.Height <= 0
            || x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height)
            return false;
        var cellCount = (long)geometry.Width * geometry.Height;
        if (cellCount > int.MaxValue)
            return false;
        var cell = checked(y * geometry.Width + x);
        if (AuthoritativeLevelGeometryCellResolver.Resolve(geometry, x, y) != AuthoritativeLevelGeometryCell.Walkable
            || geometry.StaticObjectBlockedCells.Contains(cell)
            || (geometry.NativeObjectLayer.Count != 0
                && (geometry.NativeObjectLayer.Count != cellCount || geometry.NativeObjectLayer[cell] != 0))
            || geometry.WarpTriggers.Any(trigger => trigger.TriggerX == x && trigger.TriggerY == y))
            return false;
        if (snapshot.Players.Any(player => player.EntityId != actorEntityId
                && player.LevelId == actor.LevelId
                && Occupies(player, x, y)))
            return false;
        if (snapshot.Monsters.Any(monster => monster.Alive && monster.LevelId == actor.LevelId
                && (monster.PositionX == x && monster.PositionY == y
                    || (monster.Moving && monster.FuturePositionX == x && monster.FuturePositionY == y))))
            return false;
        if (snapshot.Objects.Any(item => item.LevelId == actor.LevelId && item.PositionX == x && item.PositionY == y)
            || snapshot.WorldItems.Any(item => item.LevelId == actor.LevelId && item.PositionX == x && item.PositionY == y)
            || snapshot.Corpses.Any(item => item.LevelId == actor.LevelId && item.PositionX == x && item.PositionY == y)
            || snapshot.Portals.Any(portal =>
                (portal.SourceLevelId == actor.LevelId && portal.SourcePositionX == x && portal.SourcePositionY == y)
                || (portal.DestinationLevelId == actor.LevelId && portal.DestinationPositionX == x && portal.DestinationPositionY == y))
            || snapshot.TownerGreetings.Any(actorEntry => actorEntry.LevelId == actor.LevelId
                && actorEntry.PositionX == x && actorEntry.PositionY == y)
            || snapshot.QuestTownerInteractions.Any(actorEntry => actorEntry.LevelId == actor.LevelId
                && actorEntry.PositionX == x && actorEntry.PositionY == y))
            return false;
        return true;
    }

    private static bool Occupies(PlayerSnapshot player, int x, int y)
    {
        var current = AuthoritativePlayerPositionProjection.Resolve(player);
        return (player.PositionX == x && player.PositionY == y) || (current.X == x && current.Y == y);
    }

    private static PerformanceResumeLegalControl? TryMakeControl(
        PerformanceResumeDirection direction,
        int originX,
        int originY,
        int offsetX,
        int offsetY,
        PlayerDirection facing)
    {
        var x = (long)originX + offsetX;
        var y = (long)originY + offsetY;
        return x is >= int.MinValue and <= int.MaxValue
            && y is >= int.MinValue and <= int.MaxValue
            ? new PerformanceResumeLegalControl(direction, (int)x, (int)y, facing)
            : null;
    }

    private static bool AttributesAreNonNegative(PerformanceResumeAttributes attributes)
        => attributes.StrengthBase >= 0 && attributes.StrengthCurrent >= 0
            && attributes.MagicBase >= 0 && attributes.MagicCurrent >= 0
            && attributes.DexterityBase >= 0 && attributes.DexterityCurrent >= 0
            && attributes.VitalityBase >= 0 && attributes.VitalityCurrent >= 0;

    private static void ValidateFinalActorPreservesBaseline(
        PerformanceResumePlayerState baseline,
        PerformanceResumePlayerState observed)
    {
        if (observed.EntityId != baseline.EntityId
            || observed.CharacterName != baseline.CharacterName
            || observed.CharacterClassId != baseline.CharacterClassId
            || observed.CharacterLevel != baseline.CharacterLevel
            || observed.LevelId != baseline.LevelId
            || observed.Life != baseline.Life
            || observed.LifeMaximum != baseline.LifeMaximum
            || observed.Mana != baseline.Mana
            || observed.ManaMaximum != baseline.ManaMaximum
            || observed.BaseLifeMaximum != baseline.BaseLifeMaximum
            || observed.BaseManaMaximum != baseline.BaseManaMaximum
            || observed.Gold != baseline.Gold
            || observed.Experience != baseline.Experience
            || observed.HostileMode != baseline.HostileMode
            || observed.Attributes != baseline.Attributes)
            throw new InvalidDataException("The post-Resume actor changed an identity or resource scalar.");
    }

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void RequireExactObject(JsonElement element, IEnumerable<string> expectedFields, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"The baseline {name} must be an object.");
        var expected = expectedFields.ToHashSet(StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) {
            if (!expected.Contains(property.Name) || !observed.Add(property.Name))
                throw new InvalidDataException($"The baseline {name} has an unknown or duplicate field.");
        }
        if (!observed.SetEquals(expected))
            throw new InvalidDataException($"The baseline {name} is missing a required field.");
    }
}

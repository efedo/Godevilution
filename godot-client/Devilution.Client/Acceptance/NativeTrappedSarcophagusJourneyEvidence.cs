using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

internal sealed record NativeTrappedSarcophagusCheckpoint(uint Level, uint Entity, int X, int Y,
    uint TrapSource, ulong OpeningTick, ulong FiringTick, ulong Sequence, ulong DeadlineTick,
    uint TerminalFrame, string Before, string After, uint[] Drops, uint[] Skeletons);

/** Opening provenance survives sparse snapshots through the wall trap's retained firing tick.
 * Loot exclusion cannot distinguish another producer whose activity leaves no difference between published snapshots. */
internal static class NativeTrappedSarcophagusJourneyEvidence
{
    // The default L4 generator reserves monster slot 6 for object slot 113 at (33,62).
    // NativePopulationBuilder binds it as 0x09001000 + (4 - 1) * 0x10000 + 6.
    private const uint RequiredSkeletonEntity = 0x09031006;

    internal sealed record Firing(uint TrapSource, ulong Tick);

    internal sealed class Opening
    {
        private readonly Snapshot before;
        private readonly ObjectSnapshot target;
        private readonly HashSet<uint> spent;

        internal Opening(Snapshot snapshot, uint entity)
        {
            before = snapshot.Clone();
            target = before.Objects.Single(value => value.EntityId == entity);
            spent = before.Objects.Where(value => value.NativeTrapFiredTriggerEntityId != 0 || value.HasNativeTrapFiredTick)
                .Select(value => value.EntityId).Concat(before.Projectiles.Where(value => value.SpellId == 0)
                    .Select(value => value.SourceEntityId)).ToHashSet();
        }

        internal Firing? Find(Snapshot after, ulong openingTick, ulong deadlineTick)
        {
            if (!CanOpen(target) || IsRequiredTarget(target) && !CanOpen(before, target.EntityId) || openingTick < before.Tick || deadlineTick <= openingTick || after.Tick <= openingTick)
                return null;
            var opened = after.Objects.SingleOrDefault(value => value.EntityId == target.EntityId);
            if (!IsOpened(opened, target)) return null;
            foreach (var source in after.Objects.Where(value => value.NativeTrapFiredTriggerEntityId == target.EntityId
                    && value.HasNativeTrapFiredTick && value.NativeTrapFiredTick > openingTick
                    && value.NativeTrapFiredTick < deadlineTick && value.NativeTrapFiredTick <= after.Tick)) {
                var original = before.Objects.SingleOrDefault(value => value.EntityId == source.EntityId);
                if (!spent.Contains(source.EntityId) && source.NativeObjectId is 53 or 54 && !source.Destroyed && !source.TrapArmed && !source.NativeTrapInteractable
                    && source.LevelId == target.LevelId && IsExpectedSource(source, target)
                    && original is not null && SameObject(source, original))
                    return new(source.EntityId, source.NativeTrapFiredTick);
            }
            return null;
        }
    }

    internal static bool CanOpen(ObjectSnapshot value)
        => value.NativeObjectId == 48 && value.LevelId is >= 1 and <= 4
            && value.NativeSarcophagusInteractable && !value.Activated && !value.Destroyed;

    /** TrapSense hides armed/disarm state from Warriors; the fixed wall source must still be unspent. */
    internal static bool CanOpen(Snapshot snapshot, uint entity)
    {
        var target = snapshot.Objects.SingleOrDefault(value => value.EntityId == entity);
        if (target is null || !IsRequiredTarget(target) || !CanOpen(target)) return false;
        var source = snapshot.Objects.SingleOrDefault(value => value.LevelId == target.LevelId && IsExpectedSource(value, target));
        return source is not null && !source.Destroyed && source.NativeTrapFiredTriggerEntityId == 0 && !source.HasNativeTrapFiredTick
            && !snapshot.Projectiles.Any(value => value.SpellId == 0 && value.SourceEntityId == source.EntityId);
    }

    internal static NativeTrappedSarcophagusCheckpoint Capture(Snapshot before, Snapshot after, uint entity,
        CommandResult receipt, ulong deadlineTick, uint terminalFrame)
    {
        if (!AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt) || receipt.ClientSequence == 0)
            throw new InvalidOperationException("The trapped sarcophagus has no accepted opening receipt.");
        var firing = new Opening(before, entity).Find(after, receipt.AppliedTick, deadlineTick)
            ?? throw new InvalidOperationException("The trapped sarcophagus has no fresh firing after its opening command.");
        var target = before.Objects.Single(value => value.EntityId == entity);
        // The required L4 owner is a skeleton case, so even nearby combat loot is not its outcome.
        var drops = IsRequiredTarget(target) ? [] : after.WorldItems.Where(value => IsNewDrop(before, value, target))
            .Select(value => value.EntityId).ToArray();
        if (drops.Length != 0) VerifySoleLootProducer(before, after, entity);
        var skeletons = after.Monsters.Where(value => IsReleasedSkeleton(before, after, value, target))
            .Select(value => value.EntityId).Concat(after.Corpses.Where(value => IsReleasedCorpse(before, value, target))
                .Select(value => value.EntityId)).Distinct().ToArray();
        var hasFallbackDrop = after.WorldItems.Any(value => drops.Contains(value.EntityId) && !IsNear(value.PositionX, value.PositionY, target));
        var result = new NativeTrappedSarcophagusCheckpoint(target.LevelId, entity, target.PositionX, target.PositionY,
            firing.TrapSource, receipt.AppliedTick, firing.Tick, receipt.ClientSequence, deadlineTick, terminalFrame,
            Encode(before, target.LevelId, hasFallbackDrop, drops.Length != 0),
            Encode(after, target.LevelId, includeLootProof: drops.Length != 0), drops, skeletons);
        VerifyRecorded(result);
        return result;
    }

    internal static void VerifyRecorded(NativeTrappedSarcophagusCheckpoint expected)
    {
        if (expected.Drops is null || expected.Skeletons is null)
            throw new InvalidOperationException("The trapped sarcophagus checkpoint omitted its release evidence.");
        NativeDepthResumeEvidence.VerifyTrappedSarcophagus(true, true, expected.Level, expected.Entity,
            expected.TrapSource, expected.OpeningTick, expected.FiringTick, expected.Drops.Length + expected.Skeletons.Length > 0);
        var before = Decode(expected.Before); var after = Decode(expected.After);
        var target = before.Objects.Single(value => value.EntityId == expected.Entity);
        var firing = new Opening(before, expected.Entity).Find(after, expected.OpeningTick, expected.DeadlineTick);
        if (expected.Sequence == 0 || target.LevelId != expected.Level || target.PositionX != expected.X || target.PositionY != expected.Y
            || firing is null || firing.TrapSource != expected.TrapSource || firing.Tick != expected.FiringTick
            || expected.TerminalFrame <= target.AnimationFrame
            || after.Objects.Single(value => value.EntityId == expected.Entity).AnimationFrame > expected.TerminalFrame)
            throw new InvalidOperationException("The recorded trapped sarcophagus lost its closed baseline, opening receipt or fresh firing provenance.");
        if (IsRequiredTarget(target) && (expected.Drops.Length != 0 || expected.Skeletons.Length != 1
                || expected.Skeletons[0] != RequiredSkeletonEntity))
            throw new InvalidOperationException("The required L4 sarcophagus did not release its own reserved skeleton.");
        if (expected.Drops.Length != 0) VerifySoleLootProducer(before, after, expected.Entity);
        foreach (var id in expected.Drops) {
            var item = after.WorldItems.Single(value => value.EntityId == id);
            if (!IsNewDrop(before, item, target))
                throw new InvalidOperationException("The sarcophagus loot is preexisting or outside its native drop placement.");
            NativeThemeObjectJourneyEvidence.VerifyDrop(item, expected.Level);
        }
        foreach (var id in expected.Skeletons) {
            if (!after.Monsters.Any(value => value.EntityId == id && IsReleasedSkeleton(before, after, value, target))
                && !after.Corpses.Any(value => value.EntityId == id && IsReleasedCorpse(before, value, target)))
                throw new InvalidOperationException("The sarcophagus has no newly released native skeleton.");
        }
    }

    internal static void VerifyRetained(Snapshot snapshot, NativeTrappedSarcophagusCheckpoint expected)
    {
        VerifyRecorded(expected);
        var original = Decode(expected.After);
        var target = snapshot.Objects.SingleOrDefault(value => value.EntityId == expected.Entity);
        var source = snapshot.Objects.SingleOrDefault(value => value.EntityId == expected.TrapSource);
        if (!IsOpened(target, original.Objects.Single(value => value.EntityId == expected.Entity))
            || target!.AnimationFrame != expected.TerminalFrame || source is null || source.Destroyed || source.TrapArmed || source.NativeTrapInteractable
            || !SameObject(source, original.Objects.Single(value => value.EntityId == expected.TrapSource))
            || source.NativeTrapFiredTriggerEntityId != expected.Entity || !source.HasNativeTrapFiredTick
            || source.NativeTrapFiredTick != expected.FiringTick)
            throw new InvalidOperationException("The trapped sarcophagus reopened, rearmed or lost its fired link on retention/resume.");
        foreach (var id in expected.Skeletons)
            if (!snapshot.Monsters.Any(value => value.EntityId == id && value.LevelId == expected.Level && (value.Alive || value.DeathTicksRemaining > 0))
                && !snapshot.Corpses.Any(value => value.EntityId == id && value.LevelId == expected.Level))
                throw new InvalidOperationException("The released sarcophagus skeleton or its combat corpse did not survive resume.");
    }

    // A sparse publication may show a newly released skeleton already killed by its wall trap.
    private static bool IsReleasedSkeleton(Snapshot before, Snapshot after, MonsterSnapshot value, ObjectSnapshot target)
        => value.EntityId != 0 && value.LevelId == target.LevelId && !value.IsPlayerMinion
            // The reserved actor remains this object's outcome after native AI walks it away.
            && (IsRequiredTarget(target) ? value.EntityId == RequiredSkeletonEntity : IsNear(value.PositionX, value.PositionY, target))
            && value.NativeAi is "SkeletonMelee" or "SkeletonRanged"
            && (value.Alive || value.DeathTicksRemaining > 0
                || after.Corpses.Any(corpse => corpse.EntityId == value.EntityId && corpse.LevelId == target.LevelId))
            && !before.Monsters.Any(old => old.EntityId == value.EntityId && old.Alive)
            && !before.Corpses.Any(old => old.EntityId == value.EntityId);

    // ReplaceDeadMonsterWithCorpse removes the combat target. Only this reserved ID
    // still attributes a corpse-only publication to its sarcophagus without an AI row.
    private static bool IsReleasedCorpse(Snapshot before, CorpseSnapshot value, ObjectSnapshot target)
        => IsRequiredTarget(target) && value.EntityId == RequiredSkeletonEntity && value.LevelId == target.LevelId
            && !before.Monsters.Any(old => old.EntityId == value.EntityId && old.Alive)
            && !before.Corpses.Any(old => old.EntityId == value.EntityId);

    private static bool IsNewDrop(Snapshot before, WorldItemSnapshot value, ObjectSnapshot target)
        => value.EntityId != 0 && value.LevelId == target.LevelId && !before.WorldItems.Any(old => old.EntityId == value.EntityId)
            && (IsNear(value.PositionX, value.PositionY, target) || IsNativeFallbackDrop(before, value, target));

    private static void VerifySoleLootProducer(Snapshot before, Snapshot after, uint sarcophagus)
    {
        foreach (var corpse in after.Corpses)
            if (!before.Corpses.Any(value => value.EntityId == corpse.EntityId && value.LevelId == corpse.LevelId))
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: new corpse {corpse.EntityId} is another visible drop producer.");
        foreach (var monster in before.Monsters.Where(value => value.Alive))
            if (!after.Monsters.Any(value => value.EntityId == monster.EntityId && value.Alive))
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: monster {monster.EntityId} died or disappeared.");
        foreach (var monster in after.Monsters.Where(value => !value.Alive))
            if (!before.Monsters.Any(value => value.EntityId == monster.EntityId && !value.Alive))
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: monster {monster.EntityId} entered its death state.");
        foreach (var original in before.Objects.Where(value => value.EntityId != sarcophagus)) {
            var current = after.Objects.SingleOrDefault(value => value.EntityId == original.EntityId);
            if (current is null || current.Activated != original.Activated || current.Destroyed != original.Destroyed
                || current.CathedralDoor?.DoorState != original.CathedralDoor?.DoorState
                || current.NativeObjectId is 42 or 43 && current.AnimationFrame != original.AnimationFrame)
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: other object {original.EntityId} changed operated/opened state or disappeared.");
        }
        foreach (var current in after.Objects.Where(value => value.EntityId != sarcophagus && (value.Activated || value.Destroyed
                || value.CathedralDoor is { DoorState: not 0 })))
            if (!before.Objects.Any(value => value.EntityId == current.EntityId))
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: other object {current.EntityId} appeared in an operated/opened state.");
        foreach (var original in before.Players) {
            var current = after.Players.SingleOrDefault(value => value.EntityId == original.EntityId);
            var owned = current is null ? [] : CarriedItems(current);
            if (original.Gold > (current?.Gold ?? 0)
                || CarriedItems(original).Any(value => !owned.TryGetValue(value.Key, out var count) || count < value.Value))
                throw new InvalidOperationException($"Ambiguous sarcophagus loot: player {original.EntityId} lost an inventory/hand item or Gold.");
        }
    }

    private static Dictionary<(uint Seed, uint StableId, uint CreateInfo, int Index, int UniqueId), long> CarriedItems(PlayerSnapshot player)
    {
        var items = player.Inventory.Select(value => (value.ItemSeed, value.State))
            .Concat(player.Equipment.Select(value => (value.ItemSeed, value.State)))
            .Concat(player.Belt.Select(value => (value.ItemSeed, value.State)))
            .Concat(player.Stash.Select(value => (value.Item.ItemSeed, value.Item.State)));
        if (player.HeldItem is { } held) items = items.Append((held.ItemSeed, held.State));
        var result = new Dictionary<(uint, uint, uint, int, int), long>();
        foreach (var (seed, state) in items) {
            var key = (seed, state?.StableItemId ?? 0, state?.CreateInfo ?? 0, state?.ItemIndex ?? 0, state?.UniqueId ?? 0);
            var amount = state?.PresentationKind == Devilution.Protocol.V1.ItemPresentationKind.Gold ? Math.Max(1L, state.Value) : 1;
            result[key] = result.GetValueOrDefault(key) + amount;
        }
        return result;
    }

    // items.cpp GetSuperItemSpace: when GetItemSpace exhausts the 3x3 area,
    // take the first free cell in full-square scans k=2..49, y then x.
    // Items publish no source object, so a distant drop needs this placement proof.
    private static bool IsNativeFallbackDrop(Snapshot before, WorldItemSnapshot item, ObjectSnapshot target)
    {
        const int width = 112;
        var geometry = before.LevelGeometry.SingleOrDefault(value => value.LevelId == target.LevelId);
        if (geometry is null || geometry.Width != width || geometry.Height != width
            || (uint)target.PositionX >= width || (uint)target.PositionY >= width
            || (uint)item.PositionX >= width || (uint)item.PositionY >= width
            || Math.Max(Math.Abs((long)item.PositionX - target.PositionX), Math.Abs((long)item.PositionY - target.PositionY)) >= 50)
            return false;
        var blocked = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        var occupied = before.WorldItems.Where(value => value.LevelId == target.LevelId)
            .Select(value => (value.PositionX, value.PositionY)).ToHashSet();
        occupied.UnionWith(before.Players.Where(value => value.LevelId == target.LevelId && value.Life > 0)
            .Select(value => (value.PositionX, value.PositionY)));
        foreach (var monster in before.Monsters.Where(value => value.LevelId == target.LevelId
                && (value.Alive || value.DeathTicksRemaining > 0 || value.Petrified))) {
            occupied.Add((monster.PositionX, monster.PositionY));
            if (monster.Moving) occupied.Add((monster.FuturePositionX, monster.FuturePositionY));
        }
        // OperateSarcophagus clears its selection before item placement.
        var interactive = before.Objects.Where(value => value.LevelId == target.LevelId && value.EntityId != target.EntityId
                && !value.Destroyed && (AuthoritativeObjectSelector.HasAuthoritativeInteraction(value) || value.NativeBarrelMeleeBreakable))
            .Select(value => (value.PositionX, value.PositionY)).ToHashSet();
        var largeInteractive = interactive.ToHashSet();
        foreach (var value in before.Objects.Where(value => value.LevelId == target.LevelId
                && interactive.Contains((value.PositionX, value.PositionY)))) {
            if (value.NativeObjectId is 48 or 76 or 81) // AddSarcophagus / AddLargeFountain negative references.
                largeInteractive.Add((value.PositionX, value.PositionY - 1));
            if (value.NativeObjectId is 76 or 81) {
                largeInteractive.Add((value.PositionX - 1, value.PositionY));
                largeInteractive.Add((value.PositionX - 1, value.PositionY - 1));
            }
        }
        bool ItemSpaceOk(int x, int y)
            => (uint)x < width && (uint)y < width && !blocked.Contains(y * width + x) && !occupied.Contains((x, y))
                && !largeInteractive.Contains((x + 1, y + 1))
                && !(interactive.Contains((x + 1, y)) && interactive.Contains((x, y + 1)));
        for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
                if (ItemSpaceOk(target.PositionX + x, target.PositionY + y)) return false;
        for (var radius = 2; radius < 50; radius++)
            for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++) {
                    var candidateX = target.PositionX + x; var candidateY = target.PositionY + y;
                    if (ItemSpaceOk(candidateX, candidateY))
                        return item.PositionX == candidateX && item.PositionY == candidateY;
                }
        return false;
    }

    private static bool IsNear(int x, int y, ObjectSnapshot target)
        => Math.Max(Math.Abs((long)x - target.PositionX), Math.Abs((long)y - target.PositionY)) <= 1;
    private static bool IsRequiredTarget(ObjectSnapshot target)
        => target.LevelId == NativeDepthResumeEvidence.TrappedSarcophagusLevel && target.NativeObjectId == 48
            && target.PositionX == NativeDepthResumeEvidence.TrappedSarcophagusX && target.PositionY == NativeDepthResumeEvidence.TrappedSarcophagusY;
    private static bool IsExpectedSource(ObjectSnapshot source, ObjectSnapshot target)
        => !IsRequiredTarget(target) || source.NativeObjectId == 54 && source.PositionX == 33 && source.PositionY == 44;

    private static bool SameObject(ObjectSnapshot value, ObjectSnapshot original)
        => value.EntityId == original.EntityId && value.LevelId == original.LevelId && value.NativeObjectId == original.NativeObjectId
            && value.PositionX == original.PositionX && value.PositionY == original.PositionY;
    private static bool IsOpened(ObjectSnapshot? value, ObjectSnapshot original)
        => value is not null && SameObject(value, original) && value.NativeObjectId == 48 && value.Activated
            && !value.Destroyed && !value.NativeSarcophagusInteractable && !value.TrapArmed
            && value.AnimationFrame >= original.AnimationFrame;
    private static Snapshot Decode(string value) => Snapshot.Parser.ParseFrom(Convert.FromBase64String(value));
    private static string Encode(Snapshot value, uint level, bool includePlacement = false, bool includeLootProof = false)
    {
        var result = new Snapshot {Tick = value.Tick};
        result.Objects.Add(value.Objects.Where(item => includeLootProof || item.LevelId == level));
        result.Projectiles.Add(value.Projectiles.Where(item => includeLootProof || item.LevelId == level));
        result.WorldItems.Add(value.WorldItems.Where(item => includeLootProof || item.LevelId == level));
        result.Monsters.Add(value.Monsters.Where(item => includeLootProof || item.LevelId == level));
        result.Corpses.Add(value.Corpses.Where(item => includeLootProof || item.LevelId == level));
        if (includeLootProof) {
            foreach (var item in value.Players) {
                var player = new PlayerSnapshot {EntityId = item.EntityId, LevelId = item.LevelId,
                    PositionX = item.PositionX, PositionY = item.PositionY, Life = item.Life, Gold = item.Gold, HeldItem = item.HeldItem};
                player.Inventory.Add(item.Inventory); player.Equipment.Add(item.Equipment);
                player.Belt.Add(item.Belt); player.Stash.Add(item.Stash);
                result.Players.Add(player);
            }
        }
        if (includePlacement) {
            foreach (var geometry in value.LevelGeometry.Where(item => item.LevelId == level)) {
                var placement = new LevelGeometrySnapshot {LevelId = level, Width = geometry.Width, Height = geometry.Height};
                placement.BlockedCells.Add(geometry.BlockedCells);
                placement.StaticObjectBlockedCells.Add(geometry.StaticObjectBlockedCells);
                result.LevelGeometry.Add(placement);
            }
        }
        return Convert.ToBase64String(result.ToByteArray());
    }
}

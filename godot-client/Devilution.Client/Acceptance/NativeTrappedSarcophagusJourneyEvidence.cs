using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

internal sealed record NativeTrappedSarcophagusCheckpoint(uint Level, uint Entity, int X, int Y,
    uint TrapSource, ulong OpeningTick, ulong FiringTick, ulong Sequence, ulong DeadlineTick,
    uint TerminalFrame, string Before, string After, uint[] Drops, uint[] Skeletons);

/** Opening provenance survives sparse snapshots through the wall trap's retained firing tick. */
internal static class NativeTrappedSarcophagusJourneyEvidence
{
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
            if (!CanOpen(target) || openingTick < before.Tick || deadlineTick <= openingTick || after.Tick <= openingTick)
                return null;
            var opened = after.Objects.SingleOrDefault(value => value.EntityId == target.EntityId);
            if (!IsOpened(opened, target)) return null;
            foreach (var source in after.Objects.Where(value => value.NativeTrapFiredTriggerEntityId == target.EntityId
                    && value.HasNativeTrapFiredTick && value.NativeTrapFiredTick > openingTick
                    && value.NativeTrapFiredTick < deadlineTick && value.NativeTrapFiredTick <= after.Tick)) {
                var original = before.Objects.SingleOrDefault(value => value.EntityId == source.EntityId);
                if (!spent.Contains(source.EntityId) && source.NativeObjectId is 53 or 54 && !source.Destroyed && !source.TrapArmed && !source.NativeTrapInteractable
                    && source.LevelId == target.LevelId && original is not null && SameObject(source, original))
                    return new(source.EntityId, source.NativeTrapFiredTick);
            }
            return null;
        }
    }

    internal static bool CanOpen(ObjectSnapshot value)
        => value.NativeObjectId == 48 && value.LevelId is >= 1 and <= 4 && value.TrapArmed
            && value.NativeSarcophagusInteractable && !value.Activated && !value.Destroyed;

    internal static NativeTrappedSarcophagusCheckpoint Capture(Snapshot before, Snapshot after, uint entity,
        CommandResult receipt, ulong deadlineTick, uint terminalFrame)
    {
        if (!AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt) || receipt.ClientSequence == 0)
            throw new InvalidOperationException("The trapped sarcophagus has no accepted opening receipt.");
        var firing = new Opening(before, entity).Find(after, receipt.AppliedTick, deadlineTick)
            ?? throw new InvalidOperationException("The trapped sarcophagus has no fresh firing after its opening command.");
        var target = before.Objects.Single(value => value.EntityId == entity);
        var drops = after.WorldItems.Where(value => value.LevelId == target.LevelId
            && !before.WorldItems.Any(old => old.EntityId == value.EntityId)).Select(value => value.EntityId).ToArray();
        var skeletons = after.Monsters.Where(value => IsReleasedSkeleton(before, after, value, target.LevelId))
            .Select(value => value.EntityId).ToArray();
        var result = new NativeTrappedSarcophagusCheckpoint(target.LevelId, entity, target.PositionX, target.PositionY,
            firing.TrapSource, receipt.AppliedTick, firing.Tick, receipt.ClientSequence, deadlineTick, terminalFrame,
            Encode(before, target.LevelId), Encode(after, target.LevelId), drops, skeletons);
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
        foreach (var id in expected.Drops) {
            var item = after.WorldItems.Single(value => value.EntityId == id);
            if (before.WorldItems.Any(value => value.EntityId == id))
                throw new InvalidOperationException("The sarcophagus reused preexisting floor loot as release evidence.");
            NativeThemeObjectJourneyEvidence.VerifyDrop(item, expected.Level);
        }
        foreach (var id in expected.Skeletons) {
            var monster = after.Monsters.Single(value => value.EntityId == id);
            if (!IsReleasedSkeleton(before, after, monster, expected.Level))
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
            if (!snapshot.Monsters.Any(value => value.EntityId == id && value.LevelId == expected.Level && value.Alive)
                && !snapshot.Corpses.Any(value => value.EntityId == id && value.LevelId == expected.Level))
                throw new InvalidOperationException("The released sarcophagus skeleton or its combat corpse did not survive resume.");
    }

    // A sparse publication may show a newly released skeleton already killed by its wall trap.
    private static bool IsReleasedSkeleton(Snapshot before, Snapshot after, MonsterSnapshot value, uint level)
        => value.EntityId != 0 && value.LevelId == level && !value.IsPlayerMinion
            && value.NativeAi is "SkeletonMelee" or "SkeletonRanged"
            && (value.Alive || after.Corpses.Any(corpse => corpse.EntityId == value.EntityId && corpse.LevelId == level))
            && !before.Monsters.Any(old => old.EntityId == value.EntityId && old.Alive)
            && !before.Corpses.Any(old => old.EntityId == value.EntityId);

    private static bool SameObject(ObjectSnapshot value, ObjectSnapshot original)
        => value.EntityId == original.EntityId && value.LevelId == original.LevelId && value.NativeObjectId == original.NativeObjectId
            && value.PositionX == original.PositionX && value.PositionY == original.PositionY;
    private static bool IsOpened(ObjectSnapshot? value, ObjectSnapshot original)
        => value is not null && SameObject(value, original) && value.NativeObjectId == 48 && value.Activated
            && !value.Destroyed && !value.NativeSarcophagusInteractable && !value.TrapArmed
            && value.AnimationFrame >= original.AnimationFrame;
    private static Snapshot Decode(string value) => Snapshot.Parser.ParseFrom(Convert.FromBase64String(value));
    private static string Encode(Snapshot value, uint level)
    {
        var result = new Snapshot {Tick = value.Tick};
        result.Objects.Add(value.Objects.Where(item => item.LevelId == level));
        result.Projectiles.Add(value.Projectiles.Where(item => item.LevelId == level));
        result.WorldItems.Add(value.WorldItems.Where(item => item.LevelId == level));
        result.Monsters.Add(value.Monsters.Where(item => item.LevelId == level));
        result.Corpses.Add(value.Corpses.Where(item => item.LevelId == level));
        return Convert.ToBase64String(result.ToByteArray());
    }
}

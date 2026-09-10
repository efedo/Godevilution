using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** One content-declared step in a native quest-sequence layout variant. */
public sealed record AuthoritativeQuestSequenceVariantStep(
    uint VariantId,
    uint QuestId,
    int SlotIndex,
    uint ObjectEntityId,
    int Step);

/**
 * Validated deterministic alternatives for native ordered quest objects.
 *
 * Native set-piece builders may assign the same ordered steps to fixed object
 * slots using a gameplay RNG draw. Keeping the alternatives in content lets
 * the authoritative module reproduce that choice without embedding object
 * identities or layout permutations in gameplay code.
 */
public sealed class AuthoritativeQuestSequenceVariantCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "variant_id",
        "quest_id",
        "quest_symbol",
        "slot_index",
        "object_entity_id",
        "step",
    ];

    private readonly IReadOnlyList<AuthoritativeQuestSequenceVariantStep> definitions;

    public AuthoritativeQuestSequenceVariantCatalog(
        IEnumerable<AuthoritativeQuestSequenceVariantStep> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(step =>
                step.VariantId == 0
                || step.QuestId == 0
                || step.SlotIndex <= 0
                || step.ObjectEntityId == 0
                || step.Step <= 0)
            || values.Select(step => (step.VariantId, step.SlotIndex)).Distinct().Count()
                != values.Length) {
            throw new InvalidDataException(
                "Quest-sequence variants contain invalid or duplicate steps.");
        }

        var grouped = values
            .GroupBy(step => (step.QuestId, step.VariantId))
            .Select(group => (Key: group.Key, Steps: group.OrderBy(step => step.SlotIndex).ToArray()))
            .OrderBy(group => group.Key.QuestId)
            .ThenBy(group => group.Key.VariantId)
            .ToArray();
        foreach (var group in grouped) {
            var slotCount = group.Steps.Length;
            if (group.Steps.Select(step => step.SlotIndex).SequenceEqual(
                    Enumerable.Range(1, slotCount))
                && group.Steps.Select(step => step.Step).OrderBy(step => step)
                    .SequenceEqual(Enumerable.Range(1, slotCount))) {
                continue;
            }
            throw new InvalidDataException(
                $"Quest {group.Key.QuestId} variant {group.Key.VariantId} must contain "
                + "contiguous slots and a complete step permutation.");
        }

        foreach (var questGroup in grouped.GroupBy(group => group.Key.QuestId)) {
            var first = questGroup.First().Steps;
            var expectedObjects = first.Select(step => step.ObjectEntityId).OrderBy(id => id).ToArray();
            var expectedSlots = first.Length;
            foreach (var variant in questGroup) {
                if (variant.Steps.Length != expectedSlots
                    || !variant.Steps.Select(step => step.ObjectEntityId).OrderBy(id => id)
                        .SequenceEqual(expectedObjects)) {
                    throw new InvalidDataException(
                        $"Quest {questGroup.Key} variants must use the same object slots.");
                }
            }
        }

        this.definitions = values
            .OrderBy(step => step.QuestId)
            .ThenBy(step => step.VariantId)
            .ThenBy(step => step.SlotIndex)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeQuestSequenceVariantStep> Definitions
        => definitions;

    public bool IsEmpty => definitions.Count == 0;

    /** Validates that every content step targets an authored quest object. */
    public void ValidateReferences(
        IEnumerable<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(quests);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var questsById = quests.ToDictionary(quest => quest.QuestId);
        foreach (var group in definitions.GroupBy(step => step.QuestId)) {
            if (!questsById.ContainsKey(group.Key))
                throw new InvalidDataException(
                    $"Quest-sequence variants reference unknown quest {group.Key}.");
            var levels = new HashSet<uint>();
            foreach (var step in group) {
                if (!objectsById.TryGetValue(step.ObjectEntityId, out var @object))
                    throw new InvalidDataException(
                        $"Quest-sequence variant {step.VariantId} references unknown object "
                        + $"{step.ObjectEntityId}.");
                if (@object.QuestId != step.QuestId
                    || @object.EffectKind != AuthoritativeObjectEffectKind.QuestSequence) {
                    throw new InvalidDataException(
                        $"Quest-sequence variant {step.VariantId} object {step.ObjectEntityId} "
                        + "does not match its authored quest-sequence behavior.");
                }
                levels.Add(@object.LevelId);
            }
            if (levels.Count != 1)
                throw new InvalidDataException(
                    $"Quest {group.Key} quest-sequence objects must share one level.");
            var questObjects = group.Select(step => objectsById[step.ObjectEntityId]).Distinct().ToArray();
            if (questObjects.Count(@object =>
                    @object.QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
                    || @object.QuestSequenceFollowUpWorldMutationId != 0) > 1) {
                throw new InvalidDataException(
                    $"Quest {group.Key} quest-sequence variants may have at most one authored follow-up.");
            }
        }
    }

    /** Selects one native-style variant per quest and updates object steps atomically. */
    public void Apply(
        IReadOnlyList<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeQuestState> quests,
        uint randomSeed)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(quests);
        if (IsEmpty)
            return;
        ValidateReferences(objects, quests);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var random = new DeterministicLegacyRandom(randomSeed);
        var updates = new List<(AuthoritativeWorldObject Object, int Step)>();
        foreach (var questGroup in definitions.GroupBy(step => step.QuestId).OrderBy(group => group.Key)) {
            var variants = questGroup
                .GroupBy(step => step.VariantId)
                .OrderBy(group => group.Key)
                .ToArray();
            var selected = variants[random.GenerateRnd(variants.Length)];
            updates.AddRange(selected.Select(step => (objectsById[step.ObjectEntityId], step.Step)));

            var variantObjects = selected
                .Select(step => objectsById[step.ObjectEntityId])
                .Distinct()
                .ToArray();
            var authoredFollowUp = variantObjects.SingleOrDefault(@object =>
                @object.QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
                || @object.QuestSequenceFollowUpWorldMutationId != 0);
            var terminalStep = selected.Single(
                step => step.Step == selected.Max(candidate => candidate.Step));
            var terminalObject = objectsById[terminalStep.ObjectEntityId];
            if (authoredFollowUp is not null && authoredFollowUp != terminalObject) {
                var targetX = checked(authoredFollowUp.PositionX + authoredFollowUp.QuestSequenceFollowUpOffsetX);
                var targetY = checked(authoredFollowUp.PositionY + authoredFollowUp.QuestSequenceFollowUpOffsetY);
                var followUpOffsetX = authoredFollowUp.QuestSequenceFollowUpKind
                    is AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                    or AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                    ? checked(targetX - terminalObject.PositionX)
                    : 0;
                var followUpOffsetY = authoredFollowUp.QuestSequenceFollowUpKind
                    is AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                    or AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                    ? checked(targetY - terminalObject.PositionY)
                    : 0;
                if (followUpOffsetX is < -32 or > 32 || followUpOffsetY is < -32 or > 32)
                    throw new InvalidDataException(
                        $"Quest {questGroup.Key} terminal follow-up target is outside native offset bounds.");
                terminalObject.QuestSequenceFollowUpKind = authoredFollowUp.QuestSequenceFollowUpKind;
                terminalObject.QuestSequenceFollowUpAmount = authoredFollowUp.QuestSequenceFollowUpAmount;
                terminalObject.QuestSequenceFollowUpOffsetX = followUpOffsetX;
                terminalObject.QuestSequenceFollowUpOffsetY = followUpOffsetY;
                terminalObject.QuestSequenceFollowUpWorldMutationId = authoredFollowUp.QuestSequenceFollowUpWorldMutationId;
                terminalObject.ProjectileSourceKind = authoredFollowUp.ProjectileSourceKind;
                authoredFollowUp.QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.None;
                authoredFollowUp.QuestSequenceFollowUpAmount = 0;
                authoredFollowUp.QuestSequenceFollowUpOffsetX = 0;
                authoredFollowUp.QuestSequenceFollowUpOffsetY = 0;
                authoredFollowUp.QuestSequenceFollowUpWorldMutationId = 0;
            }
        }
        foreach (var update in updates)
            update.Object.EffectAmount = update.Step;
    }

    /** Rejects partial quest-sequence variant metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest-sequence variant table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("variant_id");
            _ = row.Required("quest_id");
            _ = row.Required("slot_index");
            _ = row.Required("object_entity_id");
            _ = row.Required("step");
        }
    }

    public static AuthoritativeQuestSequenceVariantCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeQuestSequenceVariantCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestSequenceVariantStep(
                row.RequiredUInt32("variant_id"),
                AuthoritativeQuestIdentity.Resolve(row, stableIds),
                row.RequiredInt32("slot_index"),
                row.RequiredUInt32("object_entity_id"),
                row.RequiredInt32("step"))));
    }
}

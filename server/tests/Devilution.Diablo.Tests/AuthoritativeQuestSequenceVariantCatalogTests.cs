using Devilution.Diablo;
using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestSequenceVariantCatalogTests
{
    [Fact]
    public void AppliesNativePermutationFromDeterministicSeed()
    {
        var catalog = new AuthoritativeQuestSequenceVariantCatalog([
            new(1, 7, 1, 101, 1),
            new(1, 7, 2, 102, 2),
            new(1, 7, 3, 103, 3),
            new(2, 7, 1, 101, 3),
            new(2, 7, 2, 102, 2),
            new(2, 7, 3, 103, 1),
        ]);
        var firstObjects = CreateObjects();
        var secondObjects = CreateObjects();
        var quests = new[] { new AuthoritativeQuestState(7, 24, 1) };

        catalog.Apply(firstObjects, quests, randomSeed: 0);
        catalog.Apply(secondObjects, quests, randomSeed: 0);

        Assert.Equal(
            firstObjects.Select(@object => @object.EffectAmount),
            secondObjects.Select(@object => @object.EffectAmount));
        Assert.Equal([1, 2, 3], firstObjects.Select(@object => @object.EffectAmount));
    }

    [Fact]
    public void DifferentNativeSeedCanSelectAnotherPermutation()
    {
        var catalog = new AuthoritativeQuestSequenceVariantCatalog([
            new(1, 7, 1, 101, 1),
            new(1, 7, 2, 102, 2),
            new(1, 7, 3, 103, 3),
            new(2, 7, 1, 101, 3),
            new(2, 7, 2, 102, 2),
            new(2, 7, 3, 103, 1),
        ]);
        var objects = CreateObjects();

        catalog.Apply(objects, [new AuthoritativeQuestState(7, 24, 1)], randomSeed: 6);

        Assert.Equal([3, 2, 1], objects.Select(@object => @object.EffectAmount));
    }

    [Fact]
    public void RejectsIncompleteVariantPermutation()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeQuestSequenceVariantCatalog([
                new(1, 7, 1, 101, 1),
                new(1, 7, 3, 103, 3),
            ]));
    }

    [Fact]
    public void MovesTerminalFollowUpWithTheSelectedFinalStep()
    {
        var catalog = new AuthoritativeQuestSequenceVariantCatalog([
            new(1, 7, 1, 101, 1),
            new(1, 7, 2, 102, 2),
            new(1, 7, 3, 103, 3),
            new(2, 7, 1, 101, 3),
            new(2, 7, 2, 102, 2),
            new(2, 7, 3, 103, 1),
        ]);
        var objects = CreateObjects();
        objects[2].QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster;
        objects[2].QuestSequenceFollowUpAmount = 139;
        objects[2].QuestSequenceFollowUpOffsetX = 1;

        catalog.Apply(objects, [new AuthoritativeQuestState(7, 24, 1)], randomSeed: 6);

        Assert.Equal(AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster, objects[0].QuestSequenceFollowUpKind);
        Assert.Equal(139U, objects[0].QuestSequenceFollowUpAmount);
        Assert.Equal(10, objects[0].PositionX + objects[0].QuestSequenceFollowUpOffsetX);
        Assert.Equal(AuthoritativeQuestSequenceFollowUpKind.None, objects[2].QuestSequenceFollowUpKind);
    }

    [Fact]
    public void RejectsOutOfRangeVariantDrawBeforeMutatingObjectsOrQuests()
    {
        var catalog = CreateSixWayCatalog();
        var objects = CreateObjects();
        objects[2].QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster;
        objects[2].QuestSequenceFollowUpAmount = 139;
        objects[2].QuestSequenceFollowUpOffsetX = 1;
        var quests = new[] { new AuthoritativeQuestState(7, 24, 3, progress: 1, variable1: 4, variable2: 9) };
        var objectsBefore = JsonSerializer.Serialize(objects);
        var questsBefore = JsonSerializer.Serialize(quests);

        var exception = Assert.Throws<InvalidDataException>(() =>
            catalog.Apply(objects, quests, randomSeed: 1457187811U));

        Assert.Equal(
            "Quest 7 quest-sequence selection index -2 is outside the valid range [0, 6).",
            exception.Message);
        Assert.Equal(objectsBefore, JsonSerializer.Serialize(objects));
        Assert.Equal(questsBefore, JsonSerializer.Serialize(quests));
    }

    [Fact]
    public void RejectsLaterOutOfRangeVariantDrawBeforeMutatingAnyQuestGroup()
    {
        var definitions = CreateSixWayDefinitions(7, 101, 1).Concat(CreateSixWayDefinitions(8, 104, 7));
        var catalog = new AuthoritativeQuestSequenceVariantCatalog(definitions);
        var objects = CreateObjects().Concat(CreateObjects(8, 104)).ToArray();
        objects[2].QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster;
        objects[2].QuestSequenceFollowUpAmount = 139;
        objects[2].QuestSequenceFollowUpOffsetX = 1;
        var quests = new[] {
            new AuthoritativeQuestState(7, 24, 3, progress: 1, variable1: 4, variable2: 9),
            new AuthoritativeQuestState(8, 24, 3, progress: 2, variable1: 5, variable2: 10),
        };
        var objectsBefore = JsonSerializer.Serialize(objects);
        var questsBefore = JsonSerializer.Serialize(quests);

        var exception = Assert.Throws<InvalidDataException>(() =>
            catalog.Apply(objects, quests, randomSeed: 988045466U));

        Assert.Equal(
            "Quest 8 quest-sequence selection index -2 is outside the valid range [0, 6).",
            exception.Message);
        Assert.Equal(objectsBefore, JsonSerializer.Serialize(objects));
        Assert.Equal(questsBefore, JsonSerializer.Serialize(quests));
    }

    [Fact]
    public void AppliesValidSixWaySelectionsWithTheirExistingResults()
    {
        var catalog = CreateSixWayCatalog();
        var seedZeroObjects = CreateObjects();
        var fifthVariantObjects = CreateObjects();

        catalog.Apply(seedZeroObjects, [new AuthoritativeQuestState(7, 24, 1)], randomSeed: 0);
        catalog.Apply(fifthVariantObjects, [new AuthoritativeQuestState(7, 24, 1)], randomSeed: 988045466U);

        Assert.Equal([1, 2, 3], seedZeroObjects.Select(@object => @object.EffectAmount));
        Assert.Equal([3, 2, 1], fifthVariantObjects.Select(@object => @object.EffectAmount));
    }

    private static AuthoritativeQuestSequenceVariantCatalog CreateSixWayCatalog()
        => new(CreateSixWayDefinitions(7, 101, 1));

    private static IEnumerable<AuthoritativeQuestSequenceVariantStep> CreateSixWayDefinitions(
        uint questId,
        uint firstObjectId,
        uint firstVariantId)
    {
        var permutations = new[] {
            new[] { 1, 2, 3 },
            new[] { 1, 3, 2 },
            new[] { 2, 1, 3 },
            new[] { 2, 3, 1 },
            new[] { 3, 2, 1 },
            new[] { 3, 1, 2 },
        };
        return permutations.SelectMany((steps, variantIndex) =>
            steps.Select((step, slotIndex) => new AuthoritativeQuestSequenceVariantStep(
                checked(firstVariantId + (uint)variantIndex),
                questId,
                slotIndex + 1,
                checked(firstObjectId + (uint)slotIndex),
                step)));
    }
    private static AuthoritativeWorldObject[] CreateObjects(uint questId = 7, uint firstObjectId = 101)
        => [
            CreateObject(firstObjectId, questId),
            CreateObject(checked(firstObjectId + 1), questId),
            CreateObject(checked(firstObjectId + 2), questId),
        ];

    private static AuthoritativeWorldObject CreateObject(uint entityId, uint questId = 7)
        => new(
            entityId,
            entityId,
            24,
            checked(5 + (int)(entityId - 101) * 2),
            10,
            questId: questId,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 99);
}

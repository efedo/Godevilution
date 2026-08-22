using Devilution.Diablo;
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

    private static AuthoritativeWorldObject[] CreateObjects()
        => [
            CreateObject(101),
            CreateObject(102),
            CreateObject(103),
        ];

    private static AuthoritativeWorldObject CreateObject(uint entityId)
        => new(
            entityId,
            entityId,
            24,
            checked(5 + (int)(entityId - 101) * 2),
            10,
            questId: 7,
            effectKind: AuthoritativeObjectEffectKind.QuestSequence,
            effectAmount: 99);
}

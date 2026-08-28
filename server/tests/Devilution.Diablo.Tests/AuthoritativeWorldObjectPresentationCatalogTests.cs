using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldObjectPresentationCatalogTests
{
    [Fact]
    public void LoadsAndAppliesOptionalLifecycleMetadata()
    {
        var objectState = new AuthoritativeWorldObject(
            entityId: 10,
            objectId: 4,
            levelId: 1,
            positionX: 2,
            positionY: 3);
        var catalog = AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
            "object_presentations.tsv",
            "entity_id\thide_when_activated\tanimation_frame\tactivated_animation_frame\n"
                + "10\tfalse\t3\t7\n");

        catalog.Apply([objectState]);

        Assert.False(objectState.HideWhenActivated);
        Assert.Equal(3, objectState.AnimationFrame);
        Assert.Equal(7, objectState.ActivatedAnimationFrame);
    }

    [Fact]
    public void DefaultsOmittedValuesToLegacyLifecycle()
    {
        var catalog = AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
            "object_presentations.tsv",
            "entity_id\n10\n");
        var objectState = new AuthoritativeWorldObject(
            entityId: 10,
            objectId: 4,
            levelId: 1,
            positionX: 2,
            positionY: 3);

        catalog.Apply([objectState]);

        Assert.True(objectState.HideWhenActivated);
        Assert.Equal(0, objectState.AnimationFrame);
        Assert.Equal(0, objectState.ActivatedAnimationFrame);
    }

    [Fact]
    public void RejectsDuplicateAndUnknownDefinitions()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
                "object_presentations.tsv",
                "entity_id\n10\n10\n"));

        var catalog = new AuthoritativeWorldObjectPresentationCatalog([
            new AuthoritativeWorldObjectPresentationDefinition(11),
        ]);
        var objectState = new AuthoritativeWorldObject(
            entityId: 10,
            objectId: 4,
            levelId: 1,
            positionX: 2,
            positionY: 3);

        Assert.Throws<InvalidDataException>(() => catalog.Apply([objectState]));
        Assert.True(objectState.HideWhenActivated);
    }

    [Fact]
    public void RejectsAnimationFramesOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
                "object_presentations.tsv",
                "entity_id\tanimation_frame\n10\t256\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeWorldObjectPresentationCatalog([
                new AuthoritativeWorldObjectPresentationDefinition(
                    10,
                    ActivatedAnimationFrame: -1),
            ]));
    }

}

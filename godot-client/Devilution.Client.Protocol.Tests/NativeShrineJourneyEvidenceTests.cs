using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeShrineJourneyEvidenceTests
{
    [Theory]
    [InlineData(0U)]
    [InlineData(13U)]
    [InlineData(19U)]
    [InlineData(20U)]
    [InlineData(21U)]
    public void AttributeEvidenceRequiresAChangeAndSurvivesResourceAndPositionChanges(uint type)
    {
        var before = Player(); var after = before.Clone();
        var shrine = new ObjectSnapshot { NativeShrineInteractable = true, NativeShrineType = type, LevelId = 5 };
        Assert.True(NativeShrineJourneyEvidence.CanDemonstrate(shrine, before));
        Assert.False(NativeShrineJourneyEvidence.Changed(type, 5, before, after));
        after.Attributes.Strength.Base++;
        Assert.True(NativeShrineJourneyEvidence.Changed(type, 5, before, after));
        var resumed = after.Clone(); resumed.Mana = 99; resumed.Life = 1; resumed.PositionX = 80; resumed.LevelId = 0;
        Assert.True(NativeShrineJourneyEvidence.Retained(type, 5, after, resumed));
        resumed.Attributes.Strength.Base--;
        Assert.False(NativeShrineJourneyEvidence.Retained(type, 5, after, resumed));
    }

    [Theory]
    [InlineData(9U, 1U)]
    [InlineData(16U, 4U)]
    [InlineData(23U, 22U)]
    public void WisdomEvidenceRetainsTheNativeKnownSpellChange(uint type, uint spell)
    {
        var before = Player(); var after = before.Clone();
        after.MemorizedSpells.Add(new PlayerSpellLevelSnapshot { SpellId = spell, Level = 2 });
        Assert.True(NativeShrineJourneyEvidence.Changed(type, 5, before, after));
        Assert.True(NativeShrineJourneyEvidence.Retained(type, 5, after, after.Clone()));
        Assert.False(NativeShrineJourneyEvidence.Retained(type, 5, after, before));
    }

    [Fact]
    public void SecludedEvidenceRequiresTheRevealedFloorAndDoesNotAcceptAnotherMap()
    {
        var before = Player(); var after = before.Clone(); after.FullyExploredLevelIds.Add(5);
        var shrine = new ObjectSnapshot { NativeShrineInteractable = true, NativeShrineType = 22, LevelId = 5 };
        Assert.True(NativeShrineJourneyEvidence.CanDemonstrate(shrine, before));
        Assert.False(NativeShrineJourneyEvidence.CanDemonstrate(shrine, after));
        Assert.True(NativeShrineJourneyEvidence.Changed(22, 5, before, after));
        Assert.True(NativeShrineJourneyEvidence.Retained(22, 5, after, after.Clone()));
        after.FullyExploredLevelIds.Clear(); after.FullyExploredLevelIds.Add(6);
        Assert.False(NativeShrineJourneyEvidence.Retained(22, 5, before, after));
    }

    [Fact]
    public void SeededMysteriousRequiresItsKnownObjectAndEveryBaseAttributeSurvivesWireResume()
    {
        var shrine = new ObjectSnapshot { NativeObjectId = 59, NativeShrineType = 0, LevelId = 5,
            PositionX = 81, PositionY = 65, NativeShrineInteractable = true };
        Assert.True(NativeShrineJourneyEvidence.IsSeededMysterious(shrine));
        var different = shrine.Clone(); different.NativeShrineType = 13;
        Assert.False(NativeShrineJourneyEvidence.IsSeededMysterious(different));
        different = shrine.Clone(); different.PositionX++;
        Assert.False(NativeShrineJourneyEvidence.IsSeededMysterious(different));
        var before = Player(); var after = before.Clone();
        after.Attributes.Strength.Base--; after.Attributes.Magic.Base--;
        after.Attributes.Dexterity.Base--; after.Attributes.Vitality.Base += 5;
        Assert.True(NativeShrineJourneyEvidence.Changed(0, 5, before, after));
        var resumed = PlayerSnapshot.Parser.ParseFrom(after.ToByteArray());
        resumed.LevelId = 0; resumed.PositionX = 75; resumed.PositionY = 68;
        Assert.True(NativeShrineJourneyEvidence.Retained(0, 5, after, resumed));
        foreach (var restoreOne in new Action<PlayerSnapshot>[] {
            value => value.Attributes.Strength.Base = before.Attributes.Strength.Base,
            value => value.Attributes.Magic.Base = before.Attributes.Magic.Base,
            value => value.Attributes.Dexterity.Base = before.Attributes.Dexterity.Base,
            value => value.Attributes.Vitality.Base = before.Attributes.Vitality.Base,
        }) {
            var lost = resumed.Clone(); restoreOne(lost);
            Assert.False(NativeShrineJourneyEvidence.Retained(0, 5, after, lost));
        }
    }

    private static PlayerSnapshot Player() => new() {
        Attributes = new() { Strength = new() { Base = 20 }, Magic = new() { Base = 10 },
            Dexterity = new() { Base = 20 }, Vitality = new() { Base = 20 } },
    };
}

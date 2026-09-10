using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestRouteMessageCatalogTests
{
    [Fact]
    public void TsvLoaderResolvesQuestAndLevelIdentities()
    {
        var catalog = AuthoritativeQuestRouteMessageCatalog.LoadTsv(
            "quest_route_messages.tsv",
            "quest_id\tquest_symbol\tlevel_id\tlevel_symbol\tmessage_id\n"
            + "3021\tdefiler\t17\thellfire_level_17\t286\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal((3021U, 17U, 286), (
            definition.QuestId,
            definition.LevelId,
            definition.MessageId));
    }

    [Fact]
    public void ProductionValidationRejectsPartialSchemasAndUnknownQuests()
    {
        var partial = TsvTable.Parse(
            "quest_route_messages.tsv",
            "quest_id\tlevel_id\tmessage_id\n3021\t17\t286\n");
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeQuestRouteMessageCatalog.ValidateExternalizedProduction(partial));

        var catalog = new AuthoritativeQuestRouteMessageCatalog([
            new(3021, 17, 286),
        ]);
        Assert.Throws<InvalidDataException>(() =>
            catalog.ValidateQuests([
                new AuthoritativeQuestState(3001, 0, 1),
            ]));
    }

    [Fact]
    public void ConstructorRejectsDuplicateQuestRows()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeQuestRouteMessageCatalog([
                new(3021, 17, 286),
                new(3021, 17, 287),
            ]));
    }
}

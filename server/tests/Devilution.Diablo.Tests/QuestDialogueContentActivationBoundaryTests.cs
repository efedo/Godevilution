using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class QuestDialogueContentActivationBoundaryTests
{
    [Theory]
    [InlineData("dialogue_messages.tsv", "Production dialogue-message table")]
    [InlineData("quest_dialogue_stages.tsv", "Production quest-dialogue stage table")]
    [InlineData("quest_sequence_variants.tsv", "Production quest-sequence variant table")]
    [InlineData("quest_towner_interactions.tsv", "Production quest/towner interaction table")]
    [InlineData("towner_greetings.tsv", "Production towner greeting table")]
    public void CurrentSchemaRejectsLegacyOrderedQuestDialogueSchemas(
        string sourcePath,
        string expectedMessage)
    {
        var basePack = LoadBasePack();
        var table = basePack.Tables.Single(candidate => candidate.SourcePath == sourcePath);
        var pack = ReplaceBaseTable(basePack, sourcePath, ReverseColumns(table));

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "legacy-quest-dialogue-schema",
                    "1",
                    [pack]))));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsLegacyOrderedQuestDialogueCompatibility()
    {
        var basePack = LoadBasePack();
        var tables = basePack.Tables
            .Select(table => table.SourcePath is
                "dialogue_messages.tsv" or
                "quest_dialogue_stages.tsv" or
                "quest_sequence_variants.tsv" or
                "quest_towner_interactions.tsv" or
                "towner_greetings.tsv"
                    ? ReverseColumns(table)
                    : table)
            .ToArray();
        var previous = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            basePack.Dependencies,
            basePack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.499.0"),
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-quest-dialogue-contract",
                "1",
                [previous])));

        Assert.NotNull(activation);
    }

    private static ContentPack LoadBasePack()
    {
        var root = FindRepositoryRoot();
        return ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
    }

    private static ContentPack ReplaceBaseTable(
        ContentPack basePack,
        string sourcePath,
        TsvTable replacement)
    {
        return new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Where(table => !string.Equals(
                table.SourcePath,
                sourcePath,
                StringComparison.OrdinalIgnoreCase))
                .Concat([replacement])
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
    }

    private static TsvTable ReverseColumns(TsvTable table)
    {
        var columns = table.Columns.Reverse().ToArray();
        var lines = new List<string> { string.Join('\t', columns) };
        lines.AddRange(table.Rows.Select(row =>
            string.Join('\t', columns.Select(column => row[column]))));
        return TsvTable.Parse(table.SourcePath, string.Join('\n', lines) + "\n");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

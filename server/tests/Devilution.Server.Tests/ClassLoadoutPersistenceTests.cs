using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ClassLoadoutPersistenceTests
{
    [Fact]
    public void ColdImportSelectsSavedClassTemplateBeforeRestoringLoadoutState()
    {
        var activated = ActivateBaseContent();
        var source = CreateExecutor(activated);
        source.InitializeParticipant(
            "persisted-sorcerer",
            new AuthoritativeParticipantSelection(2));
        var expected = source.GetPlayerState("persisted-sorcerer");
        var save = source.ExportPlayerSave("persisted-sorcerer");

        var replacement = CreateExecutor(ActivateBaseContent());
        replacement.ImportPlayerSave("persisted-sorcerer", save);
        var actual = replacement.GetPlayerState("persisted-sorcerer");

        Assert.Equal(2U, actual.CharacterClassId);
        Assert.Equal(expected.LifeMaximum, actual.LifeMaximum);
        Assert.Equal(expected.ManaMaximum, actual.ManaMaximum);
        Assert.Equal(expected.AbilitySpellId, actual.AbilitySpellId);
        Assert.Equal(
            expected.MemorizedSpells.ToArray(),
            actual.MemorizedSpells.ToArray());
        Assert.Equal(
            expected.Equipment.Select(item => item.ItemSeed),
            actual.Equipment.Select(item => item.ItemSeed));
        Assert.Equal(
            expected.Belt.Select(item => item.ItemSeed),
            actual.Belt.Select(item => item.ItemSeed));
        Assert.Equal(
            expected.Inventory.Select(item => item.ItemSeed),
            actual.Inventory.Select(item => item.ItemSeed));
    }

    private static StoreSimulationExecutor CreateExecutor(
        DiabloContentActivation content)
    {
        return new StoreSimulationExecutor(
            content,
            (sessionId, classId) => content.PlayerLoadouts.CreateTemplate(
                sessionId,
                classId ?? content.PlayerClasses.Default.ClassId,
                null,
                null,
                null,
                content.DefaultStartingLevelId));
    }

    private static DiabloContentActivation ActivateBaseContent()
    {
        var contentRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base");
        var manifest = new ContentManifest(
            "devilution.base",
            "0.1.0",
            [ContentPackLoader.LoadDirectory(
                "devilution.base",
                "0.1.0",
                contentRoot)]);
        var registry = new GameplayModuleRegistry();
        registry.Load(
            [new DiabloModule()],
            new GameplayModuleContext(
                manifest,
                new DelegateAuthoritativeClock(() => 0)));
        return registry.GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(ComposedContent.Compose(manifest));
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
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}

using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class ExternalContentCatalogRequirementTests
{
    [Fact]
    public void CompatibilityCallersStillUseEmbeddedCatalogDefaults()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        Assert.NotNull(simulation);
    }

    [Fact]
    public void ProductionActivationRejectsMissingComposedContent()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.Create(
                RequiredOptions(includeContent: false)));

        Assert.Contains("composed external content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionActivationRejectsMissingSpellCatalog()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.Create(
                RequiredOptions(includeSpells: false)));

        Assert.Contains("external spell catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionActivationRejectsMissingCombatRules()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.Create(
                RequiredOptions(includeCombatRules: false)));

        Assert.Contains("external combat rules", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionActivationRejectsMissingItemCatalog()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.Create(RequiredOptions()));

        Assert.Contains("external item catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapEnablesTheProductionRequirement()
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var content = ComposedContent.Compose(new ContentManifest(
            "external-content-bootstrap-test",
            "1",
            [basePack]));
        var factory = new CapturingFactory();
        var bootstrap = new DiabloContentBootstrap(factory);

        Assert.Throws<ActivationCapturedException>(() => bootstrap.Activate(content));
        Assert.NotNull(factory.Options);
        Assert.True(factory.Options!.RequireExternalContentCatalogs);
        Assert.Same(content, factory.Options.Content);
        Assert.NotNull(factory.Options.Items);
        Assert.NotNull(factory.Options.Experience);
        Assert.NotNull(factory.Options.PlayerClasses);
        Assert.NotNull(factory.Options.PlayerAnimations);
        Assert.NotNull(factory.Options.SpellBooks);
        Assert.NotNull(factory.Options.MonsterTypes);
        Assert.NotNull(factory.Options.MonsterMissiles);
        Assert.NotNull(factory.Options.NativeMissiles);
        Assert.NotNull(factory.Options.NativeMissileSprites);
    }

    private static DiabloSimulationOptions RequiredOptions(
        bool includeContent = true,
        bool includeSpells = true,
        bool includeCombatRules = true)
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var content = includeContent
            ? ComposedContent.Compose(new ContentManifest(
                "external-content-requirement-tests",
                "1",
                [basePack]))
            : null;
        var spells = includeSpells
            ? new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0),
            ])
            : null;
        var combatRules = includeCombatRules
            ? new AuthoritativeCombatRules(10, 1, 100)
            : null;
        return new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            Content = content,
            Spells = spells,
            CombatRules = combatRules,
            RequireExternalContentCatalogs = true,
        };
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

    private sealed class CapturingFactory : IDiabloSimulationFactory
    {
        public DiabloSimulationOptions? Options { get; private set; }

        public DiabloSimulation Create(DiabloSimulationOptions options)
        {
            Options = options;
            throw new ActivationCapturedException();
        }
    }

    private sealed class ActivationCapturedException : Exception
    {
    }
}

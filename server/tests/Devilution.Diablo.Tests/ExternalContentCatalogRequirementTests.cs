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

    [Theory]
    [InlineData("Content", "composed external content")]
    [InlineData("WorldRules", "external world rules")]
    [InlineData("Spells", "external spell catalog")]
    [InlineData("CombatRules", "external combat rules")]
    [InlineData("Items", "external item catalog")]
    [InlineData("Experience", "external experience catalog")]
    [InlineData("PlayerClasses", "external player-class catalog")]
    [InlineData("PlayerAnimations", "external player-animation catalog")]
    [InlineData("SpellBooks", "external spell-book catalog")]
    [InlineData("MonsterTypes", "external monster-type catalog")]
    [InlineData("MonsterMissiles", "external monster-missile catalog")]
    [InlineData("NativeMissiles", "external native-missile catalog")]
    [InlineData("NativeMissileSprites", "external native-missile sprite catalog")]
    public void ProductionActivationRejectsEveryMissingRequiredCatalog(
        string missingCatalog,
        string expectedMessage)
    {
        var source = CaptureProductionOptions();
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.Create(
                CopyRequiredOptions(source, missingCatalog)));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapEnablesTheProductionRequirement()
    {
        var options = CaptureProductionOptions();
        Assert.True(options.RequireExternalContentCatalogs);
        Assert.NotNull(options.Content);
        Assert.NotNull(options.WorldRules);
        Assert.NotNull(options.Spells);
        Assert.NotNull(options.CombatRules);
        Assert.NotNull(options.Items);
        Assert.NotNull(options.Experience);
        Assert.NotNull(options.PlayerClasses);
        Assert.NotNull(options.PlayerAnimations);
        Assert.NotNull(options.SpellBooks);
        Assert.NotNull(options.MonsterTypes);
        Assert.NotNull(options.MonsterMissiles);
        Assert.NotNull(options.NativeMissiles);
        Assert.NotNull(options.NativeMissileSprites);
    }

    private static DiabloSimulationOptions CaptureProductionOptions()
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var content = ComposedContent.Compose(new ContentManifest(
            "external-content-requirement-tests",
            "1",
            [basePack]));
        var factory = new CapturingFactory();
        var bootstrap = new DiabloContentBootstrap(factory);

        Assert.Throws<ActivationCapturedException>(() => bootstrap.Activate(content));
        return factory.Options
            ?? throw new InvalidOperationException("The bootstrap did not provide options.");
    }

    private static DiabloSimulationOptions CopyRequiredOptions(
        DiabloSimulationOptions source,
        string missingCatalog)
    {
        return new DiabloSimulationOptions(source.StoreCatalog, source.StoreRules) {
            Content = missingCatalog == "Content" ? null : source.Content,
            WorldRules = missingCatalog == "WorldRules" ? null : source.WorldRules,
            Spells = missingCatalog == "Spells" ? null : source.Spells,
            CombatRules = missingCatalog == "CombatRules" ? null : source.CombatRules,
            Items = missingCatalog == "Items" ? null : source.Items,
            Experience = missingCatalog == "Experience" ? null : source.Experience,
            PlayerClasses = missingCatalog == "PlayerClasses" ? null : source.PlayerClasses,
            PlayerAnimations = missingCatalog == "PlayerAnimations"
                ? null
                : source.PlayerAnimations,
            SpellBooks = missingCatalog == "SpellBooks" ? null : source.SpellBooks,
            MonsterTypes = missingCatalog == "MonsterTypes" ? null : source.MonsterTypes,
            MonsterMissiles = missingCatalog == "MonsterMissiles"
                ? null
                : source.MonsterMissiles,
            NativeMissiles = missingCatalog == "NativeMissiles"
                ? null
                : source.NativeMissiles,
            NativeMissileSprites = missingCatalog == "NativeMissileSprites"
                ? null
                : source.NativeMissileSprites,
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

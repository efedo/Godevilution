using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeTownConstructionTests
{
    [Fact]
    public void NativeTownCatalogRequiresVersionElevenProgression()
    {
        var catalog = AuthoritativeNativeTownCatalogTests.LoadCatalog();
        AssertFactoryRefuses(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeTown = catalog,
        });

        AssertFactoryRefuses(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeTown = catalog,
            NativeProgression = CreateDepthProgression(DiabloCohortState.NativeDepthProgressionFormatVersion),
        });
    }

    [Fact]
    public void VersionElevenProgressionRequiresTownCatalogAndPopulationPolicy()
    {
        var progression = CreateDepthProgression(DiabloCohortState.NativeTownProgressionFormatVersion);
        AssertFactoryRefuses(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeProgression = progression,
        });

        AssertFactoryRefuses(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeTown = AuthoritativeNativeTownCatalogTests.LoadCatalog(),
            NativeProgression = progression,
        });
    }

    [Fact]
    public void NativeTownRestoreCannotBeSuppliedWithoutItsCatalog()
    {
        var catalog = AuthoritativeNativeTownCatalogTests.LoadCatalog();
        var checkpoint = catalog.Capture(catalog.CreateInitialGeometry(), []);
        AssertFactoryRefuses(new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            NativeTownRestore = checkpoint,
        });
    }

    [Fact]
    public void CheckpointConstructorRejectsNullSettingsBeforeReadingThem()
    {
        var catalog = AuthoritativeNativeTownCatalogTests.LoadCatalog();
        var checkpoint = catalog.Capture(catalog.CreateInitialGeometry(), []);

        Assert.Throws<InvalidDataException>(() => new DiabloNativeTownCheckpoint(
            checkpoint.CatalogFingerprint,
            checkpoint.LevelId,
            checkpoint.GeneratorId,
            checkpoint.GeneratorRevision,
            checkpoint.WarpPolicyVersion,
            null!,
            checkpoint.WarpOpenings,
            checkpoint.InitialRandomState,
            checkpoint.FinalRandomState,
            checkpoint.ViewPositionX,
            checkpoint.ViewPositionY,
            checkpoint.MinimumDungeonPositionX,
            checkpoint.MinimumDungeonPositionY,
            checkpoint.MaximumDungeonPositionX,
            checkpoint.MaximumDungeonPositionY,
            checkpoint.ClosedHellTileChoices,
            checkpoint.Mutations,
            checkpoint.NativePieceLayer,
            checkpoint.NativeMegaTileLayer,
            checkpoint.NativeProtectedMegaCells,
            checkpoint.NativeSpecialLayer,
            checkpoint.NativeTransparencyLayer,
            checkpoint.NativeSolPropertyLayer,
            checkpoint.NativeSolidCells,
            checkpoint.NativeMissileBlockedCells,
            checkpoint.NativeBlockLightCells,
            checkpoint.NativePieceProperties));
    }

    [Fact]
    public void LegacySimulationConstructionWithoutTownOwnerStillSucceeds()
    {
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules);

        var simulation = DiabloSimulationFactory.Instance.Create(options);

        Assert.NotNull(simulation);
        Assert.False(simulation.WorldState.HasNativeTown);
    }

    private static void AssertFactoryRefuses(DiabloSimulationOptions options)
        => Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(options));

    private static AuthoritativeCathedralNativeProgression CreateDepthProgression(int cohortFormatVersion)
    {
        var baseContent = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(baseContent, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(baseContent, "class_animations.tsv")),
            classes);
        var doors = Enumerable.Range(1, AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
            .Select(depth => new AuthoritativeCathedralDoorDefinition(
                (uint)depth,
                (uint)(depth * 2),
                (uint)(depth * 2 + 1)))
            .ToArray();

        return new AuthoritativeCathedralNativeProgression(
            new string('A', 64),
            animations,
            _ => throw new InvalidOperationException("The construction guard must reject before progression use."),
            AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth,
            cohortFormatVersion,
            doors);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var roots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in roots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

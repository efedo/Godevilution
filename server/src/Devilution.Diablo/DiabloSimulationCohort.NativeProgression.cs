namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    private static DiabloSimulationOptions PrepareNativeProgressionCohortOptions(
        DiabloSimulationOptions options, DiabloCohortState state)
    {
        if (state.FormatVersion != DiabloCohortState.NativeProgressionFormatVersion) {
            if (options.NativeProgression is not null)
                throw new InvalidDataException("The native progression profile requires its separate cohort schema.");
            return options;
        }
        if (options.NativeProgression?.FreshActivation is not { } activate
            || state.NativeProgression?.RunFingerprint != options.NativeProgression.Fingerprint)
            throw new InvalidDataException("A native progression cohort requires the exact validated run definition.");
        // The visited-floor aggregate identity is regenerated below; every other
        // active configuration fence must pass before replacing construction inputs.
        ValidateCohortConfiguration(options, state.Configuration with {
            CathedralItemGenerationFingerprint = (options.NativeProgressionRestore?.Items
                ?? options.NativeCathedralItems)?.GenerationFingerprint,
        });
        // Fresh activation reconstructs trusted immutable provenance from content. It never
        // borrows an initial target, store or geometry object already advanced by a live run.
        var fresh = activate();
        var trusted = fresh.TrustedOptions
            ?? throw new InvalidDataException("Fresh native run activation omitted trusted construction inputs.");
        var definition = trusted.NativeProgression!;
        if (definition.Fingerprint != state.NativeProgression.RunFingerprint)
            throw new InvalidDataException("Fresh native run activation changed its stable identity.");
        foreach (var entry in state.NativeProgression.Entries) {
            var generated = definition.Restore(entry);
            var door = trusted.CathedralDoors!.Definitions.Single() with { LevelId = entry.LevelId };
            var admission = fresh.Simulation.WorldState.PrepareNativeFloorAdmission(generated, door,
                trusted.Items!, trusted.Spells!, []);
            fresh.Simulation.WorldState.CommitNativeProgressionEntry(admission, entry);
        }
        trusted.NativeProgressionRestore = fresh.Simulation.WorldState.CaptureNativeProgressionOwners();
        return trusted;
    }
}

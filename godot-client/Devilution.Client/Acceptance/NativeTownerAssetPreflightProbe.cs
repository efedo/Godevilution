using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

/** Headless real-texture preflight for a complete optional resident family. */
public partial class NativeTownerAssetPreflightProbe : Node
{
    public override void _Ready()
    {
        try {
            var assets = new NativeTownerAssets();
            var rows = assets.Catalog?.Residents.Select(row => new NativeTownResidentSnapshot {
                ResidentSlot = row.ResidentSlot, TownerId = row.TownerId, LevelId = 0,
                PositionX = row.PositionX, PositionY = row.PositionY, NativeDirection = row.NativeDirection,
            }).ToArray() ?? Array.Empty<NativeTownResidentSnapshot>();
            if (System.Environment.GetEnvironmentVariable("GODEVILUTION_TOWNER_PROBE_ROW_LEVEL") == "1" && rows.Length > 0)
                rows[0].LevelId = 1;
            var active = assets.Bind(System.Environment.GetEnvironmentVariable("GODEVILUTION_TOWNER_PROBE_CONTENT"),
                System.Environment.GetEnvironmentVariable("GODEVILUTION_TOWNER_PROBE_FINGERPRINT") ?? string.Empty, rows);
            var expected = System.Environment.GetEnvironmentVariable("GODEVILUTION_TOWNER_PROBE_EXPECT_ACTIVE") == "1";
            var observation = assets.ObservePack();
            var passed = active == expected && (active
                ? observation.LoadedAtlasPaths.Count > 0 && observation.MissingAtlasPaths.Count == 0
                : !string.IsNullOrWhiteSpace(observation.Diagnostic));
            var output = System.Environment.GetEnvironmentVariable("GODEVILUTION_TOWNER_PROBE_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
                System.IO.File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = passed, Active = active, Pack = observation }));
            GD.Print($"Native Town resident atlas preflight: {(passed ? "passed" : "failed")}; active={active}.");
            GetTree().Quit(passed ? 0 : 1);
        } catch (Exception error) {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}

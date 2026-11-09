using System.Diagnostics;
using System.Text.Json;
using Devilution.Client.Diagnostics;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Google.Protobuf.Collections;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PresentationFrameCostTests(ITestOutputHelper output)
{
    [Fact]
    public void AtlasPathsResolveOnceAndCacheFailureWithoutHotPathAllocations()
    {
        Assert.True(NativeSceneCatalog.TryParse(NativeSceneCatalogTests.CreateCatalogJson(), out var catalog, out _));
        Assert.True(catalog!.TryResolveFamily(new string('a', 64), 7, out var family));
        var cache = new NativeSceneAtlasPathCache("res://private/native/catalog.json");
        var expected = cache.Resolve(family, 0);
        Assert.Equal("res://private/native/sprites/town/frames.png", expected.ResourcePath);
        var missing = cache.Resolve(family, 999);
        Assert.NotNull(missing.Diagnostic);
        for (var i = 0; i < 1000; i++) cache.Resolve(family, 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < 20000; i++) cache.Resolve(family, 0);
        var elapsed = Stopwatch.GetElapsedTime(started); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, bytes); Assert.Equal(2, cache.ResolutionCount); Assert.Same(missing, cache.Resolve(family, 999));
        output.WriteLine($"20,000 cached atlas lookups: {elapsed.TotalMilliseconds:0.000} ms, {bytes} allocated bytes, one path resolution per atlas.");
        Assert.True(family.TryGetAtlas(0, out var atlas));
        allocated = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
        string? originalPath = null;
        for (var i = 0; i < 20000; i++)
            PresentationAssetPath.TryCombineResourcePath("res://private/native/catalog.json", atlas.Path, out originalPath, out _);
        elapsed = Stopwatch.GetElapsedTime(started); bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(expected.ResourcePath, originalPath); Assert.True(bytes > 0);
        output.WriteLine($"20,000 original per-draw path normalizations: {elapsed.TotalMilliseconds:0.000} ms, {bytes} allocated bytes (texture submission excluded).");
    }

    [Fact]
    public void HudChangesOnlyConsumeVisibleDataOrSelectionChanges()
    {
        var gate = new PresentationChangeGate<(RepeatedField<ItemSnapshot> Items, int Selection)>();
        var items = new RepeatedField<ItemSnapshot> {new ItemSnapshot {ItemSeed = 1}};
        Assert.False(gate.ShouldUpdate(false, (items, -1)));
        Assert.True(gate.ShouldUpdate(true, (items, -1)));
        Assert.False(gate.ShouldUpdate(true, (items.Clone(), -1))); // Another snapshot, identical panel data.
        Assert.True(gate.ShouldUpdate(true, (items, 0)));
        var changed = items.Clone(); changed[0].ItemSeed = 2;
        Assert.False(gate.ShouldUpdate(false, (changed, 0)));
        Assert.True(gate.ShouldUpdate(true, (changed, 0)));
        Assert.False(gate.ShouldUpdate(true, (changed, 0)));
        gate.Invalidate(); Assert.True(gate.ShouldUpdate(true, (changed, 0)));
    }

    [Fact]
    public void SpellSourceCacheIgnoresTicksMovementAndResourcesButObservesOwnershipAndCharges()
    {
        var cache = new AuthoritativeSpellSourceCache();
        var snapshot = new Snapshot {Players = {new PlayerSnapshot {
            MemorizedSpells = {new PlayerSpellLevelSnapshot {SpellId = 1, Level = 2}},
            Equipment = {new EquippedItemSnapshot {Slot = 4, ItemSeed = 12,
                State = new() {SpellId = 2, Charges = 5, SpellSourceKind = ItemSpellSourceKind.ChargedItem}}},
        }}};
        var sources = cache.GetSources(snapshot);
        var next = snapshot.Clone(); next.Tick++; next.Players[0].PositionX++;
        next.Players[0].Life = 5; next.Players[0].Mana = 10;
        Assert.Same(sources, cache.GetSources(next));
        next = next.Clone(); next.Players[0].Equipment[0].State.Charges = 4;
        var changed = cache.GetSources(next);
        Assert.NotSame(sources, changed);
        Assert.Contains(changed, source => source.DisplayName.Contains("4 charges", StringComparison.Ordinal));
        next = next.Clone(); next.Players[0].MemorizedSpells.Clear();
        Assert.DoesNotContain(cache.GetSources(next), source => source.Kind == CastSourceKind.MemorizedSpell);
        Assert.Empty(cache.GetSources(null));
    }

    [Theory]
    [InlineData(0, 0, 16)] [InlineData(56, 78, 16)] [InlineData(33, 62, 10)]
    [InlineData(111, 111, 10)] [InlineData(56.125, 78.375, 16)]
    public void RetainedViewportSelectionExactlyMatchesTheOriginalFullGridScan(double x, double y, byte length)
    {
        var view = NativeSceneViewport.Create(808, 520, 112, 112, x, y);
        var order = NativeSceneCatalog.ResolveIsometricCellOrder(112, 112);
        var ranks = order.Select((cell, rank) => (cell, rank)).ToDictionary(value => value.cell, value => value.rank);
        var expected = order.Where(cell => {
            var anchor = view.CellBottomAnchor(cell.X, cell.Y);
            var left = anchor.X - 32; var top = anchor.Y - 31 - (length / 2 - 1) * 32;
            return left < 808 && left + 64 > 0 && top < 520 && anchor.Y + 1 > 0;
        }).ToArray();
        var actual = NativeSceneRetentionState.VisiblePieceCells(view, length).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(actual, actual.GroupBy(cell => ranks[cell] / NativeSceneRetentionState.FloorChunkCells)
            .OrderBy(group => group.Key).SelectMany(group => group));
    }

    [Theory]
    [InlineData(56, 78)] [InlineData(56.125, 78.375)]
    public void RetainedRastersKeepOriginalSlotOrderPlacementAndOcclusionLayers(double cameraX, double cameraY)
    {
        var floor = new NativeSceneRasterReference(0, 0, 0, 32, 32);
        var wall = new NativeSceneRasterReference(0, 32, 0, 32, 32);
        var foliage = new NativeSceneRasterReference(0, 64, 0, 32, 16);
        NativeSceneSlot Slot(byte index, bool isFloor, NativeSceneSlotRole role, NativeSceneRasterReference raster)
            => new(0, index, 0, 0, 0, 0, NativeSceneTileType.Square, null, NativeSceneTileType.Square,
                isFloor, role == NativeSceneSlotRole.Foliage, role, isFloor ? floor : null, raster);
        var family = new NativeSceneFamily(NativeSceneLocation.Town, 16, 2, [], [], [], [], [],
            [Slot(0, true, NativeSceneSlotRole.Content, wall), Slot(1, true, NativeSceneSlotRole.Content, wall),
                Slot(2, false, NativeSceneSlotRole.Foliage, foliage)], [], "after-actors");
        var special = new NativeSceneSpecial(0, new(1, 0, 0, 64, 80), "bottom-left", -32, -32, "after-actors", -1, -1);
        var view = NativeSceneViewport.Create(808, 520, 112, 112, cameraX, cameraY);
        var cell = new NativeSceneRetainedCell(view, 56, 78);
        Assert.True(cell.Update(family, 0, special));
        var anchor = view.CellBottomAnchor(56, 78);
        Assert.Equal(new NativeScenePixelBounds(anchor.X - 32, anchor.Y - 31, 32, 32), cell.Floors[0].Bounds);
        Assert.Equal(new NativeScenePixelBounds(anchor.X, anchor.Y - 31, 32, 32), cell.Floors[1].Bounds);
        Assert.Equal(new[] {wall, wall, foliage}, cell.Content.Select(draw => draw.Raster));
        Assert.Equal(new NativeScenePixelBounds(anchor.X - 32, anchor.Y - 31 - 32 - 16, 32, 16), cell.Content[2].Bounds);
        Assert.Equal(NativeScenePlacement.ResolveSpecialBounds(view, 56, 78, special), Assert.Single(cell.Specials).Bounds);

        // Actors, light, and a scrolling camera invalidate membership/transform, not retained raster commands.
        var retainedFloors = cell.Floors.ToArray();
        Assert.False(cell.Update(family, 0, special));
        Assert.Equal(retainedFloors, cell.Floors);
        var scrolling = NativeSceneViewport.Create(808, 520, 112, 112, cameraX + 0.25, cameraY);
        var movement = scrolling.CellBottomAnchor(56, 78);
        Assert.Equal(movement.X - 32, cell.Floors[0].Bounds.X + scrolling.OriginX - view.OriginX);

        Assert.True(cell.Update(family, 1, special)); // A door changes this d-piece to an empty piece.
        Assert.Empty(cell.Floors); Assert.Empty(cell.Content); Assert.Single(cell.Specials);
        Assert.True(cell.Update(family, 1, null)); Assert.Empty(cell.Specials);
        Assert.False(cell.Update(family, 1, null));
        Assert.True(cell.Update(family, 0, null)); Assert.Equal(2, cell.Floors.Count);
    }

    [Fact]
    public void LevelGeometryLightObjectAndCameraChangesHaveSeparateInvalidation()
    {
        var state = new NativeSceneRetentionState(); var geometry = new object();
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 56, 78);
        Assert.True(state.Update(geometry, 0, view, 1, 10).HasFlag(NativeSceneRefresh.Geometry));
        Assert.Equal(NativeSceneRefresh.None, state.Update(geometry, 0, view, 1, 10));
        Assert.Equal(NativeSceneRefresh.Actors, state.Update(geometry, 0, view, 2, 10)); // Object/door state in an admitted snapshot.
        Assert.Equal(NativeSceneRefresh.Actors | NativeSceneRefresh.Visibility, state.Update(geometry, 0, view, 2, 12));
        var scrolling = NativeSceneViewport.Create(808, 520, 112, 112, 56.1, 78.2);
        Assert.Equal(NativeSceneRefresh.Viewport, state.Update(geometry, 0, scrolling, 2, 12));
        Assert.True(state.Update(new object(), 0, scrolling, 2, 12).HasFlag(NativeSceneRefresh.Geometry)); // Door d-piece mutation.
        Assert.True(state.Update(geometry, 1, scrolling, 2, 12).HasFlag(NativeSceneRefresh.Geometry));
    }

    [Fact]
    public void StaticTownSchedulingDoesNoPerFrameRebuildOrManagedAllocation()
    {
        var state = new NativeSceneRetentionState(); var geometry = new object();
        var view = NativeSceneViewport.Create(808, 520, 112, 112, 56, 78);
        state.Update(geometry, 0, view, 1, 10);
        for (var i = 0; i < 1000; i++) state.Update(geometry, 0, view, 1, 10);
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp(); var rebuilds = 0;
        for (var i = 0; i < 20000; i++) if (state.Update(geometry, 0, view, 1, 10) != NativeSceneRefresh.None) rebuilds++;
        var elapsed = Stopwatch.GetElapsedTime(started); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, rebuilds); Assert.Equal(0, bytes);
        var visible = NativeSceneRetentionState.VisiblePieceCells(view, 16).Count();
        output.WriteLine($"20,000 static Town scheduling frames: {elapsed.TotalMilliseconds:0.000} ms, {bytes} bytes, {rebuilds} rebuilds. Culling visits {visible} cells instead of 12,544.");
    }

    [Fact]
    public void NormalCaptureReusesRecorderAndWritesBoundedPerSecondCosts()
    {
        var root = Path.Combine(Path.GetTempPath(), "Godevilution-FrameCost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var recorder = ClientFrameEvidenceRecorder.Create(new(Path.Combine(root, "capture"), TimeSpan.Zero, TimeSpan.FromSeconds(60), 128, FrameCostOnly: true),
                new("build", "schema", "content", "rules", "renderer", "device", "808x520", true, 60, true, "4.7.1", 1, VsyncMode: "Enabled"));
            recorder.ObserveServerIdentity(new("build", "schema", "content", "rules", 20, GameMode.SinglePlayer)); recorder.ObserveParticipantCount(1);
            var started = Stopwatch.GetTimestamp();
            recorder.ObserveWorldAdmission(started);
            for (var i = 0; i < 120; i++) {
                var timestamp = started + i * Stopwatch.Frequency / 60;
                recorder.RecordFrameCost(timestamp, new(1, 2, 3, 4, i == 0 ? 1354 : 0, 20, null, null));
                recorder.RecordFrame(timestamp, timestamp, timestamp);
            }
            recorder.Stop("normal-window", true);
            using var document = JsonDocument.Parse(File.ReadAllText(recorder.EvidenceFilePath));
            Assert.Empty(document.RootElement.GetProperty("Samples").EnumerateArray());
            var second = Assert.Single(document.RootElement.GetProperty("FrameCosts").EnumerateArray());
            Assert.Equal(60, second.GetProperty("FramesPerSecond").GetDouble(), 6);
            Assert.Equal(1354, second.GetProperty("TerrainSubmissions").GetInt64());
            Assert.Equal(2, second.GetProperty("WorldUpdateMillisecondsPerFrame").GetDouble());
            Assert.Equal(JsonValueKind.Null, second.GetProperty("ViewportRenderGpuMillisecondsPerFrame").ValueKind);
            Assert.Equal(0, recorder.DroppedSampleCount);
        } finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(null, null, false)] [InlineData(null, "normal", true)]
    [InlineData("performance", null, false)] [InlineData("performance", "normal", false)]
    public void PerformanceCaptureKeepsItsExplicitWorkloadStart(string? performance, string? normal, bool automaticStart)
        => Assert.Equal(automaticStart, ClientFrameEvidenceRecorder.UsesNormalFrameCapture(performance, normal));
}

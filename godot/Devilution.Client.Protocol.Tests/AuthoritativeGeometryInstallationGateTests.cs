using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeGeometryInstallationGateTests
{
    [Fact]
    public void Synthetic112By112ProjectionCopiesOncePerAcceptedGeneration()
    {
        const int cellCount = 112 * 112;
        var geometry = new LevelGeometrySnapshot {
            LevelId = 1,
            Width = 112,
            Height = 112,
        };
        geometry.NativeTransparency.Add(Enumerable.Repeat(0U, cellCount));
        geometry.NativePieceLayer.Add(Enumerable.Repeat(1U, cellCount));
        geometry.NativeObjectLayer.Add(Enumerable.Repeat(2U, cellCount));

        var gate = new AuthoritativeGeometryInstallationGate();
        var installationCount = 0;
        var copiedCells = 0;
        var installedTransparency = Array.Empty<uint>();
        var installedPieces = Array.Empty<uint>();
        var installedObjects = Array.Empty<uint>();
        var eagerRefreshCount = 0;
        for (var frame = 0; frame < 60; frame++) {
            // The legacy path would refresh on every frame; count that decision
            // without duplicating its allocation-heavy copies in this test.
            eagerRefreshCount++;
            if (!gate.NeedsInstallation(17))
                continue;
            installationCount++;
            installedTransparency = geometry.NativeTransparency.ToArray();
            installedPieces = geometry.NativePieceLayer.ToArray();
            installedObjects = geometry.NativeObjectLayer.ToArray();
            copiedCells += installedTransparency.Length
                + installedPieces.Length
                + installedObjects.Length;
            gate.MarkInstalled(17);
        }

        Assert.Equal(1, installationCount);
        Assert.Equal(cellCount * 3, copiedCells);
        Assert.Equal(60, eagerRefreshCount);
        Assert.NotSame(geometry.NativeTransparency, installedTransparency);
        Assert.NotSame(geometry.NativePieceLayer, installedPieces);
        Assert.NotSame(geometry.NativeObjectLayer, installedObjects);

        geometry.NativeTransparency[0] = 99;
        geometry.NativePieceLayer[0] = 98;
        geometry.NativeObjectLayer[0] = 97;
        Assert.Equal(0U, installedTransparency[0]);
        Assert.Equal(1U, installedPieces[0]);
        Assert.Equal(2U, installedObjects[0]);

        Assert.True(gate.NeedsInstallation(18));
        installedTransparency = geometry.NativeTransparency.ToArray();
        installedPieces = geometry.NativePieceLayer.ToArray();
        installedObjects = geometry.NativeObjectLayer.ToArray();
        gate.MarkInstalled(18);
        Assert.Equal(99U, installedTransparency[0]);
        Assert.Equal(98U, installedPieces[0]);
        Assert.Equal(97U, installedObjects[0]);

        var emptyGeometry = new LevelGeometrySnapshot { LevelId = 1, Width = 112, Height = 112 };
        Assert.True(gate.NeedsInstallation(19));
        Assert.Empty(emptyGeometry.NativeTransparency.ToArray());
        gate.MarkInstalled(19);
        Assert.False(gate.NeedsInstallation(19));
    }

    [Fact]
    public void NewAndInvalidatedGenerationsInstallExactlyOnce()
    {
        var gate = new AuthoritativeGeometryInstallationGate();
        var installations = new List<ulong>();

        InstallIfNeeded(gate, 1, installations);
        InstallIfNeeded(gate, 1, installations);
        InstallIfNeeded(gate, 2, installations);
        InstallIfNeeded(gate, 2, installations);
        gate.Invalidate();
        InstallIfNeeded(gate, 2, installations);
        InstallIfNeeded(gate, 2, installations);

        Assert.Equal([1UL, 2UL, 2UL], installations);
    }

    [Fact]
    public void FailedInstallationRemainsPendingUntilMarkedComplete()
    {
        var gate = new AuthoritativeGeometryInstallationGate();
        var attempts = 0;

        for (var poll = 0; poll < 3; poll++) {
            if (!gate.NeedsInstallation(23))
                continue;
            attempts++;
            if (attempts == 1)
                continue;
            gate.MarkInstalled(23);
        }

        Assert.Equal(2, attempts);
        Assert.False(gate.NeedsInstallation(23));
    }

    [Fact]
    public void ZeroRevisionCanBeInvalidatedAndInstalledAgain()
    {
        var gate = new AuthoritativeGeometryInstallationGate();

        Assert.True(gate.NeedsInstallation(0));
        gate.MarkInstalled(0);
        Assert.False(gate.NeedsInstallation(0));

        gate.Invalidate();

        Assert.True(gate.NeedsInstallation(0));
    }

    private static void InstallIfNeeded(
        AuthoritativeGeometryInstallationGate gate,
        ulong revision,
        ICollection<ulong> installations)
    {
        if (!gate.NeedsInstallation(revision))
            return;
        installations.Add(revision);
        gate.MarkInstalled(revision);
    }
}
